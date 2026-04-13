using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Dashboard;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : TenantAwareController
    {
        public DashboardController(AppDbContext context, ITenantProvider tenantProvider)
            : base(context, tenantProvider)
        {
        }

        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel();

            vm.TotalProducts = await _context.Products.CountAsync();
            vm.TotalVariants = await _context.ProductVariants.CountAsync();

            var stockData = await _context.ProductVariants
                .Select(v => new
                {
                    Stock =
                        (v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0)
                        - (v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0)
                })
                .ToListAsync();

            vm.LowStockCount = stockData.Count(x => x.Stock < 10);

            vm.RecentActivities = await _context.StockMovements
                .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
                .OrderByDescending(s => s.MovementDate)
                .Take(5)
                .Select(s => new RecentActivityDto
                {
                    SKU = s.ProductVariant.SKU,
                    ProductName = s.ProductVariant.Product.Name,
                    Action = s.MovementType == "IN" ? "Stock In" : "Stock Out",
                    Quantity = s.MovementType == "IN" ? s.Quantity : -s.Quantity,
                    Date = s.MovementDate,
                    Status = "Completed"
                })
                .ToListAsync();

            var now = DateTime.UtcNow;

            // ── Daily: last 30 days ──────────────────────────────────
            var dailyFrom = now.Date.AddDays(-29);
            var dailySales = await _context.Sales
                .Where(s => s.SaleDate >= dailyFrom)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            vm.DailyTrend = Enumerable.Range(0, 30)
                .Select(i => dailyFrom.AddDays(i))
                .Select(d => new SalesTrendPoint
                {
                    Label  = d.ToString("dd MMM"),
                    Amount = dailySales.Where(s => s.SaleDate.Date == d).Sum(s => s.TotalAmount),
                    Profit = dailySales.Where(s => s.SaleDate.Date == d).Sum(s => s.TotalProfit)
                })
                .ToList();

            // ── Monthly: last 12 months ──────────────────────────────
            var monthlyFrom = new DateTime(now.Year, now.Month, 1).AddMonths(-11);
            var monthlySales = await _context.Sales
                .Where(s => s.SaleDate >= monthlyFrom)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            vm.MonthlyTrend = Enumerable.Range(0, 12)
                .Select(i => monthlyFrom.AddMonths(i))
                .Select(m => new SalesTrendPoint
                {
                    Label  = m.ToString("MMM yy"),
                    Amount = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalAmount),
                    Profit = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalProfit)
                })
                .ToList();

            return View(vm);
        }
    }
}
