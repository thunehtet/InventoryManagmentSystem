using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionController : Controller
    {
        private readonly AppDbContext _db;

        public SubscriptionController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.TenantSubscriptions.AsNoTracking().IgnoreQueryFilters()
                .Include(s => s.Tenant).Include(s => s.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(sub =>
                    sub.Tenant.Name.ToLower().Contains(s) ||
                    sub.Plan.Name.ToLower().Contains(s) ||
                    sub.BillingCycle.ToLower().Contains(s));
            }
            var total = await query.CountAsync();
            var subs = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
            ViewBag.Search = search;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index),
                Extra = new() { ["search"] = search }
            };
            return View(subs);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDropDowns();
            ViewBag.ActiveSubByTenant = await _db.TenantSubscriptions
                .AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.IsActive)
                .Include(s => s.Plan)
                .ToDictionaryAsync(s => s.TenantId, s => s.Plan.Name);
            return View(new SubscriptionCreateDto());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriptionCreateDto dto)
        {
            if (!ModelState.IsValid) { await LoadDropDowns(); return View(dto); }

            var existing = await _db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == dto.TenantId && s.IsActive)
                .ToListAsync();

            string? replacedMsg = null;
            if (existing.Any())
            {
                var now = DateTime.UtcNow;
                foreach (var s in existing)
                {
                    // Load navigations needed for the archive snapshot
                    await _db.Entry(s).Reference(x => x.Plan).LoadAsync();
                    await _db.Entry(s).Reference(x => x.Tenant).LoadAsync();

                    _db.PastSubscriptions.Add(
                        SubscriptionExpiryService.BuildArchiveRecord(s, now, "Replaced",
                            $"Replaced by new subscription created on {now:yyyy-MM-dd}."));

                    s.IsActive = false;
                    s.UpdatedAt = now;
                }
                replacedMsg = this.LocalizeShared("{0} previous subscription(s) archived and deactivated.", existing.Count);
            }

            _db.TenantSubscriptions.Add(new TenantSubscription
            {
                TenantId = dto.TenantId, PlanId = dto.PlanId,
                StartDate = dto.StartDate, EndDate = dto.EndDate,
                BillingCycle = dto.BillingCycle, Price = dto.Price,
                IsTrial = dto.IsTrial, IsActive = dto.IsActive,
                Notes = dto.Notes, CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["SuccessMsg"]      = replacedMsg == null
                ? "Subscription created."
                : this.LocalizeShared("Subscription created. Note: {0}", replacedMsg);
            TempData["SuccessListUrl"]  = Url.Action("Index", "Subscription");
            TempData["SuccessListLabel"]= "View Subscriptions";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var s = await _db.TenantSubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            await LoadDropDowns();
            return View(new SubscriptionEditDto
            {
                Id = s.Id, TenantId = s.TenantId, PlanId = s.PlanId,
                StartDate = s.StartDate, EndDate = s.EndDate,
                BillingCycle = s.BillingCycle, Price = s.Price,
                IsTrial = s.IsTrial, IsActive = s.IsActive, Notes = s.Notes
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriptionEditDto dto)
        {
            if (!ModelState.IsValid) { await LoadDropDowns(); return View(dto); }
            var s = await _db.TenantSubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (s == null) return NotFound();

            var editNow = DateTime.UtcNow;

            if (dto.IsActive)
            {
                // Deactivate (and archive) any conflicting active subs for this tenant
                var conflicts = await _db.TenantSubscriptions
                    .IgnoreQueryFilters()
                    .Include(x => x.Plan)
                    .Include(x => x.Tenant)
                    .Where(x => x.TenantId == dto.TenantId && x.IsActive && x.Id != dto.Id)
                    .ToListAsync();

                foreach (var c in conflicts)
                {
                    _db.PastSubscriptions.Add(
                        SubscriptionExpiryService.BuildArchiveRecord(c, editNow, "Replaced",
                            $"Replaced during edit of subscription {dto.Id} on {editNow:yyyy-MM-dd}."));
                    c.IsActive = false;
                    c.UpdatedAt = editNow;
                }
            }
            else if (s.IsActive && !dto.IsActive)
            {
                // Admin manually deactivating — archive with reason "Cancelled"
                await _db.Entry(s).Reference(x => x.Plan).LoadAsync();
                await _db.Entry(s).Reference(x => x.Tenant).LoadAsync();
                _db.PastSubscriptions.Add(
                    SubscriptionExpiryService.BuildArchiveRecord(s, editNow, "Cancelled",
                        $"Manually cancelled via admin UI on {editNow:yyyy-MM-dd}."));
            }

            s.TenantId = dto.TenantId; s.PlanId = dto.PlanId;
            s.StartDate = dto.StartDate; s.EndDate = dto.EndDate;
            s.BillingCycle = dto.BillingCycle; s.Price = dto.Price;
            s.IsTrial = dto.IsTrial; s.IsActive = dto.IsActive;
            s.Notes = dto.Notes; s.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = "Subscription updated.";
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Subscription");
            TempData["SuccessListLabel"]= "View Subscriptions";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var s = await _db.TenantSubscriptions.IgnoreQueryFilters()
                .Include(x => x.Tenant).Include(x => x.Plan)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            return View(s);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var s = await _db.TenantSubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            _db.TenantSubscriptions.Remove(s);
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = "Subscription deleted.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Subscription");
            TempData["SuccessListLabel"]= "View Subscriptions";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> History(string? search, string? tenantId, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.PastSubscriptions
                .AsNoTracking()
                .Include(p => p.Tenant)
                .Include(p => p.Plan)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.TenantName.ToLower().Contains(s) ||
                    p.PlanName.ToLower().Contains(s)   ||
                    p.Reason.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var tid))
                query = query.Where(p => p.TenantId == tid);

            var total = await query.CountAsync();
            var records = await query
                .OrderByDescending(p => p.ArchivedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            ViewBag.Search   = search;
            ViewBag.TenantId = tenantId;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(History),
                Extra = new() { ["search"] = search, ["tenantId"] = tenantId }
            };
            return View(records);
        }

        private async Task LoadDropDowns()
        {
            var tenants = await _db.Tenants.IgnoreQueryFilters()
                .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
            var plans = await _db.Plans.IgnoreQueryFilters()
                .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

            ViewBag.Tenants = new SelectList(tenants, "Id", "Name");
            ViewBag.Plans   = new SelectList(plans, "Id", "Name");
            ViewBag.BillingCycles = new List<SelectListItem>
            {
                new() { Value = "Monthly", Text = "Monthly" },
                new() { Value = "Yearly",  Text = "Yearly"  }
            };
        }
    }
}
