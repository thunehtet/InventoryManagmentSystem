using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.ProductVariant;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class ProductVariantsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ProductVariantsController(AppDbContext context,ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<IActionResult> Index()
        {
            var variants = await _context.ProductVariants
                .Include(v => v.Product)
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

            return View(variants);
        }

        public async Task<IActionResult> Create()
        {
            await LoadProductsDropDown();
            return View(new CreateProductVariantDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductVariantDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadProductsDropDown();
                return View(dto);
            }
            var tenantId = _tenantProvider.GetTenantId();
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

        public async Task<IActionResult> Edit(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);

            if (variant == null)
                return NotFound();

            var dto = new CreateProductVariantDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
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
        public async Task<IActionResult> Edit(CreateProductVariantDto dto)
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
        public async Task<IActionResult> DeleteConfirmed(int id)
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