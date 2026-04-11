using System.Diagnostics;
using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using ClothInventoryApp.Models.ViewModels;
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

    public HomeController(
        ILogger<HomeController> logger,
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser == null)
            return RedirectToAction("Login", "Account");

        var tenantId = currentUser.TenantId;

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId);

        if (tenant == null)
            return Unauthorized();

        var totalProducts = await _context.Products
            .CountAsync(x => x.TenantId == tenantId);

        var totalVariants = await _context.ProductVariants
            .CountAsync(x => x.TenantId == tenantId);

        // Low Stock (example: stock < 10)
        var stockData = await _context.ProductVariants
            .Select(v => new
            {
                v.Id,
                Stock =
                    v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0
                    - v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0
            })
            .ToListAsync();
        var lowStockCount = stockData.Count(x => x.Stock < 10);

        var recentActivities = await _context.StockMovements
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.ProductVariant)
                .ThenInclude(v => v.Product)
            .OrderByDescending(x => x.MovementDate)
            .Take(10)
            .Select(x => new RecentInventoryActivityItem
            {
                SKU = x.ProductVariant.SKU,
                ProductName = x.ProductVariant.Product.Name + " " + x.ProductVariant.Color + " " + x.ProductVariant.Size,
                Action = x.MovementType,
                QtyText = x.MovementType == "OUT" ? "-" + x.Quantity : "+" + x.Quantity,
                MovementDate = x.MovementDate,
                Status = x.MovementType == "ADJUST" ? "Attention" : "Completed"
            })
            .ToListAsync();

        var vm = new HomeOverviewViewModel
        {
            TenantName = tenant.Name,
            TenantLogoUrl = tenant.LogoUrl,
            TotalProducts = totalProducts,
            TotalVariants = totalVariants,
            LowStockCount = lowStockCount,
            RecentActivities = recentActivities
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}