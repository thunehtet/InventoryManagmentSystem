using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.Tenant;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Email;
using ClothInventoryApp.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class TenantController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITemporaryCredentialService _temporaryCredentialService;
        private readonly IEmailService _emailService;
        private readonly ILogger<TenantController> _logger;

        public TenantController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            ITemporaryCredentialService temporaryCredentialService,
            IEmailService emailService,
            ILogger<TenantController> logger)
        {
            _db = db;
            _userManager = userManager;
            _temporaryCredentialService = temporaryCredentialService;
            _emailService = emailService;
            _logger = logger;
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
                Code = dto.Code,
                Name = dto.Name,
                BusinessType = dto.BusinessType,
                LogoUrl = dto.LogoUrl,
                ContactEmail = dto.ContactEmail,
                ContactPhone = dto.ContactPhone,
                Country = dto.Country,
                CurrencyCode = dto.CurrencyCode,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            var tempPassword = _temporaryCredentialService.GenerateTemporaryPassword();
            var username = dto.ContactEmail;
            var appUser = new ApplicationUser
            {
                UserName = username,
                Email = dto.ContactEmail,
                FullName = dto.Name,
                TenantId = tenant.Id,
                IsTenantAdmin = true,
                IsSuperAdmin = false,
                IsActive = dto.IsActive,
                Type = "Admin",
                CreatedAt = DateTime.UtcNow,
                MustChangePassword = true
            };
            var result = await _userManager.CreateAsync(appUser, tempPassword);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                _db.Tenants.Remove(tenant);
                await _db.SaveChangesAsync();
                return View(dto);
            }

            if (!string.IsNullOrWhiteSpace(dto.ContactEmail))
            {
                try
                {
                    await _emailService.SendAsync(
                        dto.ContactEmail,
                        "Your StockEasy admin account is ready",
                        $"""
                        <p>Hello {WebUtility.HtmlEncode(dto.Name)},</p>
                        <p>Your StockEasy admin account has been created.</p>
                        <p><strong>Username:</strong> {WebUtility.HtmlEncode(username)}</p>
                        <p><strong>Temporary password:</strong> {WebUtility.HtmlEncode(tempPassword)}</p>
                        <p>Please sign in and change your password immediately.</p>
                        """,
                        HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send tenant admin credentials for tenant {TenantCode}.", dto.Code);
                    TempData["Warning"] = "Tenant created, but the credential email could not be sent.";
                }
            }

            TempData["SuccessMsg"]      = this.LocalizeShared("Tenant '{0}' created. Admin login: {1} / {2}", tenant.Name, username ?? string.Empty, tempPassword);
            TempData["SuccessListUrl"]  = Url.Action("Index", "Tenant");
            TempData["SuccessListLabel"]= "View Tenants";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            return View(new TenantEditDto
            {
                Id = t.Id, Code = t.Code, Name = t.Name, LogoUrl = t.LogoUrl,
                BusinessType = t.BusinessType,
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

            t.Code = dto.Code; t.Name = dto.Name; t.BusinessType = dto.BusinessType; t.LogoUrl = dto.LogoUrl;
            t.ContactEmail = dto.ContactEmail; t.ContactPhone = dto.ContactPhone;
            t.Country = dto.Country; t.CurrencyCode = dto.CurrencyCode;
            t.IsActive = dto.IsActive; t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["SuccessMsg"]      = this.LocalizeShared("Tenant '{0}' updated.", t.Name);
            TempData["SuccessType"]     = "update";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Tenant");
            TempData["SuccessListLabel"]= "View Tenants";
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
            TempData["SuccessMsg"]      = this.LocalizeShared("Tenant '{0}' deleted.", t.Name);
            TempData["SuccessType"]     = "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Tenant");
            TempData["SuccessListLabel"]= "View Tenants";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(Guid id)
        {
            var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound();
            t.IsActive = !t.IsActive; t.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            TempData["SuccessMsg"]      = t.IsActive
                ? this.LocalizeShared("{0} activated.", t.Name)
                : this.LocalizeShared("{0} deactivated.", t.Name);
            TempData["SuccessType"]     = t.IsActive ? "update" : "delete";
            TempData["SuccessListUrl"]  = Url.Action("Index", "Tenant");
            TempData["SuccessListLabel"]= "View Tenants";
            return RedirectToAction(nameof(Index));
        }
    }
}
