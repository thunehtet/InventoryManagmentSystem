using ClothInventoryApp.Data;
using ClothInventoryApp.Dto;
using ClothInventoryApp.Dto.Product;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public ProductsController(AppDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public async Task<IActionResult> Index()
        {
            var tenantId = _tenantProvider.GetTenantId();
            var products = await _context.Products
                .Select(p => new ViewProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    Brand = p.Brand,
                    IsActive = p.IsActive
                }).Where(x => x.TenantId == tenantId)
                .ToListAsync();

            return View(products);
        }

        public IActionResult Create()
        {
            return View(new CreateProductDto { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var tenantId = _tenantProvider.GetTenantId();
            var product = new Product
            {
                Name = dto.Name,
                Category = dto.Category,
                Brand = dto.Brand,
                IsActive = dto.IsActive,
                TenantId=tenantId
               
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            var dto = new ViewProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                Brand = product.Brand,
                IsActive = product.IsActive
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ViewProductDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var product = await _context.Products.FindAsync(dto.Id);

            if (product == null)
                return NotFound();

            product.Name = dto.Name;
            product.Category = dto.Category;
            product.Brand = dto.Brand;
            product.IsActive = dto.IsActive;

            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var product = await _context.Products
                .Select(p => new ViewProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    Brand = p.Brand,
                    IsActive = p.IsActive
                })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}