using System.Diagnostics;
using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using ClothInventoryApp.Models.ViewModels;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IFeatureService _featureService;

    public HomeController(
        ILogger<HomeController> logger,
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        IFeatureService featureService)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _featureService = featureService;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return RedirectToAction("Login", "Account");

        var tenantId = currentUser.TenantId;
        var tenant   = await _context.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId);
        if (tenant == null) return Unauthorized();

        // ── Metrics ───────────────────────────────────────────────
        var totalProducts = await _context.Products.CountAsync(x => x.TenantId == tenantId);
        var totalVariants = await _context.ProductVariants.CountAsync(x => x.TenantId == tenantId);

        var tenantSettings = await _context.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);
        var lowStockThreshold = tenantSettings?.LowStockThreshold ?? 10;

        var stockData = await _context.ProductVariants
            .Select(v => new
            {
                Stock =
                    (v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0)
                    - (v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0)
            })
            .ToListAsync();
        var isAdmin = currentUser.IsTenantAdmin;
        var alertEnabledForUser = isAdmin || (tenantSettings?.LowStockAlertEnabled != false);
        var lowStockCount = alertEnabledForUser
            ? stockData.Count(x => x.Stock < lowStockThreshold)
            : 0;

        var recentActivities = await _context.StockMovements
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.ProductVariant).ThenInclude(v => v.Product)
            .OrderByDescending(x => x.MovementDate)
            .Take(8)
            .Select(x => new RecentInventoryActivityItem
            {
                SKU         = x.ProductVariant.SKU,
                ProductName = x.ProductVariant.Product.Name + " " + x.ProductVariant.Color + " " + x.ProductVariant.Size,
                Action      = x.MovementType,
                QtyText     = x.MovementType == "OUT" ? "-" + x.Quantity : "+" + x.Quantity,
                MovementDate = x.MovementDate,
                Status      = x.MovementType == "ADJUST" ? "Attention" : "Completed"
            })
            .ToListAsync();

        // ── Subscription ──────────────────────────────────────────
        var today = DateTime.UtcNow;
        var activeSub = await _context.TenantSubscriptions
            .Where(s => s.TenantId == tenantId && s.IsActive && s.StartDate <= today && s.EndDate >= today)
            .OrderByDescending(s => s.EndDate)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync();

        var planName = activeSub?.Plan.Name ?? "No Plan";
        var planCode = activeSub?.Plan.Code ?? "";
        var daysLeft = activeSub != null ? Math.Max(0, (int)(activeSub.EndDate.Date - today.Date).TotalDays) : 0;
        var isTrial  = activeSub?.IsTrial ?? false;

        // ── Limits ────────────────────────────────────────────────
        var (productsCurrent, productsMax) = await _subscriptionService.GetProductLimitAsync(tenantId);
        var (variantsCurrent, variantsMax) = await _subscriptionService.GetVariantLimitAsync(tenantId);
        var (usersCurrent,    usersMax)    = await _subscriptionService.GetUserLimitAsync(tenantId);

        // ── Feature flags ─────────────────────────────────────────
        var hasSales      = await _featureService.HasFeatureAsync(tenantId, "sales");
        var hasSaleProfit = await _featureService.HasFeatureAsync(tenantId, "sale_profit");
        var hasCustomers  = await _featureService.HasFeatureAsync(tenantId, "customers");
        var hasDashboard  = await _featureService.HasFeatureAsync(tenantId, "dashboard");
        var hasFinance    = await _featureService.HasFeatureAsync(tenantId, "finance");
        var hasTextiles   = await _featureService.HasFeatureAsync(tenantId, "textiles");

        // ── Upgrade info ──────────────────────────────────────────
        string? nextPlanName  = null;
        int?    nextPlanPrice = null;
        var     upgradeFeatures = new List<UpgradeFeatureItem>();

        var nextPlanCode = planCode switch
        {
            "FREE"    => "STARTER",
            "STARTER" => "PRO",
            _         => null
        };

        if (nextPlanCode != null)
        {
            var nextPlan = await _context.Plans.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Code == nextPlanCode && p.IsActive);

            if (nextPlan != null)
            {
                nextPlanName  = nextPlan.Name;
                nextPlanPrice = nextPlan.PriceMonthly;

                // Features the tenant doesn't have yet that the next plan includes
                var currentFeatureCodes = await _context.TenantSubscriptions
                    .Where(s => s.TenantId == tenantId && s.IsActive)
                    .SelectMany(s => s.Plan.PlanFeatures)
                    .Where(pf => pf.IsEnabled)
                    .Select(pf => pf.Feature.Code)
                    .ToListAsync();

                var nextPlanFeatures = await _context.PlanFeatures
                    .Where(pf => pf.PlanId == nextPlan.Id && pf.IsEnabled)
                    .Include(pf => pf.Feature)
                    .ToListAsync();

                var featureIconMap = new Dictionary<string, (string Icon, string Label)>
                {
                    ["sale_profit"] = ("bi-graph-up-arrow",       "Profit & cost tracking in sales"),
                    ["customers"]   = ("bi-people-fill",           "Customer management"),
                    ["dashboard"]   = ("bi-bar-chart-line-fill",   "Sales dashboard & analytics"),
                    ["finance"]     = ("bi-cash-coin",             "Finance dashboard & expenses"),
                    ["textiles"]    = ("bi-tags-fill",             "Purchases for stock replenishment"),
                    ["sales"]       = ("bi-bag-check-fill",        "Sales recording"),
                };

                foreach (var pf in nextPlanFeatures)
                {
                    if (!currentFeatureCodes.Contains(pf.Feature.Code) &&
                        featureIconMap.TryGetValue(pf.Feature.Code, out var info))
                    {
                        upgradeFeatures.Add(new UpgradeFeatureItem
                        {
                            Icon  = info.Icon,
                            Label = info.Label
                        });
                    }
                }

                // Add limit increases
                if (nextPlan.MaxProducts > (activeSub?.Plan.MaxProducts ?? 0))
                    upgradeFeatures.Add(new UpgradeFeatureItem { Icon = "bi-box-seam-fill",  Label = $"Up to {nextPlan.MaxProducts} products" });
                if (nextPlan.MaxVariants > (activeSub?.Plan.MaxVariants ?? 0))
                    upgradeFeatures.Add(new UpgradeFeatureItem { Icon = "bi-palette-fill",   Label = $"Up to {nextPlan.MaxVariants} variants" });
                if (nextPlan.MaxUsers > (activeSub?.Plan.MaxUsers ?? 0))
                    upgradeFeatures.Add(new UpgradeFeatureItem { Icon = "bi-person-plus-fill", Label = $"Up to {nextPlan.MaxUsers} users" });
            }
        }

        var vm = new HomeOverviewViewModel
        {
            TenantName   = tenant.Name,
            TenantLogoUrl = tenant.LogoUrl,
            TotalProducts = totalProducts,
            TotalVariants = totalVariants,
            LowStockCount = lowStockCount,
            LowStockAlertVisible = alertEnabledForUser,
            RecentActivities = recentActivities,

            PlanName  = planName,
            PlanCode  = planCode,
            DaysLeft  = daysLeft,
            IsTrial   = isTrial,
            IsSubscriptionActive = activeSub != null,

            ProductsCurrent = productsCurrent,
            ProductsMax     = productsMax,
            VariantsCurrent = variantsCurrent,
            VariantsMax     = variantsMax,
            UsersCurrent    = usersCurrent,
            UsersMax        = usersMax,

            HasSales      = hasSales,
            HasSaleProfit = hasSaleProfit,
            HasCustomers  = hasCustomers,
            HasDashboard  = hasDashboard,
            HasFinance    = hasFinance,
            HasTextiles   = hasTextiles,

            NextPlanName    = nextPlanName,
            NextPlanPrice   = nextPlanPrice,
            UpgradeFeatures = upgradeFeatures
        };

        return View(vm);
    }

    public IActionResult Privacy() => View();

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
