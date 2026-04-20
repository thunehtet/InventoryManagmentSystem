namespace ClothInventoryApp.Models
{
    public class TenantDailyUsage
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public DateTime UsageDate { get; set; }

        public int LoginCount { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalActionCount { get; set; }
        public int UserActionCount { get; set; }
        public int ProductActionCount { get; set; }
        public int VariantActionCount { get; set; }
        public int StockActionCount { get; set; }
        public int SaleActionCount { get; set; }
        public int CustomerActionCount { get; set; }
        public int CashTransactionActionCount { get; set; }

        public DateTime? LastActivityAt { get; set; }
    }
}
