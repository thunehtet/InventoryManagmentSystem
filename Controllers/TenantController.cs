using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.Tenant;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    
    public class TenantController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TenantController(AppDbContext context, UserManager<ApplicationUser> userManager  )
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.Tenants
                .OrderBy(x => x.Name)
                .ToListAsync();

            return View(items);
        }

        public IActionResult Create()
        {
            return View(new TenantCreateDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TenantCreateDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var exists = await _context.Tenants.AnyAsync(x => x.Code == dto.Code);
            if (exists)
            {
                ModelState.AddModelError(nameof(dto.Code), "Tenant code already exists.");
                return View(dto);
            }

            // Create tenant first
            var item = new Tenant
            {
                Id = Guid.NewGuid(),
                Code = dto.Code,
                Name = dto.Name,
                LogoUrl = dto.LogoUrl,
                ContactEmail = dto.ContactEmail,
                ContactPhone = dto.ContactPhone,
                Country = dto.Country,
                CurrencyCode = dto.CurrencyCode,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.Add(item);
            await _context.SaveChangesAsync();

            // Generate random username
            var username = await GenerateUniqueUsernameAsync(dto.Code);

            // You may also generate a temporary password
            var temporaryPassword = GenerateTemporaryPassword();

            var appUser = new ApplicationUser
            {
                UserName = username,
                Email = dto.ContactEmail,
                FullName = dto.Name,
                TenantId = item.Id, // use newly created tenant id
                IsTenantAdmin = true,
                IsSuperAdmin = false,
                IsActive = dto.IsActive,
                Type = "Admin",
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(appUser, temporaryPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                // optional: rollback tenant if user creation fails
                _context.Tenants.Remove(item);
                await _context.SaveChangesAsync();

                return View(dto);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GenerateUniqueUsernameAsync(string tenantCode)
        {
            string username;
            bool exists;

            do
            {
                var suffix = Guid.NewGuid().ToString("N")[..6];
                username = $"{tenantCode.ToLower()}_admin_{suffix}";
                exists = await _userManager.FindByNameAsync(username) != null;
            }
            while (exists);

            return username;
        }

        private string GenerateTemporaryPassword()
        {
            // simple strong temporary password
            return $"Mandalay@2026";
        }
        public async Task<IActionResult> Edit(Guid id)
        {
            var item = await _context.Tenants.FindAsync(id);
            if (item == null) return NotFound();

            var dto = new TenantEditDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                LogoUrl = item.LogoUrl,
                ContactEmail = item.ContactEmail,
                ContactPhone = item.ContactPhone,
                Country = item.Country,
                CurrencyCode = item.CurrencyCode,
                IsActive = item.IsActive
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TenantEditDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var item = await _context.Tenants.FindAsync(dto.Id);
            if (item == null) return NotFound();

            var duplicate = await _context.Tenants
                .AnyAsync(x => x.Code == dto.Code && x.Id != dto.Id);

            if (duplicate)
            {
                ModelState.AddModelError(nameof(dto.Code), "Tenant code already exists.");
                return View(dto);
            }

            item.Code = dto.Code;
            item.Name = dto.Name;
            item.LogoUrl = dto.LogoUrl;
            item.ContactEmail = dto.ContactEmail;
            item.ContactPhone = dto.ContactPhone;
            item.Country = dto.Country;
            item.CurrencyCode = dto.CurrencyCode;
            item.IsActive = dto.IsActive;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var item = await _context.Tenants
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null) return NotFound();

            return View(item);
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var item = await _context.Tenants
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null) return NotFound();

            return View(item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.Tenants
                .Include(x => x.Users)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null) return NotFound();

            if (item.Users.Any())
            {
                TempData["Error"] = "Cannot delete tenant because users are linked to it.";
                return RedirectToAction(nameof(Index));
            }

            _context.Tenants.Remove(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}