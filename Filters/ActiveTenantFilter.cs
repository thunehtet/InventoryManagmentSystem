using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ClothInventoryApp.Filters
{
    /// <summary>
    /// Checks on every authenticated request whether the tenant is still active.
    /// Signs the user out immediately and redirects to login if the tenant has been deactivated.
    /// SuperAdmin users are exempt — they are not tenant-scoped.
    /// Result is cached per tenant for 2 minutes to avoid a DB hit on every request.
    /// </summary>
    public class ActiveTenantFilter : IAsyncActionFilter
    {
        private readonly AppDbContext _db;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IMemoryCache _cache;

        public ActiveTenantFilter(
            AppDbContext db,
            SignInManager<ApplicationUser> signInManager,
            IMemoryCache cache)
        {
            _db = db;
            _signInManager = signInManager;
            _cache = cache;
        }

        public static string CacheKey(Guid tenantId) => $"tenant_active_{tenantId}";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;

            if (user.Identity?.IsAuthenticated != true ||
                user.IsInRole("SuperAdmin"))
            {
                await next();
                return;
            }

            var tenantIdClaim = user.FindFirstValue("TenantId");
            if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                await next();
                return;
            }

            if (!_cache.TryGetValue(CacheKey(tenantId), out bool isActive))
            {
                isActive = await _db.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.IsActive)
                    .FirstOrDefaultAsync();

                _cache.Set(CacheKey(tenantId), isActive, TimeSpan.FromMinutes(2));
            }

            if (!isActive)
            {
                await _signInManager.SignOutAsync();
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { area = "", message = "suspended" });
                return;
            }

            await next();
        }
    }
}
