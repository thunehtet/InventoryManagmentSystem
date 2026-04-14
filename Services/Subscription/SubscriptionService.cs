using ClothInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Subscription
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;

        public SubscriptionService(AppDbContext context) => _context = context;

        public async Task<bool> IsSubscriptionActiveAsync(Guid tenantId)
        {
            var today = DateTime.UtcNow;
            return await _context.TenantSubscriptions
                .AnyAsync(s =>
                    s.TenantId == tenantId &&
                    s.IsActive &&
                    s.StartDate <= today &&
                    s.EndDate >= today);
        }

        private async Task<Models.Plan?> GetActivePlanAsync(Guid tenantId)
        {
            var today = DateTime.UtcNow;
            return await _context.TenantSubscriptions
                .Where(s => s.TenantId == tenantId && s.IsActive
                         && s.StartDate <= today && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .Select(s => s.Plan)
                .FirstOrDefaultAsync();
        }

        public async Task<(int Current, int? Max)> GetUserLimitAsync(Guid tenantId)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var current = await _context.Users
                .CountAsync(u => u.TenantId == tenantId && !u.IsSuperAdmin);
            return (current, plan?.MaxUsers);
        }

        public async Task<(int Current, int? Max)> GetProductLimitAsync(Guid tenantId)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var current = await _context.Products.CountAsync();  // query filter scopes to tenant
            return (current, plan?.MaxProducts);
        }

        public async Task<bool> CanAddUserAsync(Guid tenantId)
        {
            var (current, max) = await GetUserLimitAsync(tenantId);
            return max == null || current < max;
        }

        public async Task<bool> CanAddProductAsync(Guid tenantId)
        {
            var (current, max) = await GetProductLimitAsync(tenantId);
            return max == null || current < max;
        }

        public async Task<(int Current, int? Max)> GetVariantLimitAsync(Guid tenantId)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var current = await _context.ProductVariants.CountAsync(); // query filter scopes to tenant
            return (current, plan?.MaxVariants);
        }

        public async Task<bool> CanAddVariantAsync(Guid tenantId)
        {
            var (current, max) = await GetVariantLimitAsync(tenantId);
            return max == null || current < max;
        }
    }
}
