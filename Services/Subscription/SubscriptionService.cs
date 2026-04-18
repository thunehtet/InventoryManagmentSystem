using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
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

        private async Task<Plan?> GetActivePlanAsync(Guid tenantId)
        {
            var today = DateTime.UtcNow;
            return await _context.TenantSubscriptions
                .Where(s => s.TenantId == tenantId && s.IsActive
                         && s.StartDate <= today && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .Select(s => s.Plan)
                .FirstOrDefaultAsync();
        }

        // ── Quota helpers ────────────────────────────────────────────

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
            var current = await _context.Products
                .Where(p => p.TenantId == tenantId)
                .CountAsync();
            return (current, plan?.MaxProducts);
        }

        public async Task<(int Current, int? Max)> GetVariantLimitAsync(Guid tenantId)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var current = await _context.ProductVariants
                .Where(v => v.TenantId == tenantId)
                .CountAsync();
            return (current, plan?.MaxVariants);
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

        public async Task<bool> CanAddVariantAsync(Guid tenantId)
        {
            var (current, max) = await GetVariantLimitAsync(tenantId);
            return max == null || current < max;
        }

        // ── Monthly sales limit ──────────────────────────────────────

        public async Task<(int Current, int? Max)> GetMonthlySaleUsageAsync(Guid tenantId)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd   = monthStart.AddMonths(1);
            var current = await _context.Sales
                .CountAsync(s => s.TenantId == tenantId
                              && s.SaleDate >= monthStart
                              && s.SaleDate < monthEnd);
            return (current, plan?.MaxMonthlySales);
        }

        public async Task<bool> CanCreateSaleAsync(Guid tenantId)
        {
            var (current, max) = await GetMonthlySaleUsageAsync(tenantId);
            return max == null || current < max;
        }

        // ── Per-feature monthly usage ────────────────────────────────

        private static int? GetPlanLimitForFeature(Plan plan, string feature) => feature switch
        {
            FeatureUsageKeys.PdfInvoice     => plan.MaxMonthlyPdfInvoices,
            FeatureUsageKeys.ReceiptShare   => plan.MaxMonthlyReceiptShares,
            FeatureUsageKeys.CustomerInvite => plan.MaxMonthlyCustomerInvites,
            _                               => null
        };

        public async Task<(int Used, int? Max)> GetFeatureUsageAsync(Guid tenantId, string feature)
        {
            var plan = await GetActivePlanAsync(tenantId);
            var max  = plan == null ? null : GetPlanLimitForFeature(plan, feature);

            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var row = await _context.TenantFeatureUsages
                .FirstOrDefaultAsync(x =>
                    x.TenantId  == tenantId &&
                    x.YearMonth == yearMonth &&
                    x.Feature   == feature);

            return (row?.UsageCount ?? 0, max);
        }

        public async Task<bool> CanUseFeatureAsync(Guid tenantId, string feature)
        {
            var (used, max) = await GetFeatureUsageAsync(tenantId, feature);
            return max == null || used < max;
        }

        public async Task IncrementFeatureUsageAsync(Guid tenantId, string feature)
        {
            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var row = await _context.TenantFeatureUsages
                .FirstOrDefaultAsync(x =>
                    x.TenantId  == tenantId &&
                    x.YearMonth == yearMonth &&
                    x.Feature   == feature);

            if (row == null)
            {
                _context.TenantFeatureUsages.Add(new TenantFeatureUsage
                {
                    TenantId   = tenantId,
                    YearMonth  = yearMonth,
                    Feature    = feature,
                    UsageCount = 1
                });
            }
            else
            {
                row.UsageCount++;
            }

            await _context.SaveChangesAsync();
        }

        public async Task ResetMonthlyFeatureUsageAsync(Guid tenantId)
        {
            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var features  = new[] { FeatureUsageKeys.PdfInvoice, FeatureUsageKeys.ReceiptShare, FeatureUsageKeys.CustomerInvite };

            foreach (var feature in features)
            {
                var row = await _context.TenantFeatureUsages
                    .FirstOrDefaultAsync(x =>
                        x.TenantId  == tenantId &&
                        x.YearMonth == yearMonth &&
                        x.Feature   == feature);

                if (row == null)
                    _context.TenantFeatureUsages.Add(new TenantFeatureUsage
                    {
                        TenantId   = tenantId,
                        YearMonth  = yearMonth,
                        Feature    = feature,
                        UsageCount = 0
                    });
                else
                    row.UsageCount = 0;
            }

            await _context.SaveChangesAsync();
        }
    }
}
