using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.Tenant;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class TenantController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TenantController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var query = _db.Tenants.AsNoTracking().IgnoreQueryFilters();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(s) ||
                    t.Code.ToLower().Contains(s) ||
                    (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(s)));
            }
            var total = await query.CountAsync();
            var tenants = await query
                .OrderByDescending(t => t.CreatedAt)
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
            return View(tenants);
        }

        public IActionResult Create() => View(new TenantCreateDto());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TenantCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == dto.Code))
            {
                ModelState.AddModelError(nameof(dto.Code), "Tenant code already exists.");
                return View(dto);
            }

            var tenant = new Tenant
            {
                Code = dto.Code, Name = dto.Name, LogoUrl = dto.LogoUrl,
                ContactEmail = dto.ContactEmail, ContactPhone = dto.ContactPhone,
                Country = dto.Country, CurrencyCode = dto.CurrencyCode,
                IsActive = dto.IsActive, CreatedAt = DateTime.UtcNow
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            var tempPassword = "Mandalay@2026";
            var appUser = new ApplicationUser
            {
                UserName = dto.ContactEmail, Email = dto.ContactEmail,
                FullName = dto.Name, TenantId = tenant.Id,
                IsTenantAdmin = true, IsSuperAdmin = false,
                IsActive = dto.IsActive, Type = "Admin",
                CreatedAt = DateTime.UtcNow, MustChangePassword = true
            };
            var result = await _userManager.CreateAsync(appUser, tempPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                _db.Tenants.Remove(tenant);
                await _db.SaveChangesAsync();
                return View(dto);
            }

            TempData["Success"] = $"Tenant '{tenant.Name}' created. Admin login: {dto.ContactEmail} / {tempPassword}";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            return View(new TenantEditDto
            {
                Id = t.Id, Code = t.Code, Name = t.Name, LogoUrl = t.LogoUrl,
                ContactEmail = t.ContactEmail, ContactPhone = t.ContactPhone,
                Country = t.Country, CurrencyCode = t.CurrencyCode, IsActive = t.IsActive
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TenantEditDto dto)
        {
            if (!ModelState.IsValid) return View(dto);
            if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Code == dto.Code && t.Id != dto.Id))
            {
                ModelState.AddModelError(nameof(dto.Code), "Tenant code already exists.");
                return View(dto);
            }
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (t == null) return NotFound();

            t.Code = dto.Code; t.Name = dto.Name; t.LogoUrl = dto.LogoUrl;
            t.ContactEmail = dto.ContactEmail; t.ContactPhone = dto.ContactPhone;
            t.Country = dto.Country; t.CurrencyCode = dto.CurrencyCode;
            t.IsActive = dto.IsActive; t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Tenant '{t.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters()
                .Include(x => x.Users)
                .Include(x => x.Subscriptions).ThenInclude(s => s.Plan)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            return View(t);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters()
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            return View(t);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters()
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            if (t.Users.Any())
            {
                TempData["Error"] = "Cannot delete: users are linked to this tenant. Deactivate it instead.";
                return RedirectToAction(nameof(Index));
            }
            _db.Tenants.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Tenant '{t.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            t.IsActive = !t.IsActive; t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["Success"] = t.IsActive ? $"{t.Name} activated." : $"{t.Name} deactivated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
