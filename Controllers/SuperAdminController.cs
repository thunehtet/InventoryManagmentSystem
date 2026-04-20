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
            var today = now.Date;
            var lookbackStart = today.AddDays(-29);
            var dormantCutoff = today.AddDays(-7);

            var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().ToListAsync();
            var users   = await _db.Users.AsNoTracking().IgnoreQueryFilters().ToListAsync();
            var subscriptions = await _db.TenantSubscriptions
                .AsNoTracking().IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Where(s => s.IsActive)
                .ToListAsync();
            var dailyUsage = await _db.TenantDailyUsages
                .AsNoTracking()
                .Where(x => x.UsageDate >= lookbackStart)
                .ToListAsync();
            var recentActivities = await _db.UserActivityLogs
                .AsNoTracking()
                .Include(x => x.Tenant)
                .Include(x => x.User)
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
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

            var topTenantUsage = dailyUsage
                .GroupBy(x => x.TenantId)
                .Select(g =>
                {
                    var tenant = tenants.FirstOrDefault(t => t.Id == g.Key);
                    return new TenantUsageCardDto
                    {
                        TenantId = g.Key,
                        TenantName = tenant?.Name ?? "Unknown",
                        TenantCode = tenant?.Code ?? "-",
                        LoginCount = g.Sum(x => x.LoginCount),
                        TotalActions = g.Sum(x => x.TotalActionCount),
                        ActiveUsers = users.Count(u =>
                            !u.IsSuperAdmin &&
                            u.TenantId == g.Key &&
                            u.LastActivityAt.HasValue &&
                            u.LastActivityAt.Value.Date >= lookbackStart),
                        LastActivityAt = tenant?.LastActivityAt
                    };
                })
                .OrderByDescending(x => x.TotalActions)
                .ThenByDescending(x => x.LoginCount)
                .Take(6)
                .ToList();

            var dormantTenants = tenants
                .Where(t => t.IsActive && (!t.LastActivityAt.HasValue || t.LastActivityAt.Value.Date < dormantCutoff))
                .OrderBy(t => t.LastActivityAt ?? DateTime.MinValue)
                .Take(6)
                .Select(t => new TenantUsageCardDto
                {
                    TenantId = t.Id,
                    TenantName = t.Name,
                    TenantCode = t.Code,
                    LastActivityAt = t.LastActivityAt
                })
                .ToList();

            return View(new SuperAdminDashboardDto
            {
                TotalTenants        = tenants.Count,
                ActiveTenants       = tenants.Count(t => t.IsActive),
                TotalUsers          = users.Count(u => !u.IsSuperAdmin),
                ActiveSubscriptions = subscriptions.Count,
                MonthlyRevenue      = subscriptions.Sum(s => s.BillingCycle == "Yearly" ? s.Price / 12 : s.Price),
                NewTenantsThisMonth = tenants.Count(t => t.CreatedAt >= startOfMonth),
                PlanDistribution    = planDist,
                RecentTenants       = recentTenants,
                TodayLoginCount     = dailyUsage.Where(x => x.UsageDate == today).Sum(x => x.LoginCount),
                TodayActionCount    = dailyUsage.Where(x => x.UsageDate == today).Sum(x => x.TotalActionCount),
                ActiveTenants30Days = dailyUsage.Select(x => x.TenantId).Distinct().Count(),
                DormantTenants      = tenants.Count(t => t.IsActive && (!t.LastActivityAt.HasValue || t.LastActivityAt.Value.Date < dormantCutoff)),
                TopTenantUsage      = topTenantUsage,
                DormantTenantList   = dormantTenants,
                RecentActivities    = recentActivities.Select(x => new RecentUserActivityDto
                {
                    Id = x.Id,
                    TenantId = x.TenantId,
                    TenantName = x.Tenant?.Name,
                    UserName = x.User?.FullName ?? x.User?.Email,
                    Feature = x.Feature,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    Description = x.Description,
                    CreatedAt = x.CreatedAt
                }).ToList()
            });
        }

        public async Task<IActionResult> TenantUsage(Guid id)
        {
            var tenant = await _db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (tenant == null) return NotFound();

            var today = DateTime.UtcNow.Date;
            var lookbackStart = today.AddDays(-29);

            var dailyUsage = await _db.TenantDailyUsages
                .AsNoTracking()
                .Where(x => x.TenantId == id && x.UsageDate >= lookbackStart)
                .OrderBy(x => x.UsageDate)
                .ToListAsync();

            var users = await _db.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == id && !x.IsSuperAdmin)
                .OrderByDescending(x => x.LastActivityAt)
                .ThenBy(x => x.FullName)
                .ToListAsync();

            var loginCounts = await _db.UserLoginAudits
                .AsNoTracking()
                .Where(x => x.TenantId == id && x.IsSuccess && x.AttemptedAt >= lookbackStart && x.UserId != null)
                .GroupBy(x => x.UserId!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var actionCounts = await _db.UserActivityLogs
                .AsNoTracking()
                .Where(x => x.TenantId == id && x.CreatedAt >= lookbackStart && x.UserId != null)
                .GroupBy(x => x.UserId!)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var recentActivities = await _db.UserActivityLogs
                .AsNoTracking()
                .Include(x => x.User)
                .Where(x => x.TenantId == id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync();

            var dto = new TenantUsageDetailDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TenantCode = tenant.Code,
                ContactEmail = tenant.ContactEmail,
                IsActive = tenant.IsActive,
                CreatedAt = tenant.CreatedAt,
                LastActivityAt = tenant.LastActivityAt,
                TotalUsers = users.Count,
                ActiveUsers30Days = users.Count(x => x.LastActivityAt.HasValue && x.LastActivityAt.Value.Date >= lookbackStart),
                LoginCount30Days = dailyUsage.Sum(x => x.LoginCount),
                TotalActions30Days = dailyUsage.Sum(x => x.TotalActionCount),
                DailyUsage = dailyUsage.Select(x => new TenantUsageDayDto
                {
                    UsageDate = x.UsageDate,
                    Logins = x.LoginCount,
                    Actions = x.TotalActionCount,
                    ActiveUsers = x.ActiveUsers
                }).ToList(),
                Users = users.Select(x => new TenantUserActivityDto
                {
                    UserId = x.Id,
                    FullName = x.FullName,
                    Email = x.Email,
                    IsTenantAdmin = x.IsTenantAdmin,
                    LastLoginAt = x.LastLoginAt,
                    LastActivityAt = x.LastActivityAt,
                    LoginCount30Days = loginCounts.TryGetValue(x.Id, out var loginCount) ? loginCount : 0,
                    ActionCount30Days = actionCounts.TryGetValue(x.Id, out var actionCount) ? actionCount : 0
                }).ToList(),
                RecentActivities = recentActivities.Select(x => new RecentUserActivityDto
                {
                    Id = x.Id,
                    TenantId = id,
                    TenantName = tenant.Name,
                    UserName = x.User?.FullName ?? x.User?.Email,
                    Feature = x.Feature,
                    Action = x.Action,
                    EntityType = x.EntityType,
                    Description = x.Description,
                    CreatedAt = x.CreatedAt
                }).ToList()
            };

            return View(dto);
        }
    }
}
