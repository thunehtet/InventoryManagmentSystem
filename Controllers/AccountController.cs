using ClothInventoryApp.Data;
using ClothInventoryApp.Dtos.Account;
using ClothInventoryApp.Models;
using ClothInventoryApp.Services.Email;
using ClothInventoryApp.Services.Files;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Encodings.Web;

namespace ClothInventoryApp.Controllers
{
    public class AccountController : Controller
    {
        private static readonly TimeSpan StandardSessionLifetime = TimeSpan.FromHours(12);
        private static readonly TimeSpan RememberMeSessionLifetime = TimeSpan.FromDays(30);

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _db;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            AppDbContext db,
            IFileStorageService fileStorageService,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailService = emailService;
            _db = db;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null, string? message = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (message == "suspended")
                ViewBag.Error = "Your account has been suspended. Please contact support.";
            return View(new LoginDto());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(dto);

            ApplicationUser? user = null;

            if (dto.UserNameOrEmail.Contains("@"))
                user = await _userManager.FindByEmailAsync(dto.UserNameOrEmail);
            else
                user = await _userManager.FindByNameAsync(dto.UserNameOrEmail);

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(dto);
            }

            // Block login if the tenant has been deactivated by SuperAdmin.
            // SuperAdmin users (TenantId = system GUID) are exempt.
            if (!user.IsSuperAdmin)
            {
                var tenantActive = await _db.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == user.TenantId)
                    .Select(t => t.IsActive)
                    .FirstOrDefaultAsync();

                if (!tenantActive)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been suspended. Please contact support.");
                    return View(dto);
                }
            }

            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                dto.Password,
                lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked. Too many failed attempts — try again in 15 minutes.");
                return View(dto);
            }

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(dto);
            }

            var now = DateTimeOffset.UtcNow;
            var authenticationProperties = new AuthenticationProperties
            {
                IsPersistent = dto.RememberMe,
                AllowRefresh = true,
                IssuedUtc = now,
                ExpiresUtc = now.Add(dto.RememberMe
                    ? RememberMeSessionLifetime
                    : StandardSessionLifetime)
            };

            await _signInManager.SignInAsync(user, authenticationProperties);

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (user.IsSuperAdmin)
                return RedirectToAction("Index", "SuperAdmin");

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.MustChangePassword == true)
                TempData["ForcedChange"] = true;
            return View(new ChangePasswordDto());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            var result = await _userManager.ChangePasswordAsync(
                user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(dto);
            }

            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                await _userManager.UpdateAsync(user);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMsg"] = "Password changed successfully.";

            if (user.IsSuperAdmin)
                return RedirectToAction("Index", "SuperAdmin");

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
            if (tenant == null)
                return Unauthorized();

            return View(BuildProfileDto(user, tenant));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileSettingsDto dto, CancellationToken cancellationToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction(nameof(Login));

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId);
            if (tenant == null)
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                ApplyProfileState(dto, user, tenant);
                return View(dto);
            }

            var requestedUserName = dto.UserName.Trim();
            var requestedEmail = dto.Email.Trim();
            var identityChanged =
                !string.Equals(user.UserName, requestedUserName, StringComparison.Ordinal) ||
                !string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase);

            if (identityChanged && !user.CanChangeLoginIdentity)
            {
                ModelState.AddModelError(string.Empty, "Username and email can only be changed once.");
                ApplyProfileState(dto, user, tenant);
                return View(dto);
            }

            if (!string.Equals(user.UserName, requestedUserName, StringComparison.Ordinal) &&
                await _userManager.Users.AnyAsync(x => x.Id != user.Id && x.UserName == requestedUserName, cancellationToken))
            {
                ModelState.AddModelError(nameof(dto.UserName), "This username is already taken.");
                ApplyProfileState(dto, user, tenant);
                return View(dto);
            }

            if (!string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase) &&
                await _userManager.Users.AnyAsync(x => x.Id != user.Id && x.Email == requestedEmail, cancellationToken))
            {
                ModelState.AddModelError(nameof(dto.Email), "This email is already in use.");
                ApplyProfileState(dto, user, tenant);
                return View(dto);
            }

            if (dto.ProfileImage != null)
            {
                try
                {
                    var uploadedFile = await _fileStorageService.SaveImageAsync(
                        dto.ProfileImage,
                        UploadCategories.UserProfile,
                        user.Id,
                        user.IsSuperAdmin ? null : user.TenantId,
                        cancellationToken);
                    user.ProfileImageUrl = _fileStorageService.GetPublicUrl(uploadedFile);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(dto.ProfileImage), ex.Message);
                    ApplyProfileState(dto, user, tenant);
                    return View(dto);
                }
            }

            if (user.IsTenantAdmin && dto.BrandLogoImage != null)
            {
                try
                {
                    var uploadedFile = await _fileStorageService.SaveImageAsync(
                        dto.BrandLogoImage,
                        UploadCategories.TenantLogo,
                        user.Id,
                        user.TenantId,
                        cancellationToken);
                    tenant.LogoUrl = _fileStorageService.GetPublicUrl(uploadedFile);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(nameof(dto.BrandLogoImage), ex.Message);
                    ApplyProfileState(dto, user, tenant);
                    return View(dto);
                }
            }

            user.FullName = dto.FullName.Trim();

            if (identityChanged)
            {
                user.UserName = requestedUserName;
                user.Email = requestedEmail;
                user.NormalizedUserName = _userManager.NormalizeName(requestedUserName);
                user.NormalizedEmail = _userManager.NormalizeEmail(requestedEmail);
                user.CanChangeLoginIdentity = false;
                user.LoginIdentityChangedAt = DateTime.UtcNow;
            }

            tenant.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                ApplyProfileState(dto, user, tenant);
                return View(dto);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMsg"]  = "Profile updated successfully.";
            TempData["SuccessType"] = "update";
            return RedirectToAction(nameof(Profile));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword() => View(new ForgotPasswordDto());

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var user = await _userManager.FindByEmailAsync(dto.Email.Trim());
            if (user != null && user.IsActive)
            {
                try
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callbackUrl = Url.Action(
                        nameof(ResetPassword),
                        "Account",
                        new { email = user.Email, token },
                        Request.Scheme);

                    if (!string.IsNullOrWhiteSpace(callbackUrl))
                    {
                        var safeName = WebUtility.HtmlEncode(user.FullName);
                        var safeUrl = HtmlEncoder.Default.Encode(callbackUrl);
                        await _emailService.SendAsync(
                            user.Email!,
                            "Reset your StockEasy password",
                            $"""
                            <p>Hello {safeName},</p>
                            <p>We received a request to reset your StockEasy password.</p>
                            <p><a href="{safeUrl}">Reset your password</a></p>
                            <p>If you did not request this, you can ignore this email.</p>
                            """,
                            HttpContext.RequestAborted);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send forgot-password email for user {Email}.", dto.Email);
                }
            }

            return View(nameof(ForgotPasswordConfirmation));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPasswordConfirmation() => View();

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPasswordConfirmation() => View();

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string? email = null, string? token = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));

            return View(new ResetPasswordDto
            {
                Email = email,
                Token = token
            });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !user.IsActive)
                return View(nameof(ResetPasswordConfirmation));

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(dto);
            }

            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            return View(nameof(ResetPasswordConfirmation));
        }

        [Authorize, HttpGet]
        public IActionResult FeatureRequired(string? feature)
        {
            ViewBag.Feature = feature ?? "";
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        [Authorize]
        [HttpGet]
        public IActionResult Ping()
        {
            return Ok(new { authenticated = true });
        }

        private static ProfileSettingsDto BuildProfileDto(ApplicationUser user, Tenant tenant)
        {
            return new ProfileSettingsDto
            {
                FullName = user.FullName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                CurrentProfileImageUrl = user.ProfileImageUrl,
                CurrentBrandLogoUrl = tenant.LogoUrl,
                TenantName = tenant.Name,
                IsTenantAdmin = user.IsTenantAdmin,
                CanChangeLoginIdentity = user.CanChangeLoginIdentity,
                LoginIdentityChangedAt = user.LoginIdentityChangedAt
            };
        }

        private static void ApplyProfileState(ProfileSettingsDto dto, ApplicationUser user, Tenant tenant)
        {
            dto.CurrentProfileImageUrl = user.ProfileImageUrl;
            dto.CurrentBrandLogoUrl = tenant.LogoUrl;
            dto.TenantName = tenant.Name;
            dto.IsTenantAdmin = user.IsTenantAdmin;
            dto.CanChangeLoginIdentity = user.CanChangeLoginIdentity;
            dto.LoginIdentityChangedAt = user.LoginIdentityChangedAt;
        }
    }
}
