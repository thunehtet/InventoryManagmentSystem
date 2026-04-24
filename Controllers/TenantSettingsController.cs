using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.TenantSettings;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TenantSettingsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly IFeatureService _featureService;

        public TenantSettingsController(AppDbContext context, ITenantProvider tenantProvider, IFeatureService featureService)
        {
            _context = context;
            _tenantProvider = tenantProvider;
            _featureService = featureService;
        }

        public async Task<IActionResult> Index()
        {
            var tenantId = _tenantProvider.GetTenantId();
            var settings = await _context.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings == null)
            {
                settings = new TenantSetting { TenantId = tenantId };
                _context.TenantSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            var dto = new TenantSettingsEditDto
            {
                LowStockThreshold      = settings.LowStockThreshold,
                LowStockAlertEnabled   = settings.LowStockAlertEnabled,
                StaffCanSeeDashboard   = settings.StaffCanSeeDashboard,
                StaffCanSeeFinance     = settings.StaffCanSeeFinance,
                StaffCanSeeProducts    = settings.StaffCanSeeProducts,
                StaffCanSeeVariants    = settings.StaffCanSeeVariants,
                StaffCanSeeTextiles    = settings.StaffCanSeeTextiles,
                StaffCanSeeStockMovement = settings.StaffCanSeeStockMovement,
                StaffCanSeeInventory   = settings.StaffCanSeeInventory,
                StaffCanSeeSales       = settings.StaffCanSeeSales,
                StaffCanSeeCustomers   = settings.StaffCanSeeCustomers,
                StorefrontEnabled      = settings.StorefrontEnabled,
                StorefrontTagline      = settings.StorefrontTagline,
                StorefrontDescription  = settings.StorefrontDescription,
                StorefrontShippingFee  = settings.StorefrontShippingFee
            };

            var tenant = await _context.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId);
            var hasEcommerceFeature = await _featureService.HasFeatureAsync(tenantId, "storefront");
            ViewBag.HasEcommerceFeature = hasEcommerceFeature;
            ViewBag.ShopUrl = tenant == null || !hasEcommerceFeature || !settings.StorefrontEnabled
                ? null
                : $"/shop/{tenant.Code}";

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(TenantSettingsEditDto dto)
        {
            if (!ModelState.IsValid)
                return View("Index", dto);

            var tenantId = _tenantProvider.GetTenantId();
            var settings = await _context.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId);
            var hasEcommerceFeature = await _featureService.HasFeatureAsync(tenantId, "storefront");

            if (settings == null)
            {
                settings = new TenantSetting { TenantId = tenantId };
                _context.TenantSettings.Add(settings);
            }

            settings.LowStockThreshold       = dto.LowStockThreshold;
            settings.LowStockAlertEnabled    = dto.LowStockAlertEnabled;
            settings.StaffCanSeeDashboard    = dto.StaffCanSeeDashboard;
            settings.StaffCanSeeFinance      = dto.StaffCanSeeFinance;
            settings.StaffCanSeeProducts     = dto.StaffCanSeeProducts;
            settings.StaffCanSeeVariants     = dto.StaffCanSeeVariants;
            settings.StaffCanSeeTextiles     = dto.StaffCanSeeTextiles;
            settings.StaffCanSeeStockMovement = dto.StaffCanSeeStockMovement;
            settings.StaffCanSeeInventory    = dto.StaffCanSeeInventory;
            settings.StaffCanSeeSales        = dto.StaffCanSeeSales;
            settings.StaffCanSeeCustomers    = dto.StaffCanSeeCustomers;
            settings.StorefrontEnabled       = hasEcommerceFeature && dto.StorefrontEnabled;
            settings.StorefrontTagline       = string.IsNullOrWhiteSpace(dto.StorefrontTagline) ? null : dto.StorefrontTagline.Trim();
            settings.StorefrontDescription   = string.IsNullOrWhiteSpace(dto.StorefrontDescription) ? null : dto.StorefrontDescription.Trim();
            settings.StorefrontShippingFee   = dto.StorefrontShippingFee;
            settings.UpdatedAt               = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]       = hasEcommerceFeature || !dto.StorefrontEnabled
                ? this.LocalizeShared("Settings saved successfully.")
                : this.LocalizeShared("Settings saved. E-commerce was not enabled because it is not included in your active plan.");
            TempData["SuccessListUrl"]   = Url.Action("Index", "TenantSettings");
            TempData["SuccessListLabel"] = this.LocalizeShared("Back to Settings");

            return RedirectToAction("Index");
        }
    }
}
