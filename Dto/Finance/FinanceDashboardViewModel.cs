namespace ClothInventoryApp.Dto.Finance
{
    public class FinanceDashboardViewModel
    {
        public int TotalCashIn { get; set; }
        public int TotalCashOut { get; set; }
        public int CashBalance { get; set; }

        public int TotalSalesIncome { get; set; }
        public int TotalTextileExpense { get; set; }
        public int TotalTailorFee { get; set; }
        public int TotalLivingExpense { get; set; }
        public int TotalOtherExpense { get; set; }

        public int GrossProfitEstimate { get; set; }

        public List<FinanceRecentTransactionDto> RecentTransactions { get; set; } = new();
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