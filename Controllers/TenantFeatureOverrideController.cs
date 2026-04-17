using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class TenantFeatureOverrideController : Controller
    {
        private readonly AppDbContext _db;

        public TenantFeatureOverrideController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(t => t.FeatureOverrides)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(keyword) ||
                    t.Code.ToLower().Contains(keyword) ||
                    (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(keyword)));
            }

            var total = await query.CountAsync();
            var tenants = await query
                .OrderBy(t => t.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(t => new TenantFeatureOverrideIndexItemDto
                {
                    TenantId = t.Id,
                    TenantName = t.Name,
                    TenantCode = t.Code,
                    ContactEmail = t.ContactEmail,
                    OverrideCount = t.FeatureOverrides.Count,
                    IsActive = t.IsActive,
                    ActivePlanName = t.Subscriptions
                        .Where(s => s.IsActive)
                        .OrderByDescending(s => s.EndDate)
                        .Select(s => s.Plan.Name)
                        .FirstOrDefault() ?? "No Plan"
                })
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page,
                PageSize = size,
                TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };

            return View(tenants);
        }

        public async Task<IActionResult> Manage(Guid tenantId)
        {
            var vm = await BuildManageDtoAsync(tenantId);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(TenantFeatureOverrideManageDto dto)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == dto.TenantId);
            if (tenant == null) return NotFound();

            var validFeatureIds = await _db.Features
                .Where(f => f.IsActive)
                .Select(f => f.Id)
                .ToHashSetAsync();

            var existingOverrides = await _db.TenantFeatureOverrides
                .Where(x => x.TenantId == dto.TenantId)
                .ToDictionaryAsync(x => x.FeatureId);

            foreach (var item in dto.Items.Where(i => validFeatureIds.Contains(i.FeatureId)))
            {
                var mode = NormalizeMode(item.OverrideMode);
                existingOverrides.TryGetValue(item.FeatureId, out var existing);

                if (mode == "Default")
                {
                    if (existing != null)
                        _db.TenantFeatureOverrides.Remove(existing);
                    continue;
                }

                var shouldEnable = mode == "Enabled";
                if (existing == null)
                {
                    _db.TenantFeatureOverrides.Add(new TenantFeatureOverride
                    {
                        TenantId = dto.TenantId,
                        FeatureId = item.FeatureId,
                        IsEnabled = shouldEnable
                    });
                }
                else
                {
                    existing.IsEnabled = shouldEnable;
                }
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = this.LocalizeShared("Feature overrides updated for {0}.", tenant.Name);
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "TenantFeatureOverride");
            TempData["SuccessListLabel"]= "View Overrides";
            return RedirectToAction(nameof(Manage), new { tenantId = dto.TenantId });
        }

        private async Task<TenantFeatureOverrideManageDto?> BuildManageDtoAsync(Guid tenantId)
        {
            var tenant = await _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            if (tenant == null) return null;

            var today = DateTime.UtcNow;
            var activeSubscription = await _db.TenantSubscriptions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId && s.IsActive && s.StartDate <= today && s.EndDate >= today)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefaultAsync();

            var activePlanId = activeSubscription?.PlanId;
            var planFeatureIds = activePlanId.HasValue
                ? await _db.PlanFeatures
                    .AsNoTracking()
                    .Where(pf => pf.PlanId == activePlanId.Value && pf.IsEnabled)
                    .Select(pf => pf.FeatureId)
                    .ToHashSetAsync()
                : new HashSet<Guid>();

            var overrides = await _db.TenantFeatureOverrides
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .ToDictionaryAsync(x => x.FeatureId);

            var features = await _db.Features
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name)
                .ToListAsync();

            return new TenantFeatureOverrideManageDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TenantCode = tenant.Code,
                ActivePlanName = activeSubscription?.Plan.Name ?? "No Plan",
                Items = features.Select(f =>
                {
                    var hasPlanFeature = planFeatureIds.Contains(f.Id);
                    overrides.TryGetValue(f.Id, out var existingOverride);
                    var mode = existingOverride == null ? "Default" : existingOverride.IsEnabled ? "Enabled" : "Disabled";

                    return new TenantFeatureOverrideItemDto
                    {
                        FeatureId = f.Id,
                        FeatureName = f.Name,
                        FeatureCode = f.Code,
                        Description = f.Description,
                        PlanEnabled = hasPlanFeature,
                        EffectiveEnabled = existingOverride?.IsEnabled ?? hasPlanFeature,
                        OverrideMode = mode
                    };
                }).ToList()
            };
        }

        private static string NormalizeMode(string? mode)
        {
            if (string.Equals(mode, "Enabled", StringComparison.OrdinalIgnoreCase))
                return "Enabled";
            if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
                return "Disabled";
            return "Default";
        }
    }
}
