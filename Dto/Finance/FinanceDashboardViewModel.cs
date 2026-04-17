namespace ClothInventoryApp.Dto.Finance
{
    public class FinanceDashboardViewModel
    {
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public List<int> AvailableYears { get; set; } = new();

        public int PeriodCashIn { get; set; }
        public int PeriodCashOut { get; set; }
        public int PeriodNetCashFlow { get; set; }
        public int PeriodSalesIncome { get; set; }
        public int PeriodTextileExpense { get; set; }
        public int PeriodPackagingFee { get; set; }
        public int PeriodTransportationExpense { get; set; }
        public int PeriodLivingExpense { get; set; }
        public int PeriodOtherExpense { get; set; }
        public int OperatingExpense { get; set; }
        public int OwnerDrawings { get; set; }
        public int GrossProfitEstimate { get; set; }
        public int PreviousMonthNetCashFlow { get; set; }
        public int PreviousMonthCashIn { get; set; }
        public int PreviousMonthCashOut { get; set; }

        public List<FinanceTrendPointDto> MonthlyTrend { get; set; } = new();
        public List<FinanceCategoryBreakdownDto> ExpenseBreakdown { get; set; } = new();
        public List<FinanceRecentTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class FinanceTrendPointDto
    {
        public string Label { get; set; } = "";
        public int CashIn { get; set; }
        public int CashOut { get; set; }
        public int NetCashFlow { get; set; }
    }

    public class FinanceCategoryBreakdownDto
    {
        public string Category { get; set; } = "";
        public int Amount { get; set; }
        public int Percentage { get; set; }
    }

    public class FinanceRecentTransactionDto
    {
        public DateTime TransactionDate { get; set; }
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public int Amount { get; set; }
        public string ReferenceNo { get; set; } = "";
        public string Remarks { get; set; } = "";
    }
}
