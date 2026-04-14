namespace ClothInventoryApp.Models.ViewModels
{
    public class HomeOverviewViewModel
    {
        // ── Tenant ──────────────────────────────────────────────
        public string TenantName { get; set; } = string.Empty;
        public string? TenantLogoUrl { get; set; }

        // ── Metrics ──────────────────────────────────────────────
        public int TotalProducts { get; set; }
        public int TotalVariants { get; set; }
        public int LowStockCount { get; set; }
        public List<RecentInventoryActivityItem> RecentActivities { get; set; } = new();

        // ── Subscription ─────────────────────────────────────────
        public string PlanName { get; set; } = "No Plan";
        public string PlanCode { get; set; } = "";
        public int DaysLeft { get; set; }
        public bool IsTrial { get; set; }
        public bool IsSubscriptionActive { get; set; }

        // ── Usage limits ─────────────────────────────────────────
        public int ProductsCurrent { get; set; }
        public int? ProductsMax { get; set; }
        public int VariantsCurrent { get; set; }
        public int? VariantsMax { get; set; }
        public int UsersCurrent { get; set; }
        public int? UsersMax { get; set; }

        // ── Feature flags ─────────────────────────────────────────
        public bool HasSales { get; set; }
        public bool HasSaleProfit { get; set; }
        public bool HasCustomers { get; set; }
        public bool HasDashboard { get; set; }
        public bool HasFinance { get; set; }
        public bool HasTextiles { get; set; }

        // ── Upgrade info (null = already on top plan) ────────────
        public string? NextPlanName { get; set; }
        public int? NextPlanPrice { get; set; }
        public List<UpgradeFeatureItem> UpgradeFeatures { get; set; } = new();

        // ── Helpers ───────────────────────────────────────────────
        public int ProductUsagePct  => ProductsMax  > 0 ? Math.Min(100, ProductsCurrent  * 100 / ProductsMax!.Value)  : 0;
        public int VariantUsagePct  => VariantsMax  > 0 ? Math.Min(100, VariantsCurrent  * 100 / VariantsMax!.Value)  : 0;
        public int UserUsagePct     => UsersMax     > 0 ? Math.Min(100, UsersCurrent     * 100 / UsersMax!.Value)     : 0;
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

    public class UpgradeFeatureItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}