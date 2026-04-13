using ClothInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Feature
{
    public class FeatureService : IFeatureService
    {
        private readonly AppDbContext _context;

        public FeatureService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasFeatureAsync(Guid tenantId, string featureCode)
        {
            // 1. Get active, non-expired subscription
            var today = DateTime.UtcNow;
            var subscription = await _context.TenantSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId && s.IsActive
                         && s.StartDate <= today && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            if (subscription == null)
                return false;

            // 2. Check plan features
            var hasFeature = await _context.PlanFeatures
                .Include(pf => pf.Feature)
                .AnyAsync(pf =>
                    pf.PlanId == subscription.PlanId &&
                    pf.Feature.Code == featureCode &&
                    pf.IsEnabled);

            // 3. Check tenant override (override plan)
            var overrideFeature = await _context.TenantFeatureOverrides
                .Include(o => o.Feature)
                .FirstOrDefaultAsync(o =>
                    o.TenantId == tenantId &&
                    o.Feature.Code == featureCode);

            if (overrideFeature != null)
                return overrideFeature.IsEnabled;

            return hasFeature;
        }
    }
}