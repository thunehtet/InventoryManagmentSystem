using ClothInventoryApp.Data;
using ClothInventoryApp.Dto;
using ClothInventoryApp.Dto.Product;
using ClothInventoryApp.Models;
using ClothInventoryApp.Resources;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ProductsController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            ISubscriptionService subscriptionService,
            IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _subscriptionService = subscriptionService;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    p.Category.Contains(search) ||
                    p.Brand.Contains(search));

            var total = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(p => new ViewProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Category = p.Category,
                    Brand = p.Brand,
                    IsActive = p.IsActive
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(products);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new CreateProductDto { IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var tenantId = _tenantProvider.GetTenantId();

            // Enforce plan product limit
            if (!await _subscriptionService.CanAddProductAsync(tenantId))
            {
                var (current, max) = await _subscriptionService.GetProductLimitAsync(tenantId);
                TempData["LimitError"] = $"Product limit reached ({current}/{max}). Upgrade your plan to add more products.";
                return RedirectToAction(nameof(Index));
            }

            var product = new Product
            {
                Name = dto.Name,
                Category = dto.Category,
                Brand = dto.Brand,
                IsActive = dto.IsActive,
                TenantId = tenantId
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = this.LocalizeShared("Product '{0}' added to catalog.", product.Name);
            TempData["SuccessListUrl"]  = Url.Action("Index", "Products");
            TempData["SuccessListLabel"]= "View Products";
            TempData["SuccessAddUrl"]   = Url.Action("Create", "ProductVariants", new { productId = product.Id });
            TempData["SuccessAddLabel"] = "Add Variants for This Product";
            TempData["SuccessAddHint"]  = "Next step: add size, color, and price variants so this product can be stocked and sold.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
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

        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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

            TempData["SuccessMsg"]      = this.LocalizeShared("Product '{0}' updated.", product.Name);
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Products");
            TempData["SuccessListLabel"]= "View Products";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
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

            await PopulateDeleteStateAsync(id);
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            var deleteState = await GetDeleteStateAsync(id);
            if (!deleteState.CanDelete)
            {
                TempData["Error"] = deleteState.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]      = this.LocalizeShared("Product '{0}' deleted.", product.Name);
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Products");
            TempData["SuccessListLabel"]= "View Products";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDeleteStateAsync(Guid productId)
        {
            var deleteState = await GetDeleteStateAsync(productId);
            ViewBag.CanDelete = deleteState.CanDelete;
            ViewBag.DeleteMessage = deleteState.Message;
        }

        private async Task<(bool CanDelete, string? Message)> GetDeleteStateAsync(Guid productId)
        {
            var variantIds = await _context.ProductVariants
                .Where(v => v.ProductId == productId)
                .Select(v => v.Id)
                .ToListAsync();

            if (variantIds.Count == 0)
                return (true, null);

            var hasSaleHistory = await _context.SaleItems
                .AnyAsync(i => variantIds.Contains(i.ProductVariantId));
            if (hasSaleHistory)
            {
                return (false, _localizer["This product cannot be deleted because one or more variants are used in sales history. Mark the product inactive instead."]);
            }

            var stockDelta = await _context.StockMovements
                .Where(m => variantIds.Contains(m.ProductVariantId))
                .SumAsync(m => m.MovementType == "IN" ? m.Quantity : m.MovementType == "OUT" ? -m.Quantity : 0);
            if (stockDelta != 0)
            {
                return (false, _localizer["This product cannot be deleted because its variants still have stock on hand. Reduce stock to zero first and keep the history."]);
            }

            var hasStockHistory = await _context.StockMovements
                .AnyAsync(m => variantIds.Contains(m.ProductVariantId));
            if (hasStockHistory)
            {
                return (false, _localizer["This product cannot be deleted because its variants already have stock movement history. Mark the product inactive instead."]);
            }

            return (true, null);
        }
    }
}
