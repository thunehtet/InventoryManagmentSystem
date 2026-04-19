using ClothInventoryApp.Data;
using ClothInventoryApp.Dto;
using ClothInventoryApp.Dto.Stock;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Stock;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class StockController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly IStockService _stockService;

        public StockController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            IStockService stockService)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _stockService = stockService;
        }
        

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.StockMovements
                .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s =>
                    s.ProductVariant.Product.Name.Contains(search) ||
                    s.ProductVariant.SKU.Contains(search) ||
                    s.MovementType.Contains(search) ||
                    (s.Remarks != null && s.Remarks.Contains(search)));

            var total = await query.CountAsync();
            var stockMovements = await query
                .OrderByDescending(s => s.MovementDate)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(s => new ViewStockDto
                {
                    Id = s.Id,
                    ProductVariantId = s.ProductVariantId,
                    ProductVariantName = s.ProductVariant.Product.Name + " - " +
                                         s.ProductVariant.SKU + " - " +
                                         s.ProductVariant.Color + " - " +
                                         s.ProductVariant.Size,
                    MovementType = s.MovementType,
                    Quantity = s.Quantity,
                    MovementDate = s.MovementDate,
                    Remarks = s.Remarks
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(stockMovements);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await LoadVariantDropDown();
            ViewBag.MovementTypes = GetMovementTypes();
            return View(new CreateStockDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateStockDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }
            var tenantId = _tenantProvider.GetTenantId();
            dto.MovementType = NormalizeMovementType(dto.MovementType);

            if (!await VariantAvailableForManualMovementAsync(dto.ProductVariantId))
            {
                ModelState.AddModelError(nameof(dto.ProductVariantId), "Please select a valid active product variant in your workspace.");
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }

            if (!await _stockService.CanApplyMovementAsync(
                tenantId,
                dto.ProductVariantId,
                dto.MovementType,
                dto.Quantity))
            {
                ModelState.AddModelError(nameof(dto.Quantity), "Not enough stock for this stock-out movement.");
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }

            var stockMovement = new StockMovement
            {
                ProductVariantId = dto.ProductVariantId,
                TenantId = tenantId,
                MovementType = dto.MovementType,
                Quantity = dto.Quantity,
                MovementDate = dto.MovementDate,
                Remarks = dto.Remarks
            };

            _context.StockMovements.Add(stockMovement);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Stock movement saved.";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Stock");
            TempData["SuccessListLabel"]= "View Movements";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var stockMovement = await _context.StockMovements.FindAsync(id);

            if (stockMovement == null)
                return NotFound();

            if (stockMovement.SaleId != null)
            {
                TempData["Error"] = "This stock movement was created by a sale and cannot be edited here. Update the sale instead.";
                return RedirectToAction(nameof(Index));
            }

            var dto = new ViewStockDto
            {
                Id = stockMovement.Id,
                ProductVariantId = stockMovement.ProductVariantId,
                MovementType = stockMovement.MovementType,
                Quantity = stockMovement.Quantity,
                MovementDate = stockMovement.MovementDate,
                Remarks = stockMovement.Remarks
            };

            await LoadVariantDropDown();
            ViewBag.MovementTypes = GetMovementTypes();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(ViewStockDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }

            var stockMovement = await _context.StockMovements.FindAsync(dto.Id);

            if (stockMovement == null)
                return NotFound();

            if (stockMovement.SaleId != null)
            {
                TempData["Error"] = "This stock movement was created by a sale and cannot be edited here. Update the sale instead.";
                return RedirectToAction(nameof(Index));
            }

            dto.MovementType = NormalizeMovementType(dto.MovementType);
            if (!await VariantAvailableForManualMovementAsync(dto.ProductVariantId))
            {
                ModelState.AddModelError(nameof(dto.ProductVariantId), "Please select a valid active product variant in your workspace.");
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }

            if (!await CanReplaceMovementAsync(stockMovement, dto.ProductVariantId, dto.MovementType, dto.Quantity))
            {
                ModelState.AddModelError(nameof(dto.Quantity), "This change would make stock go below zero.");
                await LoadVariantDropDown();
                ViewBag.MovementTypes = GetMovementTypes();
                return View(dto);
            }

            stockMovement.ProductVariantId = dto.ProductVariantId;
            stockMovement.MovementType = dto.MovementType;
            stockMovement.Quantity = dto.Quantity;
            stockMovement.MovementDate = dto.MovementDate;
            stockMovement.Remarks = dto.Remarks;

            _context.StockMovements.Update(stockMovement);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Stock movement updated.";
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Stock");
            TempData["SuccessListLabel"]= "View Movements";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var stockMovement = await _context.StockMovements
                .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
                .Where(s => s.Id == id)
                .Select(s => new ViewStockDto
                {
                    Id = s.Id,
                    ProductVariantId = s.ProductVariantId,
                    ProductVariantName = s.ProductVariant.Product.Name + " - " +
                                         s.ProductVariant.SKU + " - " +
                                         s.ProductVariant.Color + " - " +
                                         s.ProductVariant.Size,
                    MovementType = s.MovementType,
                    Quantity = s.Quantity,
                    MovementDate = s.MovementDate,
                    Remarks = s.Remarks
                })
                .FirstOrDefaultAsync();

            if (stockMovement == null)
                return NotFound();

            return View(stockMovement);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var isSaleLinked = await _context.StockMovements
                .Where(s => s.Id == id)
                .Select(s => s.SaleId != null)
                .FirstOrDefaultAsync();

            var stockMovement = await _context.StockMovements
                .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
                .Where(s => s.Id == id)
                .Select(s => new ViewStockDto
                {
                    Id = s.Id,
                    ProductVariantId = s.ProductVariantId,
                    ProductVariantName = s.ProductVariant.Product.Name + " - " +
                                         s.ProductVariant.SKU + " - " +
                                         s.ProductVariant.Color + " - " +
                                         s.ProductVariant.Size,
                    MovementType = s.MovementType,
                    Quantity = s.Quantity,
                    MovementDate = s.MovementDate,
                    Remarks = s.Remarks
                })
                .FirstOrDefaultAsync();

            if (stockMovement == null)
                return NotFound();

            ViewBag.CanDelete = !isSaleLinked;
            ViewBag.DeleteMessage = !isSaleLinked
                ? null
                : "This stock movement was created by a sale and cannot be deleted here. Delete or void the sale instead.";
            return View(stockMovement);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var stockMovement = await _context.StockMovements.FindAsync(id);

            if (stockMovement == null)
                return NotFound();

            if (stockMovement.SaleId != null)
            {
                TempData["Error"] = "This stock movement was created by a sale and cannot be deleted here. Delete or void the sale instead.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            if (!await CanDeleteMovementAsync(stockMovement))
            {
                TempData["Error"] = "This stock movement cannot be deleted because doing so would make stock go below zero.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.StockMovements.Remove(stockMovement);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Stock movement deleted.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Stock");
            TempData["SuccessListLabel"]= "View Movements";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadVariantDropDown()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Product.IsActive)
                .OrderBy(v => v.SKU)
                .Select(v => new
                {
                    v.Id,
                    Text = v.Product.Name + " - " + v.SKU + " - " + v.Color + " - " + v.Size
                })
                .ToListAsync();

            ViewBag.ProductVariants = new SelectList(variants, "Id", "Text");
        }

        private List<SelectListItem> GetMovementTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "IN", Text = "IN" },
                new SelectListItem { Value = "OUT", Text = "OUT" }
            };
        }
       

        public async Task<IActionResult> StockIn(Guid? variantId = null)
        {
            await LoadProductVariants();
            var vm = new StockInViewModel();
            if (variantId.HasValue) vm.ProductVariantId = variantId.Value;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await LoadProductVariants();
                return View(vm);
            }

            var tenantId = _tenantProvider.GetTenantId();

            if (!await VariantAvailableForManualMovementAsync(vm.ProductVariantId))
            {
                ModelState.AddModelError(nameof(vm.ProductVariantId), "Please select a valid active product variant in your workspace.");
                await LoadProductVariants();
                return View(vm);
            }

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                TenantId = tenantId,
                Quantity = vm.Quantity,
                MovementType = "IN",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Stock recorded successfully.";
            TempData["SuccessListUrl"]  = Url.Action("Inventory", "Stock");
            TempData["SuccessListLabel"]= "View Inventory";
            TempData["SuccessAddUrl"]   = Url.Action("StockIn", "Stock");
            TempData["SuccessAddLabel"] = "Record More Stock";
            TempData["SuccessAddHint"]  = "Accurate stock counts depend on recording every delivery. Add stock-in entries for each variant received to keep inventory up to date.";
            return RedirectToAction(nameof(Inventory));
        }

        public async Task<IActionResult> StockOut()
        {
            await LoadProductVariants();
            return View(new StockInViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockInViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await LoadProductVariants();
                return View(vm);
            }

            var tenantId = _tenantProvider.GetTenantId();
            if (!await VariantAvailableForManualMovementAsync(vm.ProductVariantId))
            {
                ModelState.AddModelError(nameof(vm.ProductVariantId), "Please select a valid active product variant in your workspace.");
                await LoadProductVariants();
                return View(vm);
            }

            if (!await _stockService.CanApplyMovementAsync(
                tenantId,
                vm.ProductVariantId,
                "OUT",
                vm.Quantity))
            {
                ModelState.AddModelError(nameof(vm.Quantity), "Not enough stock for this stock-out movement.");
                await LoadProductVariants();
                return View(vm);
            }

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                TenantId = tenantId,
                Quantity = vm.Quantity,
                MovementType = "OUT",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Stock-out recorded.";
            TempData["SuccessListUrl"]  = Url.Action("Inventory", "Stock");
            TempData["SuccessListLabel"]= "View Inventory";
            return RedirectToAction(nameof(Inventory));
        }

        public async Task<IActionResult> Inventory(string? search, bool lowstock = false, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var tenantId = _tenantProvider.GetTenantId();

            var tenantSettings = await _context.TenantSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);
            var lowStockThreshold = tenantSettings?.LowStockThreshold ?? 10;
            var isAdmin = User.IsInRole("Admin");
            var lowStockAlertVisible = isAdmin || (tenantSettings?.LowStockAlertEnabled != false);

            var query = _context.ProductVariants
                .Include(v => v.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v =>
                    v.Product.Name.Contains(search) ||
                    v.SKU.Contains(search) ||
                    v.Color.Contains(search) ||
                    v.Size.Contains(search));

            List<InventoryViewModel> inventory;
            var total = 0;

            if (lowstock)
            {
                var variants = await query
                    .Select(v => new
                    {
                        v.Id,
                        ProductName = v.Product.Name,
                        v.SKU,
                        v.Size,
                        v.Color
                    })
                    .ToListAsync();

                var stockMap = await _stockService.GetCurrentStockMapAsync(
                    tenantId,
                    variants.Select(v => v.Id));

                var lowStockInventory = variants
                    .Select(v => new InventoryViewModel
                    {
                        ProductVariantId = v.Id,
                        ProductName = v.ProductName,
                        SKU = v.SKU,
                        Size = v.Size,
                        Color = v.Color,
                        CurrentStock = stockMap.TryGetValue(v.Id, out var stock) ? stock : 0
                    })
                    .Where(x => x.CurrentStock < lowStockThreshold)
                    .OrderBy(x => x.CurrentStock)
                    .ThenBy(x => x.ProductName)
                    .ThenBy(x => x.SKU)
                    .ToList();

                total = lowStockInventory.Count;
                inventory = lowStockInventory
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToList();
            }
            else
            {
                total = await query.CountAsync();
                var variants = await query
                    .OrderBy(v => v.Product.Name)
                    .ThenBy(v => v.SKU)
                    .Skip((page - 1) * size)
                    .Take(size)
                    .Select(v => new
                    {
                        v.Id,
                        ProductName = v.Product.Name,
                        v.SKU,
                        v.Size,
                        v.Color
                    })
                    .ToListAsync();

                var stockMap = await _stockService.GetCurrentStockMapAsync(
                    tenantId,
                    variants.Select(v => v.Id));

                inventory = variants
                    .Select(v => new InventoryViewModel
                    {
                        ProductVariantId = v.Id,
                        ProductName = v.ProductName,
                        SKU = v.SKU,
                        Size = v.Size,
                        Color = v.Color,
                        CurrentStock = stockMap.TryGetValue(v.Id, out var stock) ? stock : 0
                    })
                    .ToList();
            }

            ViewBag.Search = search;
            ViewBag.LowStock = lowstock;
            ViewBag.LowStockThreshold = lowStockThreshold;
            ViewBag.LowStockAlertVisible = lowStockAlertVisible;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = size,
                TotalCount = total,
                Action = nameof(Inventory),
                Extra = new()
                {
                    ["search"] = search,
                    ["lowstock"] = lowstock ? "true" : null
                }
            };
            return View(inventory);
        }

        private async Task LoadProductVariants()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Product.IsActive)
                .Select(v => new
                {
                    v.Id,
                    DisplayText = v.Product.Name + " - " + v.SKU + " - " + v.Color + " - " + v.Size
                })
                .ToListAsync();

            ViewBag.ProductVariants = new SelectList(variants, "Id", "DisplayText");
        }

        private static string NormalizeMovementType(string? movementType)
        {
            return movementType?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        private async Task<bool> VariantAvailableForManualMovementAsync(Guid variantId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            return await _context.ProductVariants
                .Include(v => v.Product)
                .AnyAsync(v => v.Id == variantId && v.TenantId == tenantId && v.Product.IsActive);
        }

        private async Task<bool> CanDeleteMovementAsync(StockMovement movement)
        {
            var currentStock = await _stockService.GetCurrentStockAsync(movement.TenantId, movement.ProductVariantId);
            return currentStock - GetStockDelta(movement.MovementType, movement.Quantity) >= 0;
        }

        private async Task<bool> CanReplaceMovementAsync(
            StockMovement existingMovement,
            Guid newVariantId,
            string newMovementType,
            int newQuantity)
        {
            var affectedVariantIds = new[] { existingMovement.ProductVariantId, newVariantId }
                .Distinct()
                .ToList();

            foreach (var variantId in affectedVariantIds)
            {
                var currentStock = await _stockService.GetCurrentStockAsync(existingMovement.TenantId, variantId);
                var adjustedStock = currentStock;

                if (variantId == existingMovement.ProductVariantId)
                    adjustedStock -= GetStockDelta(existingMovement.MovementType, existingMovement.Quantity);

                if (variantId == newVariantId)
                    adjustedStock += GetStockDelta(newMovementType, newQuantity);

                if (adjustedStock < 0)
                    return false;
            }

            return true;
        }

        private static int GetStockDelta(string movementType, int quantity)
        {
            var normalizedType = NormalizeMovementType(movementType);
            return normalizedType == "IN"
                ? quantity
                : normalizedType == "OUT"
                    ? -quantity
                    : 0;
        }
    }
}
