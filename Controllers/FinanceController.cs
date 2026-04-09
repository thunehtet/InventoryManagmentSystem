using ClothInventoryApp.Data;
using ClothInventoryApp.Dto.Finance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Controllers
{
    public class FinanceController : Controller
    {
        private readonly AppDbContext _context;

        public FinanceController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new FinanceDashboardViewModel();

            vm.TotalCashIn = await _context.CashTransactions
                .Where(x => x.Type == "IN")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.TotalCashOut = await _context.CashTransactions
                .Where(x => x.Type == "OUT")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.CashBalance = vm.TotalCashIn - vm.TotalCashOut;

            vm.TotalSalesIncome = await _context.CashTransactions
                .Where(x => x.Type == "IN" && x.Category == "Sale Income")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.TotalTextileExpense = await _context.CashTransactions
                .Where(x => x.Type == "OUT" && x.Category == "Textile Purchase")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.TotalTailorFee = await _context.CashTransactions
                .Where(x => x.Type == "OUT" && x.Category == "Tailor Fee")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.TotalLivingExpense = await _context.CashTransactions
                .Where(x => x.Type == "OUT" && x.Category == "Living Expense")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            vm.TotalOtherExpense = await _context.CashTransactions
                .Where(x => x.Type == "OUT" &&
                            x.Category != "Textile Purchase" &&
                            x.Category != "Tailor Fee" &&
                            x.Category != "Living Expense")
                .SumAsync(x => (int?)x.Amount) ?? 0;

            // Simple estimate:
            vm.GrossProfitEstimate = vm.TotalSalesIncome
                                     - vm.TotalTextileExpense
                                     - vm.TotalTailorFee
                                     - vm.TotalOtherExpense;

            vm.RecentTransactions = await _context.CashTransactions
                .OrderByDescending(x => x.TransactionDate)
                .ThenByDescending(x => x.Id)
                .Take(10)
                .Select(x => new FinanceRecentTransactionDto
                {
                    TransactionDate = x.TransactionDate,
                    Type = x.Type,
                    Category = x.Category,
                    Amount = x.Amount,
                    ReferenceNo = x.ReferenceNo,
                    Remarks = x.Remarks
                })
                .ToListAsync();

            return View(vm);
        }
    }
}