using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class PlanFeatureController : Controller
    {
        private readonly AppDbContext _db;

        public PlanFeatureController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(Guid? planId)
        {
            var plansQuery = _db.Plans.AsNoTracking().IgnoreQueryFilters()
                .Include(p => p.PlanFeatures).ThenInclude(pf => pf.Feature)
                .OrderBy(p => p.PriceMonthly);

            var plans = await plansQuery.ToListAsync();

            ViewBag.Plans = plans;
            ViewBag.SelectedPlanId = planId;

            if (planId.HasValue)
            {
                var filtered = plans.Where(p => p.Id == planId.Value).ToList();
                return View(filtered);
            }

            return View(plans);
        }

        public async Task<IActionResult> Create(Guid? planId)
        {
            await LoadDropDowns(planId);
            return View(new PlanFeatureCreateDto { PlanId = planId ?? Guid.Empty });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlanFeatureCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropDowns(dto.PlanId);
                return View(dto);
            }

            var exists = await _db.PlanFeatures
                .AnyAsync(pf => pf.PlanId == dto.PlanId && pf.FeatureId == dto.FeatureId);

            if (exists)
            {
                ModelState.AddModelError("", "This feature is already assigned to the selected plan.");
                await LoadDropDowns(dto.PlanId);
                return View(dto);
            }

            _db.PlanFeatures.Add(new PlanFeature
            {
                PlanId = dto.PlanId,
                FeatureId = dto.FeatureId,
                IsEnabled = dto.IsEnabled
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = "Feature assigned to plan.";
            TempData["SuccessListUrl"]  = Url.Action("Index", "PlanFeature", new { planId = dto.PlanId });
            TempData["SuccessListLabel"]= "View Plan Features";
            return RedirectToAction(nameof(Index), new { planId = dto.PlanId });
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var pf = await _db.PlanFeatures
                .Include(x => x.Plan)
                .Include(x => x.Feature)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (pf == null) return NotFound();
            return View(pf);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var pf = await _db.PlanFeatures.FirstOrDefaultAsync(x => x.Id == id);
            if (pf == null) return NotFound();
            var planId = pf.PlanId;
            _db.PlanFeatures.Remove(pf);
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = "Feature removed from plan.";
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "PlanFeature", new { planId });
            TempData["SuccessListLabel"]= "View Plan Features";
            return RedirectToAction(nameof(Index), new { planId });
        }

        private async Task LoadDropDowns(Guid? selectedPlanId)
        {
            var plans = await _db.Plans.AsNoTracking().IgnoreQueryFilters()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            var features = await _db.Features.AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, Text = f.Name + " (" + f.Code + ")" })
                .ToListAsync();

            ViewBag.Plans = new SelectList(plans, "Id", "Name", selectedPlanId);
            ViewBag.Features = new SelectList(features, "Id", "Text");
        }
    }
}
