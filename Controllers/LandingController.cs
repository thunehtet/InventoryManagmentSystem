using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Landing;
using ClothInventoryApp.Models;
using ClothInventoryApp.Options;
using ClothInventoryApp.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClothInventoryApp.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITurnstileValidationService _turnstileValidationService;
        private readonly TurnstileSettings _turnstileSettings;

        public LandingController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            ITurnstileValidationService turnstileValidationService,
            IOptions<TurnstileSettings> turnstileSettings)
        {
            _db = db;
            _userManager = userManager;
            _turnstileValidationService = turnstileValidationService;
            _turnstileSettings = turnstileSettings.Value;
        }

        public async Task<IActionResult> Index()
        {
            await LoadLandingViewDataAsync();
            return View(new LandingSignupDto());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("public-registration")]
        public async Task<IActionResult> ContactSubmit(LandingSignupDto dto)
        {
            await LoadLandingViewDataAsync();

            if (!ModelState.IsValid)
                return View(nameof(Index), dto);

            if (!_turnstileSettings.IsConfigured)
            {
                ModelState.AddModelError(string.Empty, "Bot protection is not configured yet. Please try again later.");
                return View(nameof(Index), dto);
            }

            var turnstileToken = Request.Form["cf-turnstile-response"].ToString();
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var turnstileValid = await _turnstileValidationService.ValidateAsync(
                turnstileToken,
                remoteIp,
                HttpContext.RequestAborted);

            if (!turnstileValid)
            {
                ModelState.AddModelError(string.Empty, "Bot verification failed. Please try again.");
                return View(nameof(Index), dto);
            }

            var normalizedEmail = NormalizeEmail(dto.Email);
            if (await EmailAlreadyExistsAsync(normalizedEmail))
            {
                ModelState.AddModelError(nameof(dto.Email), "This email is already registered.");
                return View(nameof(Index), dto);
            }

            var hasPendingInquiry = await _db.ContactInquiries.AnyAsync(x =>
                x.Email == normalizedEmail &&
                x.Status == "Pending");

            if (hasPendingInquiry)
            {
                ModelState.AddModelError(string.Empty, "A pending inquiry already exists for this email. Please wait for SuperAdmin review.");
                return View(nameof(Index), dto);
            }

            _db.ContactInquiries.Add(new ContactInquiry
            {
                BusinessName = dto.BusinessName.Trim(),
                BusinessType = dto.BusinessType.Trim(),
                Name = dto.FullName.Trim(),
                Email = normalizedEmail,
                Phone = dto.Phone?.Trim(),
                Message = "Landing page free account inquiry.",
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            TempData["ContactSuccess"] = true;
            TempData["ContactName"] = dto.FullName.Trim();
            TempData["SignupBusinessName"] = dto.BusinessName.Trim();
            return Redirect(Url.Action(nameof(Index))! + "#contact");
        }

        private string NormalizeEmail(string email)
            => _userManager.NormalizeEmail(email.Trim());

        private async Task<bool> EmailAlreadyExistsAsync(string normalizedEmail)
            => await _userManager.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedEmail);

        private async Task LoadLandingViewDataAsync()
        {
            var plans = await _db.Plans
                .Where(p => p.IsActive)
                .Include(p => p.PlanFeatures.Where(pf => pf.IsEnabled))
                    .ThenInclude(pf => pf.Feature)
                .OrderBy(p => p.PriceMonthly)
                .ToListAsync();

            ViewBag.Plans = plans;
            ViewBag.TurnstileSiteKey = _turnstileSettings.SiteKey;
        }
    }
}
