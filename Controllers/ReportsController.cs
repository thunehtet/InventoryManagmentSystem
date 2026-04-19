using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Reports;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [FeatureRequired("sales")]
    public class ReportsController : TenantAwareController
    {
        private readonly IFeatureService _featureService;
        private readonly ITenantTimeService _tenantTimeService;

        public ReportsController(
            AppDbContext context,
            ITenantProvider tenantProvider,
            IFeatureService featureService,
            ITenantTimeService tenantTimeService)
            : base(context, tenantProvider)
        {
            _featureService = featureService;
            _tenantTimeService = tenantTimeService;
        }

        public async Task<IActionResult> Sales(string preset = "this_month", DateTime? from = null, DateTime? to = null)
        {
            var now = _tenantTimeService.ConvertUtcToTenantTime(DateTime.UtcNow).Date;

            DateTime dateFrom, dateTo;
            switch (preset)
            {
                case "last_month":
                    var lastMonth = now.AddMonths(-1);
                    dateFrom = new DateTime(lastMonth.Year, lastMonth.Month, 1);
                    dateTo   = dateFrom.AddMonths(1).AddDays(-1);
                    break;
                case "this_year":
                    dateFrom = new DateTime(now.Year, 1, 1);
                    dateTo   = new DateTime(now.Year, 12, 31);
                    break;
                case "last_7":
                    dateFrom = now.AddDays(-6);
                    dateTo   = now;
                    break;
                case "custom":
                    dateFrom = from ?? new DateTime(now.Year, now.Month, 1);
                    dateTo   = to   ?? now;
                    preset   = "custom";
                    break;
                default: // this_month
                    preset   = "this_month";
                    dateFrom = new DateTime(now.Year, now.Month, 1);
                    dateTo   = now;
                    break;
            }

            if (dateFrom > dateTo) dateTo = dateFrom;

            var rangeEnd = dateTo.AddDays(1); // exclusive upper bound for queries

            var tid = _tenantProvider.GetTenantId();
            var hasSaleProfit = await _featureService.HasFeatureAsync(tid, "sale_profit");
            var hasCustomers  = await _featureService.HasFeatureAsync(tid, "customers");

            var sales = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Customer)
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate < rangeEnd)
                .OrderByDescending(s => s.SaleDate)
                .Select(s => new SalesReportItem
                {
                    Id           = s.Id,
                    SaleDate     = s.SaleDate,
                    CustomerName = s.Customer != null ? s.Customer.Name : null,
                    TotalAmount  = s.TotalAmount,
                    Discount     = s.Discount,
                    TotalProfit  = s.TotalProfit
                })
                .ToListAsync();

            // daily breakdown — only days that had sales
            var dailyRows = sales
                .GroupBy(s => s.SaleDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new SalesReportDayRow
                {
                    Date     = g.Key,
                    Count    = g.Count(),
                    Revenue  = g.Sum(s => s.TotalAmount),
                    Discount = g.Sum(s => s.Discount),
                    Profit   = g.Sum(s => s.TotalProfit)
                })
                .ToList();

            var vm = new SalesReportViewModel
            {
                From         = dateFrom,
                To           = dateTo,
                Preset       = preset,
                TotalCount   = sales.Count,
                TotalRevenue = sales.Sum(s => s.TotalAmount),
                TotalDiscount= sales.Sum(s => s.Discount),
                TotalProfit  = sales.Sum(s => s.TotalProfit),
                DailyRows    = dailyRows,
                Sales        = sales
            };

            ViewBag.HasSaleProfit = hasSaleProfit;
            ViewBag.HasCustomers  = hasCustomers;

            return View(vm);
        }
    }
}
