using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.ApplicationUser;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ApplicationUserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly ISubscriptionService _subscriptionService;

        public ApplicationUserController(
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            ITenantProvider tenantProvider,
            ISubscriptionService subscriptionService)
        {
            _userManager = userManager;
            _context = context;
            _tenantProvider = tenantProvider;
            _subscriptionService = subscriptionService;
        }

        // ── Helpers ──────────────────────────────────────────────────
        private Guid CurrentTenantId() => _tenantProvider.GetTenantId();

        private bool IsSuperAdmin() =>
            User.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "SuperAdmin");

        // ── Index ────────────────────────────────────────────────────
        public async Task<IActionResult> Index(int page = 1, int size = 10)
        {
            size = PaginationViewModel.Clamp(size);
            var tenantId = CurrentTenantId();

            var query = _context.Users
                .Include(u => u.Tenant)
                .Where(u => u.TenantId == tenantId);

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.IsTenantAdmin)
                .ThenBy(u => u.FullName)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var (current, max) = await _subscriptionService.GetUserLimitAsync(tenantId);
            ViewBag.CurrentUserId  = _userManager.GetUserId(User);
            ViewBag.UserCount      = current;
            ViewBag.UserMax        = max;
            ViewBag.CanAddUser     = max == null || current < max;
            ViewBag.Pagination = new PaginationViewModel
            {
                Page = page, PageSize = size, TotalCount = total,
                Action = nameof(Index)
            };
            return View(users);
        }

        // ── Details ──────────────────────────────────────────────────
        public async Task<IActionResult> Details(string id)
        {
            var tenantId = CurrentTenantId();
            var user = await _context.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

            if (user == null) return NotFound();
            return View(user);
        }

        // ── Create ───────────────────────────────────────────────────
        public IActionResult Create()
        {
            var dto = new ApplicationUserCreateDto
            {
                TenantId = CurrentTenantId(),
                IsActive = true
            };
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ApplicationUserCreateDto dto)
        {
            // Always force tenant to the current admin's tenant
            dto.TenantId = CurrentTenantId();
            // Tenant admins cannot create super-admins
            dto.IsSuperAdmin = false;

            // Remove fields that are set server-side, not from the form
            ModelState.Remove(nameof(dto.TenantId));
            ModelState.Remove(nameof(dto.UserName));

            if (!ModelState.IsValid)
                return View(dto);

            // Enforce plan user limit
            if (!await _subscriptionService.CanAddUserAsync(dto.TenantId))
            {
                var (current, max) = await _subscriptionService.GetUserLimitAsync(dto.TenantId);
                ModelState.AddModelError(string.Empty,
                    $"User limit reached ({current}/{max}). Upgrade your plan to add more users.");
                return View(dto);
            }

            var user = new ApplicationUser
            {
                UserName  = dto.Email,   // use email as username
                Email     = dto.Email,
                FullName  = dto.FullName,
                TenantId  = dto.TenantId,
                IsTenantAdmin = dto.IsTenantAdmin,
                IsSuperAdmin  = false,
                IsActive      = dto.IsActive,
                Type          = dto.IsTenantAdmin ? "Admin" : "Staff",
                CreatedAt     = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(dto);
            }

            TempData["Success"] = $"User {dto.Email} created. Default password has been set.";
            return RedirectToAction(nameof(Index));
        }

        // ── Edit ─────────────────────────────────────────────────────
        public async Task<IActionResult> Edit(string id)
        {
            var tenantId = CurrentTenantId();

            // Prevent editing own account from user management
            if (id == _userManager.GetUserId(User))
                return RedirectToAction(nameof(Index));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

            if (user == null) return NotFound();

            var dto = new ApplicationUserEditDto
            {
                Id            = user.Id,
                UserName      = user.UserName      ?? string.Empty,
                Email         = user.Email         ?? string.Empty,
                FullName      = user.FullName,
                TenantId      = user.TenantId,
                IsTenantAdmin = user.IsTenantAdmin,
                IsSuperAdmin  = false,
                IsActive      = user.IsActive
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUserEditDto dto)
        {
            var tenantId = CurrentTenantId();

            // Validate ownership
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == dto.Id && u.TenantId == tenantId);

            if (user == null) return NotFound();

            ModelState.Remove(nameof(dto.NewPassword));

            if (!ModelState.IsValid)
                return View(dto);

            user.FullName      = dto.FullName;
            user.Email         = dto.Email;
            user.UserName      = dto.Email;   // keep email = username
            user.IsTenantAdmin = dto.IsTenantAdmin;
            user.IsActive      = dto.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
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
                    return View(dto);
                }
            }

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── Delete ───────────────────────────────────────────────────
        public async Task<IActionResult> Delete(string id)
        {
            if (id == _userManager.GetUserId(User))
                return RedirectToAction(nameof(Index));

            var tenantId = CurrentTenantId();
            var user = await _context.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var tenantId = CurrentTenantId();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

            if (user == null) return NotFound();

            // Prevent deleting yourself
            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.DeleteAsync(user);
            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
