namespace ClothInventoryApp.Models.ViewModels
{
    public class HomeOverviewViewModel
    {
        public string TenantName { get; set; } = string.Empty;
        public string? TenantLogoUrl { get; set; }

        public int TotalProducts { get; set; }
        public int TotalVariants { get; set; }
        public int LowStockCount { get; set; }

        public List<RecentInventoryActivityItem> RecentActivities { get; set; } = new();
    }

    public class RecentInventoryActivityItem
    {
        public string SKU { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string QtyText { get; set; } = string.Empty;
        public DateTime MovementDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}