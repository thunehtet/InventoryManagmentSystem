using ClothInventoryApp.Data;
using ClothInventoryApp.Dto;
using ClothInventoryApp.Dto.Stock;
using ClothInventoryApp.Models;
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

        public StockController(AppDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
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

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var stockMovement = await _context.StockMovements.FindAsync(id);

            if (stockMovement == null)
                return NotFound();

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

            stockMovement.ProductVariantId = dto.ProductVariantId;
            stockMovement.MovementType = dto.MovementType;
            stockMovement.Quantity = dto.Quantity;
            stockMovement.MovementDate = dto.MovementDate;
            stockMovement.Remarks = dto.Remarks;

            _context.StockMovements.Update(stockMovement);
            await _context.SaveChangesAsync();

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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var stockMovement = await _context.StockMovements.FindAsync(id);

            if (stockMovement == null)
                return NotFound();

            _context.StockMovements.Remove(stockMovement);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadVariantDropDown()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
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
                new SelectListItem { Value = "OUT", Text = "OUT" },
                new SelectListItem { Value = "ADJUST", Text = "ADJUST" }
            };
        }
       

        public async Task<IActionResult> StockIn()
        {
            await LoadProductVariants();
            return View(new StockInViewModel());
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

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                Quantity = vm.Quantity,
                MovementType = "IN",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

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

            var movement = new StockMovement
            {
                ProductVariantId = vm.ProductVariantId,
                Quantity = vm.Quantity,
                MovementType = "OUT",
                MovementDate = DateTime.UtcNow,
                Remarks = vm.Remarks
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Inventory));
        }

        public async Task<IActionResult> Inventory(string? search, bool lowstock = false)
        {
            var query = _context.ProductVariants
                .Include(v => v.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v =>
                    v.Product.Name.Contains(search) ||
                    v.SKU.Contains(search) ||
                    v.Color.Contains(search) ||
                    v.Size.Contains(search));

            var inventory = await query
                .Select(v => new InventoryViewModel
                {
                    ProductVariantId = v.Id,
                    ProductName = v.Product.Name,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    CurrentStock =
                        (v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0)
                        - (v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0)
                })
                .ToListAsync();

            if (lowstock)
                inventory = inventory.Where(x => x.CurrentStock < 10).ToList();

            ViewBag.Search = search;
            ViewBag.LowStock = lowstock;
            return View(inventory);
        }

        private async Task LoadProductVariants()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
                .Select(v => new
                {
                    v.Id,
                    DisplayText = v.Product.Name + " - " + v.SKU + " - " + v.Color + " - " + v.Size
                })
                .ToListAsync();

            ViewBag.ProductVariants = new SelectList(variants, "Id", "DisplayText");
        }
    }
}