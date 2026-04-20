namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class SuperAdminDashboardDto
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int MonthlyRevenue { get; set; }
        public int NewTenantsThisMonth { get; set; }
        public int TodayLoginCount { get; set; }
        public int TodayActionCount { get; set; }
        public int ActiveTenants30Days { get; set; }
        public int DormantTenants { get; set; }

        public List<PlanDistributionItem> PlanDistribution { get; set; } = new();
        public List<RecentTenantItem> RecentTenants { get; set; } = new();
        public List<TenantUsageCardDto> TopTenantUsage { get; set; } = new();
        public List<TenantUsageCardDto> DormantTenantList { get; set; } = new();
        public List<RecentUserActivityDto> RecentActivities { get; set; } = new();
    }

    public class PlanDistributionItem
    {
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RecentTenantItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ActivePlan { get; set; }
    }
}
