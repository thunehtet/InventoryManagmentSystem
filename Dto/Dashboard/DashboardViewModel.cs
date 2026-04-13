namespace ClothInventoryApp.Dto.Dashboard
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalVariants { get; set; }
        public int LowStockCount { get; set; }

        public List<RecentActivityDto> RecentActivities { get; set; } = new();

        // Both datasets always loaded — JS switches between them (no page reload)
        public List<SalesTrendPoint> DailyTrend   { get; set; } = new();
        public List<SalesTrendPoint> MonthlyTrend { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public string SKU         { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string Action      { get; set; } = "";
        public int    Quantity    { get; set; }
        public DateTime Date      { get; set; }
        public string Status      { get; set; } = "";
    }

    public class SalesTrendPoint
    {
        public string Label  { get; set; } = "";
        public int    Amount { get; set; }
        public int    Profit { get; set; }
    }
}
