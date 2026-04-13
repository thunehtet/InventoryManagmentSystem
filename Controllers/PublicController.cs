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

        /// GET /p/{tenantCode}/join/success
        [HttpGet("/p/{tenantCode}/join/success")]
        public async Task<IActionResult> RegisterSuccess(string tenantCode)
        {
            tenantCode = tenantCode.Trim().ToUpperInvariant();
            var tenant = await GetActiveTenantAsync(tenantCode);
            ViewBag.TenantName = tenant?.Name ?? tenantCode;
            return View();
        }

        // ─── Helpers ─────────────────────────────────────────────────

        /// Looks up a tenant by Code. The Tenants table has no query filter,
        /// so this is safe to call without an authenticated user.
        private Task<Tenant?> GetActiveTenantAsync(string code) =>
            _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code && t.IsActive);
    }
}
