using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Dashboard;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Time;
using ClothInventoryApp.Services.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [FeatureRequired("dashboard")]
    public class DashboardController : TenantAwareController
    {
        private readonly ITenantTimeService _tenantTimeService;
        private readonly ISubscriptionService _subscriptionService;

        public DashboardController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            ITenantTimeService tenantTimeService,
            ISubscriptionService subscriptionService)
            : base(context, tenantProvider)
        {
            _tenantTimeService = tenantTimeService;
            _subscriptionService = subscriptionService;
        }

        [HttpGet]
        public async Task<IActionResult> TrendData(int? month, int? year)
        {
            var tenantNow = _tenantTimeService.ConvertUtcToTenantTime(DateTime.UtcNow);
            var selectedMonth = month.GetValueOrDefault(tenantNow.Month);
            var selectedYear = year.GetValueOrDefault(tenantNow.Year);

            if (selectedMonth < 1 || selectedMonth > 12) selectedMonth = tenantNow.Month;

            var availableYears = await _context.Sales
                .AsNoTracking()
                .Select(s => s.SaleDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (availableYears.Count == 0) availableYears.Add(selectedYear);
            if (!availableYears.Contains(selectedYear)) selectedYear = availableYears[0];

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);

            var selectedMonthSales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= monthStart && s.SaleDate < monthEnd)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            var dailyTrend = Enumerable.Range(0, DateTime.DaysInMonth(selectedYear, selectedMonth))
                .Select(i => monthStart.AddDays(i))
                .Select(d => new
                {
                    label = d.ToString("dd MMM"),
                    amount = selectedMonthSales.Where(s => s.SaleDate.Date == d.Date).Sum(s => s.TotalAmount),
                    profit = selectedMonthSales.Where(s => s.SaleDate.Date == d.Date).Sum(s => s.TotalProfit)
                })
                .ToList();

            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = yearStart.AddYears(1);
            var monthlySales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= yearStart && s.SaleDate < yearEnd)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            var monthlyTrend = Enumerable.Range(0, 12)
                .Select(i => yearStart.AddMonths(i))
                .Select(m => new
                {
                    label = m.ToString("MMM"),
                    amount = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalAmount),
                    profit = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalProfit)
                })
                .ToList();

            var selectedMonthName = new DateTime(selectedYear, selectedMonth, 1).ToString("MMMM");

            return Json(new
            {
                dailyLabels = dailyTrend.Select(d => d.label),
                dailySales = dailyTrend.Select(d => d.amount),
                dailyProfit = dailyTrend.Select(d => d.profit),
                monthlyLabels = monthlyTrend.Select(m => m.label),
                monthlySales = monthlyTrend.Select(m => m.amount),
                monthlyProfit = monthlyTrend.Select(m => m.profit),
                dailySubtitle = $"Daily revenue and profit for {selectedMonthName} {selectedYear}",
                monthlySubtitle = $"Monthly revenue and profit for {selectedYear}"
            });
        }

        public async Task<IActionResult> Index(int? month, int? year)
        {
            var vm = new DashboardViewModel();
            var tenantId = _tenantProvider.GetTenantId();

            vm.TotalProducts = await _context.Products.CountAsync();
            vm.TotalVariants = await _context.ProductVariants.CountAsync();

            var tenantSettings = await _context.TenantSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);
            var lowStockThreshold = tenantSettings?.LowStockThreshold ?? 10;

            var stockData = await _context.ProductVariants
                .AsNoTracking()
                .Select(v => new
                {
                    v.Id,
                    v.ProductId,
                    ProductName = v.Product.Name,
                    Stock =
                        (v.StockMovements.Where(m => m.MovementType == "IN").Sum(m => (int?)m.Quantity) ?? 0)
                        - (v.StockMovements.Where(m => m.MovementType == "OUT").Sum(m => (int?)m.Quantity) ?? 0)
                })
                .ToListAsync();

            vm.LowStockCount = stockData.Count(x => x.Stock > 0 && x.Stock < lowStockThreshold);
            vm.OutOfStockCount = stockData.Count(x => x.Stock <= 0);

            vm.RecentActivities = await _context.StockMovements
                .AsNoTracking()
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

            var tenantNow = _tenantTimeService.ConvertUtcToTenantTime(DateTime.UtcNow);
            var selectedMonth = month.GetValueOrDefault(tenantNow.Month);
            var selectedYear = year.GetValueOrDefault(tenantNow.Year);

            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = tenantNow.Month;
            }

            var availableYears = await _context.Sales
                .AsNoTracking()
                .Select(s => s.SaleDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (availableYears.Count == 0)
            {
                availableYears.Add(selectedYear);
            }

            if (!availableYears.Contains(selectedYear))
            {
                selectedYear = availableYears[0];
            }

            vm.SelectedMonth = selectedMonth;
            vm.SelectedYear = selectedYear;
            vm.AvailableYears = availableYears;

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);
            var currentMonthStart = new DateTime(tenantNow.Year, tenantNow.Month, 1);
            var currentMonthEnd = currentMonthStart.AddMonths(1);

            var selectedMonthSales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= monthStart && s.SaleDate < monthEnd)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            var currentMonthSales = selectedYear == tenantNow.Year && selectedMonth == tenantNow.Month
                ? selectedMonthSales
                : await _context.Sales
                    .AsNoTracking()
                    .Where(s => s.SaleDate >= currentMonthStart && s.SaleDate < currentMonthEnd)
                    .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                    .ToListAsync();

            vm.TodaySalesAmount = currentMonthSales
                .Where(s => s.SaleDate.Date == tenantNow.Date)
                .Sum(s => s.TotalAmount);
            vm.CurrentMonthSalesAmount = currentMonthSales.Sum(s => s.TotalAmount);
            vm.CurrentMonthProfitAmount = currentMonthSales.Sum(s => s.TotalProfit);
            vm.CurrentMonthOrderCount = currentMonthSales.Count;

            vm.DailyTrend = Enumerable.Range(0, DateTime.DaysInMonth(selectedYear, selectedMonth))
                .Select(i => monthStart.AddDays(i))
                .Select(d => new SalesTrendPoint
                {
                    Label = d.ToString("dd MMM"),
                    Amount = selectedMonthSales.Where(s => s.SaleDate.Date == d.Date).Sum(s => s.TotalAmount),
                    Profit = selectedMonthSales.Where(s => s.SaleDate.Date == d.Date).Sum(s => s.TotalProfit)
                })
                .ToList();

            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = yearStart.AddYears(1);
            var monthlySales = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SaleDate >= yearStart && s.SaleDate < yearEnd)
                .Select(s => new { s.SaleDate, s.TotalAmount, s.TotalProfit })
                .ToListAsync();

            vm.MonthlyTrend = Enumerable.Range(0, 12)
                .Select(i => yearStart.AddMonths(i))
                .Select(m => new SalesTrendPoint
                {
                    Label = m.ToString("MMM"),
                    Amount = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalAmount),
                    Profit = monthlySales.Where(s => s.SaleDate.Year == m.Year && s.SaleDate.Month == m.Month).Sum(s => s.TotalProfit)
                })
                .ToList();

            vm.AttentionItems = new List<AttentionItemDto>
            {
                new()
                {
                    Title = "Low Stock Items",
                    Value = vm.LowStockCount.ToString(),
                    Note = "Variants below the replenishment threshold.",
                    ActionText = "Review Inventory",
                    Controller = "Stock",
                    Action = "Inventory",
                    RouteValue = "true",
                    Tone = vm.LowStockCount > 0 ? "orange" : "green"
                },
                new()
                {
                    Title = "Out Of Stock",
                    Value = vm.OutOfStockCount.ToString(),
                    Note = "Variants that cannot be sold right now.",
                    ActionText = "Check Stock",
                    Controller = "Stock",
                    Action = "Inventory",
                    Tone = vm.OutOfStockCount > 0 ? "red" : "green"
                },
                new()
                {
                    Title = "Month Profit",
                    Value = vm.CurrentMonthProfitAmount.ToString("N0"),
                    Note = "Estimated profit from sales recorded this month.",
                    ActionText = "Open Finance",
                    Controller = "Finance",
                    Action = "Index",
                    Tone = vm.CurrentMonthProfitAmount >= 0 ? "blue" : "red"
                }
            };
            vm.PlanLimitWarnings = await _subscriptionService.BuildPlanLimitWarningsAsync(tenantId);

            return View(vm);
        }
    }
}
