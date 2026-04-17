using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Finance;
using ClothInventoryApp.Filters;
using ClothInventoryApp.Services.Tenant;
using ClothInventoryApp.Services.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    [Authorize(Roles = "Admin")]
    [FeatureRequired("finance")]
    public class FinanceController : TenantAwareController
    {
        private readonly ITenantTimeService _tenantTimeService;

        public FinanceController(AppDbContext context, ITenantProvider tenantProvider, ITenantTimeService tenantTimeService)
            : base(context, tenantProvider)
        {
            _tenantTimeService = tenantTimeService;
        }

        public async Task<IActionResult> Index(int? month, int? year)
        {
            var tenantNow = _tenantTimeService.ConvertUtcToTenantTime(DateTime.UtcNow);
            var selectedMonth = month.GetValueOrDefault(tenantNow.Month);
            var selectedYear = year.GetValueOrDefault(tenantNow.Year);
            var saleIncomeCategories = new[] { "Sale Income", "Sales" };
            var purchaseCategories = new[] { "Inventory Purchase", "Textile Purchase" };
            var packagingCategories = new[] { "Packaging Fee", "Packaging" };
            var transportationCategories = new[] { "Transportation", "Transport" };

            if (selectedMonth < 1 || selectedMonth > 12)
            {
                selectedMonth = tenantNow.Month;
            }

            var availableYears = await _context.CashTransactions
                .AsNoTracking()
                .Select(x => x.TransactionDate.Year)
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

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);
            var previousMonthStart = monthStart.AddMonths(-1);
            var previousMonthEnd = monthStart;
            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = yearStart.AddYears(1);

            var periodTransactions = await _context.CashTransactions
                .AsNoTracking()
                .Where(x => x.TransactionDate >= monthStart && x.TransactionDate < monthEnd)
                .ToListAsync();

            var previousMonthTransactions = await _context.CashTransactions
                .AsNoTracking()
                .Where(x => x.TransactionDate >= previousMonthStart && x.TransactionDate < previousMonthEnd)
                .ToListAsync();

            var yearTransactions = await _context.CashTransactions
                .AsNoTracking()
                .Where(x => x.TransactionDate >= yearStart && x.TransactionDate < yearEnd)
                .ToListAsync();

            var vm = new FinanceDashboardViewModel
            {
                SelectedMonth = selectedMonth,
                SelectedYear = selectedYear,
                AvailableYears = availableYears,
                PeriodCashIn = periodTransactions.Where(x => x.Type == "IN").Sum(x => x.Amount),
                PeriodCashOut = periodTransactions.Where(x => x.Type == "OUT").Sum(x => x.Amount),
                PeriodSalesIncome = periodTransactions
                    .Where(x => x.Type == "IN" && (x.SaleId != null || saleIncomeCategories.Contains(x.Category)))
                    .Sum(x => x.Amount),
                PeriodTextileExpense = periodTransactions.Where(x => x.Type == "OUT" && purchaseCategories.Contains(x.Category)).Sum(x => x.Amount),
                PeriodPackagingFee = periodTransactions.Where(x => x.Type == "OUT" && packagingCategories.Contains(x.Category)).Sum(x => x.Amount),
                PeriodTransportationExpense = periodTransactions.Where(x => x.Type == "OUT" && transportationCategories.Contains(x.Category)).Sum(x => x.Amount),
                PeriodLivingExpense = periodTransactions.Where(x => x.Type == "OUT" && x.Category == "Living Expense").Sum(x => x.Amount),
                PeriodOtherExpense = periodTransactions.Where(x => x.Type == "OUT"
                    && !purchaseCategories.Contains(x.Category)
                    && !packagingCategories.Contains(x.Category)
                    && !transportationCategories.Contains(x.Category)
                    && x.Category != "Living Expense").Sum(x => x.Amount),
                PreviousMonthCashIn = previousMonthTransactions.Where(x => x.Type == "IN").Sum(x => x.Amount),
                PreviousMonthCashOut = previousMonthTransactions.Where(x => x.Type == "OUT").Sum(x => x.Amount),
                RecentTransactions = await _context.CashTransactions
                    .AsNoTracking()
                    .OrderByDescending(x => x.TransactionDate)
                    .ThenByDescending(x => x.Id)
                    .Take(10)
                    .Select(x => new FinanceRecentTransactionDto
                    {
                        TransactionDate = x.TransactionDate,
                        Type = x.Type,
                        Category = x.Category,
                        Amount = x.Amount,
                        ReferenceNo = x.ReferenceNo ?? string.Empty,
                        Remarks = x.Remarks ?? string.Empty
                    })
                    .ToListAsync()
            };

            vm.PeriodNetCashFlow = vm.PeriodCashIn - vm.PeriodCashOut;
            vm.OperatingExpense = vm.PeriodTextileExpense + vm.PeriodPackagingFee + vm.PeriodTransportationExpense + vm.PeriodOtherExpense;
            vm.OwnerDrawings = vm.PeriodLivingExpense;
            vm.GrossProfitEstimate = vm.PeriodSalesIncome - vm.OperatingExpense;
            vm.PreviousMonthNetCashFlow = vm.PreviousMonthCashIn - vm.PreviousMonthCashOut;

            vm.MonthlyTrend = Enumerable.Range(0, 12)
                .Select(i => yearStart.AddMonths(i))
                .Select(m =>
                {
                    var monthTransactions = yearTransactions.Where(x => x.TransactionDate.Year == m.Year && x.TransactionDate.Month == m.Month).ToList();
                    var cashIn = monthTransactions.Where(x => x.Type == "IN").Sum(x => x.Amount);
                    var cashOut = monthTransactions.Where(x => x.Type == "OUT").Sum(x => x.Amount);

                    return new FinanceTrendPointDto
                    {
                        Label = m.ToString("MMM"),
                        CashIn = cashIn,
                        CashOut = cashOut,
                        NetCashFlow = cashIn - cashOut
                    };
                })
                .ToList();

            var expenseItems = new[]
            {
                new FinanceCategoryBreakdownDto { Category = "Inventory Purchase", Amount = vm.PeriodTextileExpense },
                new FinanceCategoryBreakdownDto { Category = "Packaging Fee", Amount = vm.PeriodPackagingFee },
                new FinanceCategoryBreakdownDto { Category = "Transportation", Amount = vm.PeriodTransportationExpense },
                new FinanceCategoryBreakdownDto { Category = "Living Expense", Amount = vm.PeriodLivingExpense },
                new FinanceCategoryBreakdownDto { Category = "Other Expense", Amount = vm.PeriodOtherExpense }
            }.Where(x => x.Amount > 0)
             .OrderByDescending(x => x.Amount)
             .ToList();

            var totalExpenseForPeriod = expenseItems.Sum(x => x.Amount);
            vm.ExpenseBreakdown = expenseItems
                .Select(x => new FinanceCategoryBreakdownDto
                {
                    Category = x.Category,
                    Amount = x.Amount,
                    Percentage = totalExpenseForPeriod == 0 ? 0 : (int)Math.Round((double)x.Amount / totalExpenseForPeriod * 100)
                })
                .ToList();

            return View(vm);
        }
    }
}
