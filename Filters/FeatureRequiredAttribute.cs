using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClothInventoryApp.Filters
{
    /// <summary>
    /// Redirects to the "upgrade your plan" page if the tenant's active subscription
    /// does not include the specified feature. SuperAdmin users bypass this check.
    /// Usage: [FeatureRequired("sales")]
    /// </summary>
    public class FeatureRequiredAttribute : TypeFilterAttribute
    {
        public FeatureRequiredAttribute(string featureCode)
            : base(typeof(FeatureRequiredFilter))
        {
            Arguments = new object[] { featureCode };
        }
    }

    public class FeatureRequiredFilter : IAsyncActionFilter
    {
        private readonly IFeatureService _featureService;
        private readonly ITenantProvider _tenantProvider;
        private readonly string _featureCode;

        public FeatureRequiredFilter(
            IFeatureService featureService,
            ITenantProvider tenantProvider,
            string featureCode)
        {
            _featureService = featureService;
            _tenantProvider = tenantProvider;
            _featureCode = featureCode;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            // SuperAdmin bypasses all feature checks
            if (context.HttpContext.User.IsInRole("SuperAdmin"))
            {
                await next();
                return;
            }

            Guid tenantId;
            try { tenantId = _tenantProvider.GetTenantId(); }
            catch { await next(); return; }

            var hasFeature = await _featureService.HasFeatureAsync(tenantId, _featureCode);
            if (!hasFeature)
            {
                context.Result = new RedirectToActionResult(
                    "FeatureRequired", "Account",
                    new { feature = _featureCode });
                return;
            }

            await next();
        }
    }
}
