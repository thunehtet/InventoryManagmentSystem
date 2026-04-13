using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.SuperAdmin;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : Controller
    {
        private readonly AppDbContext _db;

        public SuperAdminController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().ToListAsync();
            var users   = await _db.Users.AsNoTracking().IgnoreQueryFilters().ToListAsync();
            var subscriptions = await _db.TenantSubscriptions
                .AsNoTracking().IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Where(s => s.IsActive)
                .ToListAsync();

            var planDist = subscriptions
                .GroupBy(s => new { s.Plan.Name, s.Plan.Code })
                .Select(g => new PlanDistributionItem { PlanName = g.Key.Name, PlanCode = g.Key.Code, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var recentTenants = tenants
                .OrderByDescending(t => t.CreatedAt).Take(8)
                .Select(t => new RecentTenantItem
                {
                    Id = t.Id, Name = t.Name, Code = t.Code,
                    ContactEmail = t.ContactEmail, IsActive = t.IsActive, CreatedAt = t.CreatedAt,
                    ActivePlan = subscriptions.FirstOrDefault(s => s.TenantId == t.Id)?.Plan.Name
                }).ToList();

            return View(new SuperAdminDashboardDto
            {
                TotalTenants        = tenants.Count,
                ActiveTenants       = tenants.Count(t => t.IsActive),
                TotalUsers          = users.Count(u => !u.IsSuperAdmin),
                ActiveSubscriptions = subscriptions.Count,
                MonthlyRevenue      = subscriptions.Sum(s => s.BillingCycle == "Yearly" ? s.Price / 12 : s.Price),
                NewTenantsThisMonth = tenants.Count(t => t.CreatedAt >= startOfMonth),
                PlanDistribution    = planDist,
                RecentTenants       = recentTenants
            });
        }
    }
}
