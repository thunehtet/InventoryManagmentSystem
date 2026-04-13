using ClothInventoryApp.Data;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClothInventoryApp.Controllers
{
    public abstract class TenantAwareController : Controller
    {
        protected readonly AppDbContext _context;
        protected readonly ITenantProvider _tenantProvider;

        protected TenantAwareController(AppDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            try
            {
                var tenantId = _tenantProvider.GetTenantId();
                var currency = _context.Tenants
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.CurrencyCode)
                    .FirstOrDefault();
                ViewData["Currency"] = currency ?? "";
            }
            catch
            {
                ViewData["Currency"] = "";
            }
        }
    }
}
