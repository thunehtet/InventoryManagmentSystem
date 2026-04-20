namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class TenantUsageCardDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public int LoginCount { get; set; }
        public int TotalActions { get; set; }
        public int ActiveUsers { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }

    public class RecentUserActivityDto
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? UserName { get; set; }
        public string Feature { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TenantUsageDetailDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers30Days { get; set; }
        public int LoginCount30Days { get; set; }
        public int TotalActions30Days { get; set; }
        public List<TenantUsageDayDto> DailyUsage { get; set; } = new();
        public List<TenantUserActivityDto> Users { get; set; } = new();
        public List<RecentUserActivityDto> RecentActivities { get; set; } = new();
    }

    public class TenantUsageDayDto
    {
        public DateTime UsageDate { get; set; }
        public int Logins { get; set; }
        public int Actions { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class TenantUserActivityDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsTenantAdmin { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public int LoginCount30Days { get; set; }
        public int ActionCount30Days { get; set; }
    }
}
