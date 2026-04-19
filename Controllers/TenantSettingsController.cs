using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.TenantSettings;
using ClothInventoryApp.Models;
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

        public TenantSettingsController(AppDbContext context, ITenantProvider tenantProvider)
        {
            _context = context;
            _tenantProvider = tenantProvider;
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
            };

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
            settings.UpdatedAt               = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"]       = "Settings saved successfully.";
            TempData["SuccessListUrl"]   = Url.Action("Index", "TenantSettings");
            TempData["SuccessListLabel"] = "Back to Settings";

            return RedirectToAction("Index");
        }
    }
}
