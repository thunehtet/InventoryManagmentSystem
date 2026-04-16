using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Customer;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    /// <summary>
    /// Publicly accessible endpoints — no login required.
    ///
    /// Security design:
    /// - AppDbContext query filters return nothing for unauthenticated requests
    ///   (CurrentTenantId falls back to Guid.Empty, matching no real tenant rows).
    /// - The Tenants table has no query filter, so tenant lookup by Code is safe.
    /// - EF Core does not apply query filters on INSERT — TenantId is set explicitly.
    /// - Rate limiting prevents registration spam.
    /// - No financial, inventory, or user data is accessible here.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("public-registration")]
    public class PublicController : Controller
    {
        private readonly AppDbContext _context;

        public PublicController(AppDbContext context)
        {
            _context = context;
        }

        // ─── Customer self-registration ──────────────────────────────

        /// GET /p/{tenantCode}/join
        /// The shop owner shares this URL. Anyone who opens it can register
        /// as a customer of that specific tenant — and nothing else.
        [HttpGet("/p/{tenantCode}/join")]
        public async Task<IActionResult> Register(string tenantCode)
        {
            // Normalise to prevent case-sensitivity issues
            tenantCode = tenantCode.Trim().ToUpperInvariant();

            var tenant = await GetActiveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            ViewBag.TenantName = tenant.Name;
            ViewBag.TenantLogoUrl = tenant.LogoUrl;
            ViewBag.TenantCode = tenantCode;
            return View(new CreateCustomerDto());
        }

        /// POST /p/{tenantCode}/join
        [HttpPost("/p/{tenantCode}/join")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string tenantCode, CreateCustomerDto dto)
        {
            tenantCode = tenantCode.Trim().ToUpperInvariant();

            // Always re-fetch tenant on POST — never trust client-side data for TenantId
            var tenant = await GetActiveTenantAsync(tenantCode);
            if (tenant == null) return NotFound();

            ViewBag.TenantName = tenant.Name;
            ViewBag.TenantLogoUrl = tenant.LogoUrl;
            ViewBag.TenantCode = tenantCode;

            if (!ModelState.IsValid)
                return View(dto);

            // TenantId is taken exclusively from the database lookup, never from user input
            _context.Customers.Add(new Customer
            {
                TenantId  = tenant.Id,      // ← always server-side, from DB
                Name      = dto.Name.Trim(),
                Phone     = dto.Phone?.Trim(),
                Email     = dto.Email?.Trim().ToLowerInvariant(),
                FacebookAccount = dto.FacebookAccount?.Trim(),
                Address   = dto.Address?.Trim(),
                Notes     = null,           // don't allow free-form notes from public form
                IsActive  = true,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RegisterSuccess), new { tenantCode });
        }

        [HttpGet("/c/{token}")]
        public async Task<IActionResult> RegisterByInvite(string token)
        {
            var invite = await GetActiveInviteAsync(token);
            if (invite == null)
                return NotFound();

            ViewBag.TenantName = invite.Tenant.Name;
            ViewBag.TenantLogoUrl = invite.Tenant.LogoUrl;
            ViewBag.InviteToken = invite.Token;
            ViewBag.InviteExpiresAt = invite.ExpiresAt;
            return View("Register", new CreateCustomerDto());
        }

        [HttpPost("/c/{token}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterByInvite(string token, CreateCustomerDto dto)
        {
            var invite = await GetActiveInviteAsync(token);
            if (invite == null)
                return NotFound();

            ViewBag.TenantName = invite.Tenant.Name;
            ViewBag.TenantLogoUrl = invite.Tenant.LogoUrl;
            ViewBag.InviteToken = invite.Token;
            ViewBag.InviteExpiresAt = invite.ExpiresAt;

            if (!ModelState.IsValid)
                return View("Register", dto);

            var customer = new Customer
            {
                TenantId = invite.TenantId,
                Name = dto.Name.Trim(),
                Phone = dto.Phone?.Trim(),
                Email = dto.Email?.Trim().ToLowerInvariant(),
                FacebookAccount = dto.FacebookAccount?.Trim(),
                Address = dto.Address?.Trim(),
                Notes = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Customers.Add(customer);
            invite.IsActive = false;
            invite.UsedAt = DateTime.UtcNow;
            invite.Customer = customer;

            await _context.SaveChangesAsync();

            ViewBag.TenantName = invite.Tenant.Name;
            ViewBag.TenantLogoUrl = invite.Tenant.LogoUrl;
            return View("RegisterSuccess");
        }

        /// GET /p/{tenantCode}/join/success
        [HttpGet("/p/{tenantCode}/join/success")]
        public async Task<IActionResult> RegisterSuccess(string tenantCode)
        {
            tenantCode = tenantCode.Trim().ToUpperInvariant();
            var tenant = await GetActiveTenantAsync(tenantCode);
            ViewBag.TenantName = tenant?.Name ?? tenantCode;
            ViewBag.TenantLogoUrl = tenant?.LogoUrl;
            return View();
        }

        [HttpGet("/r/{token}")]
        public async Task<IActionResult> Receipt(string token)
        {
            var sale = await _context.Sales
                .IgnoreQueryFilters()
                .Include(s => s.Tenant)
                .Include(s => s.Items)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(s => s.PublicReceiptToken == token);

            if (sale == null)
                return NotFound();

            var dto = new Dto.Sale.ViewSaleDto
            {
                Id = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                Discount = sale.Discount,
                Items = sale.Items.Select(i => new Dto.Sale.ViewSaleItemDto
                {
                    Id = i.Id,
                    ProductVariantId = i.ProductVariantId,
                    ProductVariantName = i.ProductVariant.Product.Name + " / " + i.ProductVariant.SKU,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    CostPrice = 0,
                    LineTotal = i.Quantity * i.UnitPrice,
                    LineProfit = 0
                }).ToList()
            };

            ViewBag.TenantName = sale.Tenant.Name;
            ViewBag.TenantLogoUrl = sale.Tenant.LogoUrl;
            ViewBag.TenantPhone = sale.Tenant.ContactPhone;
            ViewBag.TenantEmail = sale.Tenant.ContactEmail;
            ViewBag.Currency = sale.Tenant.CurrencyCode ?? string.Empty;
            ViewBag.ReceiptUrl = Url.Action(nameof(Receipt), "Public", new { token }, Request.Scheme);

            return View(dto);
        }

        // ─── Helpers ─────────────────────────────────────────────────

        /// Looks up a tenant by Code. The Tenants table has no query filter,
        /// so this is safe to call without an authenticated user.
        private Task<Tenant?> GetActiveTenantAsync(string code) =>
            _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code && t.IsActive);

        private Task<CustomerInviteLink?> GetActiveInviteAsync(string token) =>
            _context.CustomerInviteLinks
                .IgnoreQueryFilters()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x =>
                    x.Token == token &&
                    x.IsActive &&
                    x.UsedAt == null &&
                    x.ExpiresAt > DateTime.UtcNow &&
                    x.Tenant.IsActive);
    }
}
