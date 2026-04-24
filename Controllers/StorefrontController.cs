using System.Data;
using System.Security.Cryptography;
using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Stock;
using ClothInventoryApp.Services.Storefront;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    /// <summary>
    /// Publicly accessible storefront — no login required.
    ///
    /// Security design:
    /// - AppDbContext query filters return nothing for unauthenticated requests, so every
    ///   tenant-scoped query uses IgnoreQueryFilters + explicit TenantId filtering.
    /// - Tenant lookup is always done server-side from the URL's tenantCode; nothing from
    ///   the client is ever trusted for tenant identity.
    /// - The 'storefront' feature flag AND the tenant's StorefrontEnabled toggle are both
    ///   enforced before any storefront page renders.
    /// - Stock is decremented at checkout via a serializable transaction to prevent overselling.
    /// - Cart is session-backed and keyed by tenant code so carts don't leak across tenants.
    /// </summary>
    [AllowAnonymous]
    public class StorefrontController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IFeatureService _featureService;
        private readonly IStockService _stockService;
        private readonly ICartService _cartService;

        public StorefrontController(
            AppDbContext db,
            IFeatureService featureService,
            IStockService stockService,
            ICartService cartService)
        {
            _db = db;
            _featureService = featureService;
            _stockService = stockService;
            _cartService = cartService;
        }

        // ─── Landing / home ──────────────────────────────────────────
        [HttpGet("/shop/{tenantCode}")]
        public async Task<IActionResult> Index(string tenantCode)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var featuredProducts = await _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenant.Id && p.IsActive)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .OrderBy(p => p.Name)
                .Take(8)
                .ToListAsync();

            var variantIds = featuredProducts.SelectMany(p => p.Variants).Select(v => v.Id).ToList();
            var stockMap = await _stockService.GetCurrentStockMapAsync(tenant.Id, variantIds);

            var dto = featuredProducts.Select(p => BuildProductCard(p, stockMap)).ToList();
            return View(dto);
        }

        [HttpGet("/shop/{tenantCode}/products")]
        public async Task<IActionResult> Catalog(string tenantCode, string? q, string? category, int page = 1)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            const int pageSize = 24;
            page = Math.Max(1, page);

            var query = _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenant.Id && p.IsActive)
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
                query = query.Where(p => p.Category == category);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                query = query.Where(p =>
                    p.Name.Contains(kw) ||
                    p.Brand.Contains(kw) ||
                    p.Category.Contains(kw));
            }

            var total = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var variantIds = products.SelectMany(p => p.Variants).Select(v => v.Id).ToList();
            var stockMap = await _stockService.GetCurrentStockMapAsync(tenant.Id, variantIds);

            var categories = await _db.Products
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenant.Id && p.IsActive && !string.IsNullOrWhiteSpace(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Search = q;
            ViewBag.Category = category;
            ViewBag.Categories = categories;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = total;

            var dto = products.Select(p => BuildProductCard(p, stockMap)).ToList();
            return View(dto);
        }

        [HttpGet("/shop/{tenantCode}/p/{productId:guid}")]
        public async Task<IActionResult> Product(string tenantCode, Guid productId)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var product = await _db.Products
                .IgnoreQueryFilters()
                .Include(p => p.Variants)
                    .ThenInclude(v => v.Images)
                .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenant.Id && p.IsActive);

            if (product == null) return NotFound();

            var stockMap = await _stockService.GetCurrentStockMapAsync(
                tenant.Id,
                product.Variants.Select(v => v.Id));

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            var cartQuantityMap = cart.Lines
                .GroupBy(x => x.ProductVariantId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var gallery = product.Variants
                .SelectMany(v => v.Images)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .Distinct()
                .ToList();

            if (gallery.Count == 0 && !string.IsNullOrWhiteSpace(product.ImageUrl))
                gallery.Add(product.ImageUrl);

            var dto = new Dto.Storefront.ProductDetailDto
            {
                Id = product.Id,
                Name = product.Name,
                Brand = product.Brand,
                Category = product.Category,
                Description = product.Description,
                ImageUrl = gallery.FirstOrDefault() ?? product.ImageUrl,
                GalleryImageUrls = gallery,
                Variants = product.Variants.Select(v => new Dto.Storefront.ProductVariantCardDto
                {
                    Id = v.Id,
                    SKU = v.SKU,
                    Size = v.Size,
                    Color = v.Color,
                    SellingPrice = v.SellingPrice,
                    Stock = stockMap.TryGetValue(v.Id, out var s) ? s : 0,
                    QuantityInCart = cartQuantityMap.TryGetValue(v.Id, out var qty) ? qty : 0,
                    PrimaryImageUrl = GetVariantPrimaryImage(v) ?? product.ImageUrl,
                    ImageUrls = v.Images
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenBy(i => i.SortOrder)
                        .Select(i => i.ImageUrl)
                        .ToList()
                }).ToList()
            };

            return View(dto);
        }

        // ─── Cart ────────────────────────────────────────────────────
        [HttpGet("/shop/{tenantCode}/cart")]
        public async Task<IActionResult> Cart(string tenantCode)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            var dto = await BuildCartViewAsync(tenant.Id, setting, cart);
            return View(dto);
        }

        [HttpPost("/shop/{tenantCode}/cart/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string tenantCode, Guid variantId, int quantity = 1)
        {
            var (tenant, _) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            if (quantity < 1) quantity = 1;
            if (quantity > 99) quantity = 99;

            var variant = await _db.ProductVariants
                .IgnoreQueryFilters()
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenant.Id && v.Product.IsActive);

            if (variant == null) return NotFound();

            var stock = await _stockService.GetCurrentStockAsync(tenant.Id, variantId);
            if (stock <= 0)
            {
                TempData["Error"] = "That item is out of stock.";
                return RedirectToAction(nameof(Product), new { tenantCode, productId = variant.ProductId });
            }

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            var line = cart.Lines.FirstOrDefault(x => x.ProductVariantId == variantId);
            if (line == null)
            {
                cart.Lines.Add(new CartLine { ProductVariantId = variantId, Quantity = Math.Min(quantity, stock) });
            }
            else
            {
                line.Quantity = Math.Min(line.Quantity + quantity, stock);
            }
            _cartService.SaveCart(HttpContext.Session, tenantCode, cart);

            TempData["Success"] = $"Added to cart: {variant.Product.Name}";
            return RedirectToAction(nameof(Product), new { tenantCode, productId = variant.ProductId });
        }

        [HttpPost("/shop/{tenantCode}/cart/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCart(string tenantCode, Guid variantId, int quantity)
        {
            var (tenant, _) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            var line = cart.Lines.FirstOrDefault(x => x.ProductVariantId == variantId);
            if (line != null)
            {
                if (quantity <= 0)
                {
                    cart.Lines.Remove(line);
                }
                else
                {
                    var stock = await _stockService.GetCurrentStockAsync(tenant.Id, variantId);
                    line.Quantity = Math.Min(quantity, Math.Max(stock, 0));
                    if (line.Quantity <= 0) cart.Lines.Remove(line);
                }
                _cartService.SaveCart(HttpContext.Session, tenantCode, cart);
            }

            return RedirectToAction(nameof(Cart), new { tenantCode });
        }

        [HttpPost("/shop/{tenantCode}/cart/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(string tenantCode, Guid variantId)
        {
            var (tenant, _) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            cart.Lines.RemoveAll(x => x.ProductVariantId == variantId);
            _cartService.SaveCart(HttpContext.Session, tenantCode, cart);

            return RedirectToAction(nameof(Cart), new { tenantCode });
        }

        // ─── Checkout ────────────────────────────────────────────────
        [HttpGet("/shop/{tenantCode}/checkout")]
        public async Task<IActionResult> Checkout(string tenantCode)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            if (cart.Lines.Count == 0)
                return RedirectToAction(nameof(Cart), new { tenantCode });

            var dto = await BuildCartViewAsync(tenant.Id, setting, cart);
            if (dto.Lines.Count == 0)
                return RedirectToAction(nameof(Cart), new { tenantCode });

            ViewBag.CartView = dto;
            return View(new Dto.Storefront.CheckoutDto());
        }

        [HttpPost("/shop/{tenantCode}/checkout")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("storefront-checkout")]
        public async Task<IActionResult> Checkout(string tenantCode, Dto.Storefront.CheckoutDto dto)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var cart = _cartService.GetCart(HttpContext.Session, tenantCode);
            if (cart.Lines.Count == 0)
                return RedirectToAction(nameof(Cart), new { tenantCode });

            var cartView = await BuildCartViewAsync(tenant.Id, setting, cart);
            ViewBag.CartView = cartView;

            if (!ModelState.IsValid)
                return View(dto);

            if (cartView.Lines.Count == 0)
            {
                ModelState.AddModelError("", "Your cart is empty.");
                return View(dto);
            }

            if (cartView.Lines.Any(l => l.Quantity > l.Stock))
            {
                ModelState.AddModelError("", "One or more items exceed the available stock. Please review your cart.");
                return View(dto);
            }

            using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                // Re-check stock inside the transaction to prevent overselling
                foreach (var line in cartView.Lines)
                {
                    if (!await _stockService.CanApplyMovementAsync(tenant.Id, line.VariantId, "OUT", line.Quantity))
                        throw new InvalidOperationException($"Not enough stock for '{line.ProductName}'.");
                }

                // Resolve/create Customer (idempotent on phone within tenant)
                Customer? customer = null;
                var trimmedPhone = dto.CustomerPhone.Trim();
                if (!string.IsNullOrEmpty(trimmedPhone))
                {
                    customer = await _db.Customers
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.Phone == trimmedPhone);
                }

                if (customer == null)
                {
                    customer = new Customer
                    {
                        TenantId = tenant.Id,
                        Name = dto.CustomerName.Trim(),
                        Phone = trimmedPhone,
                        Email = dto.CustomerEmail?.Trim().ToLowerInvariant(),
                        Address = dto.ShippingAddress.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Customers.Add(customer);
                    await _db.SaveChangesAsync();
                }

                var shippingFee = Math.Max(0, setting?.StorefrontShippingFee ?? 0);
                var subtotal = cartView.Lines.Sum(l => l.Quantity * l.UnitPrice);
                var total = subtotal + shippingFee;

                var orderNumber = await GenerateOrderNumberAsync(tenant.Id, setting);

                var order = new ShopOrder
                {
                    TenantId = tenant.Id,
                    OrderNumber = orderNumber,
                    PublicToken = GeneratePublicToken(),
                    CustomerId = customer.Id,
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerPhone = trimmedPhone,
                    CustomerEmail = dto.CustomerEmail?.Trim().ToLowerInvariant(),
                    ShippingAddress = dto.ShippingAddress.Trim(),
                    CustomerNotes = dto.CustomerNotes?.Trim(),
                    Subtotal = subtotal,
                    Discount = 0,
                    ShippingFee = shippingFee,
                    TotalAmount = total,
                    Status = "Pending",
                    PaymentStatus = "Unpaid",
                    CreatedAt = DateTime.UtcNow,
                    Items = cartView.Lines.Select(l => new ShopOrderItem
                    {
                        TenantId = tenant.Id,
                        ProductVariantId = l.VariantId,
                        ProductNameSnapshot = l.ProductName,
                        ProductSkuSnapshot = l.Sku,
                        ProductColorSnapshot = l.Color,
                        ProductSizeSnapshot = l.Size,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        LineTotal = l.Quantity * l.UnitPrice
                    }).ToList()
                };

                _db.ShopOrders.Add(order);
                await _db.SaveChangesAsync();

                // Decrement stock (reserve) so the item can't be oversold while the order is pending
                foreach (var line in order.Items)
                {
                    if (line.ProductVariantId == null) continue;
                    _db.StockMovements.Add(new StockMovement
                    {
                        TenantId = tenant.Id,
                        ProductVariantId = line.ProductVariantId.Value,
                        MovementType = "OUT",
                        Quantity = line.Quantity,
                        MovementDate = DateTime.UtcNow,
                        Remarks = $"Storefront order {order.OrderNumber}"
                    });
                }
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                _cartService.ClearCart(HttpContext.Session, tenantCode);

                return RedirectToAction(nameof(OrderStatus), new { tenantCode, token = order.PublicToken });
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
            catch
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "We couldn't place your order. Please try again.");
                return View(dto);
            }
        }

        [HttpGet("/shop/{tenantCode}/order/{token}")]
        public async Task<IActionResult> OrderStatus(string tenantCode, string token)
        {
            var (tenant, setting) = await ResolveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            await PopulateTenantViewDataAsync(tenant, setting);

            var order = await _db.ShopOrders
                .IgnoreQueryFilters()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.TenantId == tenant.Id && o.PublicToken == token);

            if (order == null) return NotFound();

            return View(order);
        }

        // ─── Helpers ─────────────────────────────────────────────────
        private async Task<(Tenant? tenant, TenantSetting? setting)> ResolveTenantAsync(string tenantCode)
        {
            if (string.IsNullOrWhiteSpace(tenantCode)) return (null, null);
            var code = tenantCode.Trim().ToUpperInvariant();

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code && t.IsActive);

            if (tenant == null) return (null, null);

            // Enforce storefront feature flag
            var hasFeature = await _featureService.HasFeatureAsync(tenant.Id, "storefront");
            if (!hasFeature) return (null, null);

            var setting = await _db.TenantSettings
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenant.Id);

            // The tenant admin must explicitly enable the storefront before it becomes public
            if (setting == null || !setting.StorefrontEnabled)
                return (null, null);

            return (tenant, setting);
        }

        private async Task PopulateTenantViewDataAsync(Tenant tenant, TenantSetting? setting)
        {
            ViewBag.TenantId = tenant.Id;
            ViewBag.TenantCode = tenant.Code;
            ViewBag.TenantName = tenant.Name;
            ViewBag.TenantLogoUrl = tenant.LogoUrl;
            ViewBag.TenantPhone = tenant.ContactPhone;
            ViewBag.TenantEmail = tenant.ContactEmail;
            ViewBag.Currency = tenant.CurrencyCode ?? string.Empty;
            ViewBag.Tagline = setting?.StorefrontTagline;
            ViewBag.Description = setting?.StorefrontDescription;
            ViewBag.ShippingFee = setting?.StorefrontShippingFee ?? 0;

            var cart = _cartService.GetCart(HttpContext.Session, tenant.Code);
            ViewBag.CartQuantity = cart.TotalQuantity;
            await Task.CompletedTask;
        }

        private async Task<Dto.Storefront.CartViewDto> BuildCartViewAsync(
            Guid tenantId,
            TenantSetting? setting,
            Cart cart)
        {
            if (cart.Lines.Count == 0)
                return new Dto.Storefront.CartViewDto { ShippingFee = setting?.StorefrontShippingFee ?? 0 };

            var variantIds = cart.Lines.Select(x => x.ProductVariantId).ToList();

            var variants = await _db.ProductVariants
                .IgnoreQueryFilters()
                .Include(v => v.Product)
                .Include(v => v.Images)
                .Where(v => variantIds.Contains(v.Id) && v.TenantId == tenantId && v.Product.IsActive)
                .ToListAsync();

            var stockMap = await _stockService.GetCurrentStockMapAsync(tenantId, variants.Select(v => v.Id));

            var lines = new List<Dto.Storefront.CartLineDto>();
            foreach (var cl in cart.Lines)
            {
                var v = variants.FirstOrDefault(x => x.Id == cl.ProductVariantId);
                if (v == null) continue;

                var stock = stockMap.TryGetValue(v.Id, out var s) ? s : 0;

                lines.Add(new Dto.Storefront.CartLineDto
                {
                    VariantId = v.Id,
                    ProductId = v.ProductId,
                    ProductName = v.Product.Name,
                    Sku = v.SKU,
                    Color = v.Color,
                    Size = v.Size,
                    ImageUrl = GetVariantPrimaryImage(v) ?? v.Product.ImageUrl,
                    UnitPrice = v.SellingPrice,
                    Quantity = cl.Quantity,
                    Stock = stock
                });
            }

            var shippingFee = Math.Max(0, setting?.StorefrontShippingFee ?? 0);

            return new Dto.Storefront.CartViewDto
            {
                Lines = lines,
                Subtotal = lines.Sum(l => l.Quantity * l.UnitPrice),
                ShippingFee = shippingFee
            };
        }

        private Dto.Storefront.ProductCardDto BuildProductCard(Product p, Dictionary<Guid, int> stockMap)
        {
            var activeVariants = p.Variants.ToList();
            var totalStock = activeVariants.Sum(v => stockMap.TryGetValue(v.Id, out var s) ? s : 0);
            var minPrice = activeVariants.Count > 0 ? activeVariants.Min(v => v.SellingPrice) : 0;
            var maxPrice = activeVariants.Count > 0 ? activeVariants.Max(v => v.SellingPrice) : 0;

            return new Dto.Storefront.ProductCardDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Category = p.Category,
                ImageUrl = GetProductPrimaryImage(p),
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                TotalStock = totalStock,
                VariantCount = activeVariants.Count
            };
        }

        private async Task<string> GenerateOrderNumberAsync(Guid tenantId, TenantSetting? setting)
        {
            var prefix = !string.IsNullOrWhiteSpace(setting?.OrderPrefix)
                ? setting!.OrderPrefix!.Trim().ToUpperInvariant()
                : "ORD";

            var datePart = DateTime.UtcNow.ToString("yyMMdd");
            var basePrefix = $"{prefix}-{datePart}-";

            var todayCount = await _db.ShopOrders
                .IgnoreQueryFilters()
                .CountAsync(x => x.TenantId == tenantId && x.OrderNumber.StartsWith(basePrefix));

            return $"{basePrefix}{(todayCount + 1):D4}";
        }

        private static string GeneratePublicToken()
            => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        private static string? GetProductPrimaryImage(Product product)
        {
            return product.Variants
                .SelectMany(v => v.Images)
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefault() ?? product.ImageUrl;
        }

        private static string? GetVariantPrimaryImage(ProductVariant variant)
        {
            return variant.Images
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefault();
        }
    }
}
