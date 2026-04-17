namespace ClothInventoryApp.Dto.Dashboard
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalVariants { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int TodaySalesAmount { get; set; }
        public int CurrentMonthSalesAmount { get; set; }
        public int CurrentMonthProfitAmount { get; set; }
        public int CurrentMonthOrderCount { get; set; }
        public int SelectedMonth { get; set; }
        public int SelectedYear { get; set; }
        public List<int> AvailableYears { get; set; } = new();

        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public List<AttentionItemDto> AttentionItems { get; set; } = new();

        public List<SalesTrendPoint> DailyTrend { get; set; } = new();
        public List<SalesTrendPoint> MonthlyTrend { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public string SKU { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Action { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
    }

    public class AttentionItemDto
    {
        public string Title { get; set; } = "";
        public string Value { get; set; } = "";
        public string Note { get; set; } = "";
        public string ActionText { get; set; } = "";
        public string Controller { get; set; } = "";
        public string Action { get; set; } = "";
        public string? RouteValue { get; set; }
        public string Tone { get; set; } = "blue";
    }

    public class SalesTrendPoint
    {
        public string Label { get; set; } = "";
        public int Amount { get; set; }
        public int Profit { get; set; }
    }
}
