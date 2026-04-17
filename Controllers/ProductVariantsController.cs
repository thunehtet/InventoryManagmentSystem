using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.ProductVariant;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class ProductVariantsController : TenantAwareController
    {
        private readonly ISubscriptionService _subscriptionService;

        public ProductVariantsController(AppDbContext context, ITenantProvider tenantProvider, ISubscriptionService subscriptionService)
            : base(context, tenantProvider)
        {
            _subscriptionService = subscriptionService;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.ProductVariants.Include(v => v.Product).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v =>
                    v.Product.Name.Contains(search) ||
                    v.SKU.Contains(search) ||
                    v.Color.Contains(search) ||
                    v.Size.Contains(search));

            var total = await query.CountAsync();
            var variants = await query
                .OrderBy(v => v.Product.Name).ThenBy(v => v.SKU)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(v => new ViewProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    CostPrice = v.CostPrice,
                    SellingPrice = v.SellingPrice
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(variants);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Guid? productId = null)
        {
            await LoadProductsDropDown();
            var dto = new CreateProductVariantDto();
            if (productId.HasValue) dto.ProductId = productId.Value;
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadProductsDropDown();
                return View(dto);
            }
            var tenantId = _tenantProvider.GetTenantId();

            if (!await ProductExistsForTenantAsync(dto.ProductId))
            {
                ModelState.AddModelError(nameof(dto.ProductId), "Please select a valid active product in your workspace.");
                await LoadProductsDropDown();
                return View(dto);
            }

            var skuExists = await _context.ProductVariants
                .AnyAsync(v => v.TenantId == tenantId && v.SKU == dto.SKU);
            if (skuExists)
            {
                ModelState.AddModelError(nameof(dto.SKU), "This SKU already exists in your workspace.");
                await LoadProductsDropDown();
                return View(dto);
            }

            if (!await _subscriptionService.CanAddVariantAsync(tenantId))
            {
                var (current, max) = await _subscriptionService.GetVariantLimitAsync(tenantId);
                TempData["LimitError"] = $"Variant limit reached ({current}/{max}). Upgrade your plan to add more variants.";
                return RedirectToAction(nameof(Index));
            }

            var variant = new ProductVariant
            {
                ProductId = dto.ProductId,
                TenantId = tenantId,
                SKU = dto.SKU,
                Size = dto.Size,
                Color = dto.Color,
                CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Variant added successfully.";
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= "View Variants";
            TempData["SuccessAddUrl"]   = Url.Action("StockIn", "Stock", new { variantId = variant.Id });
            TempData["SuccessAddLabel"] = "Record Stock for This Variant";
            TempData["SuccessAddHint"]  = "Next step: record the opening stock so this variant is ready for sales.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (variant == null)
                return NotFound();

            var dto = new ViewProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                ProductName = variant.Product.Name,
                SKU = variant.SKU,
                Size = variant.Size,
                Color = variant.Color,
                CostPrice = variant.CostPrice,
                SellingPrice = variant.SellingPrice
            };

            await LoadProductsDropDown();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(ViewProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadProductsDropDown();
                return View(dto);
            }

            var tenantId = _tenantProvider.GetTenantId();
            var skuExists = await _context.ProductVariants
                .AnyAsync(v => v.TenantId == tenantId && v.SKU == dto.SKU && v.Id != dto.Id);
            if (skuExists)
            {
                ModelState.AddModelError(nameof(dto.SKU), "This SKU already exists in your workspace.");
                await LoadProductsDropDown();
                return View(dto);
            }

            var variant = await _context.ProductVariants.FindAsync(dto.Id);

            if (variant == null)
                return NotFound();

            if (!await ProductExistsForTenantAsync(dto.ProductId))
            {
                ModelState.AddModelError(nameof(dto.ProductId), "Please select a valid active product in your workspace.");
                await LoadProductsDropDown();
                return View(dto);
            }

            variant.ProductId = dto.ProductId;
            variant.SKU = dto.SKU;
            variant.Size = dto.Size;
            variant.Color = dto.Color;
            variant.CostPrice = dto.CostPrice;
            variant.SellingPrice = dto.SellingPrice;

            _context.ProductVariants.Update(variant);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Variant updated.";
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= "View Variants";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Id == id)
                .Select(v => new ViewProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    CostPrice = v.CostPrice,
                    SellingPrice = v.SellingPrice
                })
                .FirstOrDefaultAsync();

            if (variant == null)
                return NotFound();

            await PopulateDeleteStateAsync(id);
            return View(variant);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Where(v => v.Id == id)
                .Select(v => new ViewProductVariantDto
                {
                    Id = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    CostPrice = v.CostPrice,
                    SellingPrice = v.SellingPrice
                })
                .FirstOrDefaultAsync();

            if (variant == null)
                return NotFound();

            return View(variant);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);

            if (variant == null)
                return NotFound();

            var deleteState = await GetDeleteStateAsync(id);
            if (!deleteState.CanDelete)
            {
                TempData["Error"] = deleteState.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = "Variant deleted.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= "View Variants";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadProductsDropDown()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "Id", "Name");
        }

        private async Task<bool> ProductExistsForTenantAsync(Guid productId)
        {
            var tenantId = _tenantProvider.GetTenantId();
            return await _context.Products
                .AnyAsync(p => p.Id == productId && p.TenantId == tenantId && p.IsActive);
        }

        private async Task PopulateDeleteStateAsync(Guid variantId)
        {
            var deleteState = await GetDeleteStateAsync(variantId);
            ViewBag.CanDelete = deleteState.CanDelete;
            ViewBag.DeleteMessage = deleteState.Message;
        }

        private async Task<(bool CanDelete, string? Message)> GetDeleteStateAsync(Guid variantId)
        {
            var hasSaleHistory = await _context.SaleItems
                .AnyAsync(i => i.ProductVariantId == variantId);
            if (hasSaleHistory)
            {
                return (false, "This variant cannot be deleted because it is used in sales history.");
            }

            var stockDelta = await _context.StockMovements
                .Where(m => m.ProductVariantId == variantId)
                .SumAsync(m => m.MovementType == "IN" ? m.Quantity : m.MovementType == "OUT" ? -m.Quantity : 0);
            if (stockDelta != 0)
            {
                return (false, "This variant cannot be deleted because it still has stock on hand.");
            }

            var hasStockHistory = await _context.StockMovements
                .AnyAsync(m => m.ProductVariantId == variantId);
            if (hasStockHistory)
            {
                return (false, "This variant cannot be deleted because it already has stock movement history.");
            }

            return (true, null);
        }
    }
}
