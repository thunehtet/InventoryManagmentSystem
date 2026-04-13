using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class PlanController : Controller
    {
        private readonly AppDbContext _db;

        public PlanController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.Plans.AsNoTracking().IgnoreQueryFilters()
                .Include(p => p.TenantSubscriptions)
                .OrderBy(p => p.PriceMonthly);
            var total = await query.CountAsync();
            var plans = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total, Action = nameof(Index)
            };
            return View(plans);
        }

        public IActionResult Create() => View(new PlanCreateDto());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlanCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            if (await _db.Plans.IgnoreQueryFilters().AnyAsync(p => p.Code == dto.Code))
            {
                ModelState.AddModelError(nameof(dto.Code), "Plan code already exists.");
                return View(dto);
            }
            _db.Plans.Add(new Plan
            {
                Name = dto.Name, Code = dto.Code, Description = dto.Description,
                PriceMonthly = dto.PriceMonthly, PriceYearly = dto.PriceYearly,
                MaxUsers = dto.MaxUsers, MaxProducts = dto.MaxProducts,
                IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Plan '{dto.Name}' created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var p = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            return View(new PlanEditDto
            {
                Id = p.Id, Name = p.Name, Code = p.Code, Description = p.Description,
                PriceMonthly = p.PriceMonthly, PriceYearly = p.PriceYearly,
                MaxUsers = p.MaxUsers, MaxProducts = p.MaxProducts, IsActive = p.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PlanEditDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            if (await _db.Plans.IgnoreQueryFilters().AnyAsync(p => p.Code == dto.Code && p.Id != dto.Id))
            {
                ModelState.AddModelError(nameof(dto.Code), "Plan code already exists.");
                return View(dto);
            }
            var p = await _db.Plans.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (p == null) return NotFound();
            p.Name = dto.Name; p.Code = dto.Code; p.Description = dto.Description;
            p.PriceMonthly = dto.PriceMonthly; p.PriceYearly = dto.PriceYearly;
            p.MaxUsers = dto.MaxUsers; p.MaxProducts = dto.MaxProducts; p.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Plan '{p.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var p = await _db.Plans.IgnoreQueryFilters()
                .Include(x => x.TenantSubscriptions)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            return View(p);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var p = await _db.Plans.IgnoreQueryFilters()
                .Include(x => x.TenantSubscriptions)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            if (p.TenantSubscriptions.Any())
            {
                TempData["Error"] = "Cannot delete: subscriptions are linked to this plan.";
                return RedirectToAction(nameof(Index));
            }
            _db.Plans.Remove(p);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Plan '{p.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
