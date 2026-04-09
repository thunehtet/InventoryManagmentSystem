namespace ClothInventoryApp.Dto.Dashboard
{
    public class DashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalVariants { get; set; }
        public int LowStockCount { get; set; }

        public List<RecentActivityDto> RecentActivities { get; set; } = new();
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
}