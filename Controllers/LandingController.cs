using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Landing;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public LandingController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var plans = await _db.Plans
                .Where(p => p.IsActive)
                .Include(p => p.PlanFeatures.Where(pf => pf.IsEnabled))
                    .ThenInclude(pf => pf.Feature)
                .OrderBy(p => p.PriceMonthly)
                .ToListAsync();

            ViewBag.Plans = plans;
            return View(new LandingSignupDto());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("public-registration")]
        public async Task<IActionResult> ContactSubmit(LandingSignupDto dto)
        {
            var plans = await _db.Plans
                .Where(p => p.IsActive)
                .Include(p => p.PlanFeatures.Where(pf => pf.IsEnabled))
                    .ThenInclude(pf => pf.Feature)
                .OrderBy(p => p.PriceMonthly)
                .ToListAsync();
            ViewBag.Plans = plans;

            if (!ModelState.IsValid)
                return View(nameof(Index), dto);

            var normalizedEmail = dto.Email.Trim();
            if (await _userManager.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail || u.UserName == normalizedEmail))
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
    }
}
