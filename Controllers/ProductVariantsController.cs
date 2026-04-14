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
        public async Task<IActionResult> Create()
        {
            await LoadProductsDropDown();
            return View(new CreateProductVariantDto());
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

            var variant = await _context.ProductVariants.FindAsync(dto.Id);

            if (variant == null)
                return NotFound();

            variant.ProductId = dto.ProductId;
            variant.SKU = dto.SKU;
            variant.Size = dto.Size;
            variant.Color = dto.Color;
            variant.CostPrice = dto.CostPrice;
            variant.SellingPrice = dto.SellingPrice;

            _context.ProductVariants.Update(variant);
            await _context.SaveChangesAsync();

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

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadProductsDropDown()
        {
            var products = await _context.Products
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "Id", "Name");
        }
    }
}