using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.ProductVariant;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Files;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Usage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class ProductVariantsController : TenantAwareController
    {
        private const int MaxImagesPerVariant = 12;
        private const int MaxImagesPerUpload = 5;

        private readonly ISubscriptionService _subscriptionService;
        private readonly IUsageTrackingService _usageTrackingService;
        private readonly IFileStorageService _fileStorageService;

        public ProductVariantsController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            ISubscriptionService subscriptionService,
            IUsageTrackingService usageTrackingService,
            IFileStorageService fileStorageService)
            : base(context, tenantProvider)
        {
            _subscriptionService = subscriptionService;
            _usageTrackingService = usageTrackingService;
            _fileStorageService = fileStorageService;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var tenantId = _tenantProvider.GetTenantId();
            var query = _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Images)
                .AsQueryable();

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
                    SellingPrice = v.SellingPrice,
                    Images = v.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .Select(i => new ProductVariantImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            SortOrder = i.SortOrder,
                            IsPrimary = i.IsPrimary
                        })
                        .ToList()
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.PlanLimitWarnings = await _subscriptionService.BuildPlanLimitWarningsAsync(tenantId);
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

            var sku = string.IsNullOrWhiteSpace(dto.SKU)
                ? await GenerateAutoSkuAsync(tenantId)
                : dto.SKU.Trim();

            var skuExists = await _context.ProductVariants
                .AnyAsync(v => v.TenantId == tenantId && v.SKU == sku);
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
                SKU = sku,
                Size = NormalizeVariantOption(dto.Size),
                Color = NormalizeVariantOption(dto.Color),
                CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice
            };

            _context.ProductVariants.Add(variant);
            await _context.SaveChangesAsync();

            try
            {
                await SaveVariantImagesAsync(variant.Id, tenantId, dto.Images);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            TempData["SuccessMsg"]      = this.LocalizeShared("Variant added successfully.");
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Variants");
            TempData["SuccessAddUrl"]   = Url.Action("StockIn", "Stock", new { variantId = variant.Id });
            TempData["SuccessAddLabel"] = this.LocalizeShared("Record Stock for This Variant");
            TempData["SuccessAddHint"]  = this.LocalizeShared("Next step: record the opening stock so this variant is ready for sales.");
            await _usageTrackingService.TrackActionAsync(tenantId, "variants", "create", "ProductVariant", variant.Id.ToString(), $"Created variant {variant.SKU}.", cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Images)
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
                SellingPrice = variant.SellingPrice,
                Images = MapImages(variant.Images)
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
            var sku = string.IsNullOrWhiteSpace(dto.SKU)
                ? await GenerateAutoSkuAsync(tenantId)
                : dto.SKU.Trim();

            var skuExists = await _context.ProductVariants
                .AnyAsync(v => v.TenantId == tenantId && v.SKU == sku && v.Id != dto.Id);
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
            variant.SKU = sku;
            variant.Size = NormalizeVariantOption(dto.Size);
            variant.Color = NormalizeVariantOption(dto.Color);
            variant.CostPrice = dto.CostPrice;
            variant.SellingPrice = dto.SellingPrice;

            _context.ProductVariants.Update(variant);
            await _context.SaveChangesAsync();

            try
            {
                await SaveVariantImagesAsync(variant.Id, tenantId, dto.NewImages);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }

            TempData["SuccessMsg"]      = this.LocalizeShared("Variant updated.");
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Variants");
            await _usageTrackingService.TrackActionAsync(variant.TenantId, "variants", "update", "ProductVariant", variant.Id.ToString(), $"Updated variant {variant.SKU}.", cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Images)
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
                    SellingPrice = v.SellingPrice,
                    Images = v.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .Select(i => new ProductVariantImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            SortOrder = i.SortOrder,
                            IsPrimary = i.IsPrimary
                        })
                        .ToList()
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

            TempData["SuccessMsg"]      = this.LocalizeShared("Variant deleted.");
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "ProductVariants");
            TempData["SuccessListLabel"]= this.LocalizeShared("View Variants");
            await _usageTrackingService.TrackActionAsync(variant.TenantId, "variants", "delete", "ProductVariant", variant.Id.ToString(), $"Deleted variant {variant.SKU}.", cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetPrimaryImage(Guid imageId)
        {
            var image = await _context.ProductVariantImages.FirstOrDefaultAsync(i => i.Id == imageId);
            if (image == null) return NotFound();

            var images = await _context.ProductVariantImages
                .Where(i => i.ProductVariantId == image.ProductVariantId)
                .ToListAsync();

            foreach (var item in images)
                item.IsPrimary = item.Id == imageId;

            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = this.LocalizeShared("Primary image updated.");
            return RedirectToAction(nameof(Edit), new { id = image.ProductVariantId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveImage(Guid imageId)
        {
            var image = await _context.ProductVariantImages.FirstOrDefaultAsync(i => i.Id == imageId);
            if (image == null) return NotFound();

            var variantId = image.ProductVariantId;
            var wasPrimary = image.IsPrimary;

            _context.ProductVariantImages.Remove(image);
            await _context.SaveChangesAsync();

            if (wasPrimary)
            {
                var next = await _context.ProductVariantImages
                    .Where(i => i.ProductVariantId == variantId)
                    .OrderBy(i => i.SortOrder)
                    .FirstOrDefaultAsync();
                if (next != null)
                {
                    next.IsPrimary = true;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMsg"] = this.LocalizeShared("Image removed.");
            return RedirectToAction(nameof(Edit), new { id = variantId });
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
            var stockDelta = await _context.StockMovements
                .Where(m => m.ProductVariantId == variantId)
                .SumAsync(m => m.MovementType == "IN" ? m.Quantity : m.MovementType == "OUT" ? -m.Quantity : 0);
            if (stockDelta != 0)
            {
                return (false, this.LocalizeShared("This variant cannot be deleted because it still has stock on hand."));
            }

            var hasStockHistory = await _context.StockMovements
                .AnyAsync(m => m.ProductVariantId == variantId);
            if (hasStockHistory)
            {
                return (false, this.LocalizeShared("This variant cannot be deleted because it already has stock movement history."));
            }

            return (true, null);
        }

        private async Task SaveVariantImagesAsync(Guid variantId, Guid tenantId, IEnumerable<IFormFile>? files)
        {
            var images = files?
                .Where(f => f != null && f.Length > 0)
                .ToList() ?? new List<IFormFile>();
            if (images.Count == 0) return;

            if (images.Count > MaxImagesPerUpload)
                throw new InvalidOperationException($"Upload up to {MaxImagesPerUpload} images at a time.");

            var existingCount = await _context.ProductVariantImages
                .CountAsync(i => i.ProductVariantId == variantId);
            if (existingCount + images.Count > MaxImagesPerVariant)
                throw new InvalidOperationException($"A variant can have up to {MaxImagesPerVariant} images.");

            var sortOrder = existingCount;
            var uploadedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var file in images)
            {
                var uploaded = await _fileStorageService.SaveImageAsync(
                    file,
                    UploadCategories.ProductImage,
                    uploadedBy,
                    tenantId,
                    HttpContext.RequestAborted);

                _context.ProductVariantImages.Add(new ProductVariantImage
                {
                    TenantId = tenantId,
                    ProductVariantId = variantId,
                    ImageUrl = _fileStorageService.GetPublicUrl(uploaded),
                    SortOrder = sortOrder,
                    IsPrimary = existingCount == 0 && sortOrder == 0
                });

                sortOrder++;
            }

            await _context.SaveChangesAsync();
        }

        private static List<ProductVariantImageDto> MapImages(IEnumerable<ProductVariantImage> images)
        {
            return images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => new ProductVariantImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    SortOrder = i.SortOrder,
                    IsPrimary = i.IsPrimary
                })
                .ToList();
        }

        private async Task<string> GenerateAutoSkuAsync(Guid tenantId)
        {
            var next = await _context.ProductVariants
                .IgnoreQueryFilters()
                .Where(v => v.TenantId == tenantId)
                .CountAsync() + 1;

            while (true)
            {
                var sku = $"AUTO-{next:D6}";
                var exists = await _context.ProductVariants
                    .IgnoreQueryFilters()
                    .AnyAsync(v => v.TenantId == tenantId && v.SKU == sku);

                if (!exists)
                    return sku;

                next++;
            }
        }

        private static string NormalizeVariantOption(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "Default" : trimmed;
        }
    }
}
