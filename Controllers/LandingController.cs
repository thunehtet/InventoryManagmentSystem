using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [AllowAnonymous]
    public class LandingController : Controller
    {
        private readonly AppDbContext _db;

        public LandingController(AppDbContext db)
        {
            _db = db;
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
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ContactSubmit(
            string name, string email, string? phone, string? message)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            {
                _db.ContactInquiries.Add(new ContactInquiry
                {
                    Name    = name.Trim(),
                    Email   = email.Trim(),
                    Phone   = phone?.Trim(),
                    Message = message?.Trim()
                });
                await _db.SaveChangesAsync();
            }

            TempData["ContactSuccess"] = true;
            TempData["ContactName"]    = name;
            return Redirect(Url.Action(nameof(Index))! + "#contact");
        }
    }
}
