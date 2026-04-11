using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.ApplicationUser;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize]
    public class ApplicationUserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public ApplicationUserController(
            UserManager<ApplicationUser> userManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Include(x => x.Tenant)
                .OrderBy(x => x.FullName)
                .ToListAsync();

            return View(users);
        }

        public async Task<IActionResult> Create()
        {
            var dto = new ApplicationUserCreateDto();
            await LoadTenants(dto.Tenants);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApplicationUserCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadTenants(dto.Tenants);
                return View(dto);
            }

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                FullName = dto.FullName,
                TenantId = dto.TenantId,
                IsTenantAdmin = dto.IsTenantAdmin,
                IsSuperAdmin = dto.IsSuperAdmin,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await LoadTenants(dto.Tenants);
                return View(dto);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null) return NotFound();

            var dto = new ApplicationUserEditDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                TenantId = user.TenantId,
                IsTenantAdmin = user.IsTenantAdmin,
                IsSuperAdmin = user.IsSuperAdmin,
                IsActive = user.IsActive
            };

            await LoadTenants(dto.Tenants);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUserEditDto dto)
        {
            if (!ModelState.IsValid)
            {
                await LoadTenants(dto.Tenants);
                return View(dto);
            }

            var user = await _userManager.FindByIdAsync(dto.Id);
            if (user == null) return NotFound();

            user.FullName = dto.FullName;
            user.UserName = dto.UserName;
            user.Email = dto.Email;
            user.TenantId = dto.TenantId;
            user.IsTenantAdmin = dto.IsTenantAdmin;
            user.IsSuperAdmin = dto.IsSuperAdmin;
            user.IsActive = dto.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await LoadTenants(dto.Tenants);
                return View(dto);
            }

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);

                    await LoadTenants(dto.Tenants);
                    return View(dto);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _context.Users
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        public async Task<IActionResult> Delete(string id)
        {
            var user = await _context.Users
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadTenants(List<SelectListItem> target)
        {
            var tenants = await _context.Tenants
                .OrderBy(x => x.Name)
                .ToListAsync();

            target.Clear();
            target.AddRange(tenants.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            }));
        }
    }
}