using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Landing;
using ClothInventoryApp.Models;
using ClothInventoryApp.Options;
using ClothInventoryApp.Services.Email;
using ClothInventoryApp.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace ClothInventoryApp.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ITurnstileValidationService _turnstileValidationService;
        private readonly TurnstileSettings _turnstileSettings;
        private readonly ILogger<LandingController> _logger;

        public LandingController(
            AppDbContext db,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            ITurnstileValidationService turnstileValidationService,
            IOptions<TurnstileSettings> turnstileSettings,
            ILogger<LandingController> logger)
        {
            _db = db;
            _userManager = userManager;
            _emailService = emailService;
            _turnstileValidationService = turnstileValidationService;
            _turnstileSettings = turnstileSettings.Value;
            _logger = logger;
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

            var email = dto.Email.Trim();
            var normalizedEmail = NormalizeEmail(email);
            if (await EmailAlreadyExistsAsync(normalizedEmail))
            {
                ModelState.AddModelError(nameof(dto.Email), "This email is already registered.");
                return View(nameof(Index), dto);
            }

            var hasPendingInquiry = await _db.ContactInquiries.AnyAsync(x =>
                x.Email.ToUpper() == normalizedEmail &&
                x.Status == "Pending");

            if (hasPendingInquiry)
            {
                ModelState.AddModelError(string.Empty, "A pending inquiry already exists for this email. Please wait for SuperAdmin review.");
                return View(nameof(Index), dto);
            }

            var inquiry = new ContactInquiry
            {
                BusinessName = dto.BusinessName.Trim(),
                BusinessType = dto.BusinessType.Trim(),
                Name = dto.FullName.Trim(),
                Email = email,
                Phone = dto.Phone?.Trim(),
                Message = "Landing page free account inquiry.",
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow
            };

            _db.ContactInquiries.Add(inquiry);

            await _db.SaveChangesAsync();
            await NotifySuperAdminsOfInquiryAsync(inquiry);

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

        private async Task NotifySuperAdminsOfInquiryAsync(ContactInquiry inquiry)
        {
            var superAdminEmails = await _userManager.Users
                .IgnoreQueryFilters()
                .Where(u => u.IsSuperAdmin && !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => u.Email!)
                .Distinct()
                .ToListAsync();

            if (superAdminEmails.Count == 0)
            {
                _logger.LogWarning("Inquiry {InquiryId} was saved, but no SuperAdmin email recipients were found.", inquiry.Id);
                return;
            }

            var subject = "New landing page inquiry received";
            var htmlBody = $"""
                <p>A new inquiry was submitted from the landing page.</p>
                <p><strong>Business Name:</strong> {WebUtility.HtmlEncode(inquiry.BusinessName)}</p>
                <p><strong>Business Type:</strong> {WebUtility.HtmlEncode(inquiry.BusinessType)}</p>
                <p><strong>Name:</strong> {WebUtility.HtmlEncode(inquiry.Name)}</p>
                <p><strong>Email:</strong> {WebUtility.HtmlEncode(inquiry.Email)}</p>
                <p><strong>Phone:</strong> {WebUtility.HtmlEncode(inquiry.Phone ?? "-")}</p>
                <p><strong>Inquiry Id:</strong> {inquiry.Id}</p>
                <p><strong>Submitted At (UTC):</strong> {inquiry.SubmittedAt:yyyy-MM-dd HH:mm:ss}</p>
                <p>Review it from the SuperAdmin inquiry box.</p>
                """;

            foreach (var email in superAdminEmails)
            {
                try
                {
                    await _emailService.SendAsync(email, subject, htmlBody, HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send inquiry notification email for inquiry {InquiryId} to SuperAdmin {Email}.", inquiry.Id, email);
                }
            }
        }

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
