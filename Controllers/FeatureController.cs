using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class FeatureController : Controller
    {
        private readonly AppDbContext _db;

        public FeatureController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.Features.AsNoTracking()
                .Include(f => f.PlanFeatures)
                .OrderBy(f => f.Name);
            var total = await query.CountAsync();
            var features = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total, Action = nameof(Index)
            };
            return View(features);
        }

        public IActionResult Create() => View(new FeatureCreateDto());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FeatureCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            if (await _db.Features.AnyAsync(f => f.Code == dto.Code))
            {
                ModelState.AddModelError(nameof(dto.Code), "Feature code already exists.");
                return View(dto);
            }
            _db.Features.Add(new Feature
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            });
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = this.LocalizeShared("Feature '{0}' created.", dto.Name);
            TempData["SuccessListUrl"]  = Url.Action("Index", "Feature");
            TempData["SuccessListLabel"]= "View Features";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var f = await _db.Features.FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            return View(new FeatureEditDto
            {
                Id = f.Id, Code = f.Code, Name = f.Name,
                Description = f.Description, IsActive = f.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FeatureEditDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            if (await _db.Features.AnyAsync(f => f.Code == dto.Code && f.Id != dto.Id))
            {
                ModelState.AddModelError(nameof(dto.Code), "Feature code already exists.");
                return View(dto);
            }
            var f = await _db.Features.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (f == null) return NotFound();
            f.Code = dto.Code; f.Name = dto.Name;
            f.Description = dto.Description; f.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = this.LocalizeShared("Feature '{0}' updated.", f.Name);
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Feature");
            TempData["SuccessListLabel"]= "View Features";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var f = await _db.Features
                .Include(x => x.PlanFeatures)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            return View(f);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var f = await _db.Features
                .Include(x => x.PlanFeatures)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();
            if (f.PlanFeatures.Any())
            {
                TempData["Error"] = "Cannot delete: this feature is assigned to one or more plans. Remove plan assignments first.";
                return RedirectToAction(nameof(Index));
            }
            _db.Features.Remove(f);
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = this.LocalizeShared("Feature '{0}' deleted.", f.Name);
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Feature");
            TempData["SuccessListLabel"]= "View Features";
            return RedirectToAction(nameof(Index));
        }
    }
}
