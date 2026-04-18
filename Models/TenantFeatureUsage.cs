namespace ClothInventoryApp.Models
{
    public class TenantFeatureUsage
    {
        public Guid TenantId { get; set; }
        public string YearMonth { get; set; } = string.Empty; // "2025-04"
        public string Feature { get; set; } = string.Empty;   // pdf_invoice | receipt_share | customer_invite
        public int UsageCount { get; set; }

        public Tenant Tenant { get; set; } = null!;
    }
}
