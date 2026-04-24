using ClothInventoryApp.Dto.Subscription;
using ClothInventoryApp.Services.Subscription;

namespace ClothInventoryApp.Controllers
{
    public static class PlanLimitWarningExtensions
    {
        public static async Task<List<PlanLimitWarningDto>> BuildPlanLimitWarningsAsync(
            this ISubscriptionService subscriptionService,
            Guid tenantId)
        {
            var warnings = new List<PlanLimitWarningDto>();

            var (productsCurrent, productsMax) = await subscriptionService.GetProductLimitAsync(tenantId);
            AddWarning(warnings, "products", productsCurrent, productsMax,
                "Your current plan allows {0} products, but your workspace has {1}. You can keep using existing products, but cannot add more until you upgrade or reduce products.");

            var (variantsCurrent, variantsMax) = await subscriptionService.GetVariantLimitAsync(tenantId);
            AddWarning(warnings, "variants", variantsCurrent, variantsMax,
                "Your current plan allows {0} variants, but your workspace has {1}. You can keep selling existing variants, but cannot add more until you upgrade or reduce variants.");

            var (salesCurrent, salesMax) = await subscriptionService.GetMonthlySaleUsageAsync(tenantId);
            AddWarning(warnings, "sales this month", salesCurrent, salesMax,
                "Your current plan allows {0} sales this month, and you already have {1}. New sales are blocked until next month or plan upgrade.");

            return warnings;
        }

        private static void AddWarning(
            List<PlanLimitWarningDto> warnings,
            string resourceName,
            int current,
            int? max,
            string messageTemplate)
        {
            if (!max.HasValue || current <= max.Value)
                return;

            warnings.Add(new PlanLimitWarningDto
            {
                ResourceName = resourceName,
                Current = current,
                Max = max.Value,
                Message = string.Format(messageTemplate, max.Value, current)
            });
        }
    }
}
