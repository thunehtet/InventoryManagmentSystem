namespace ClothInventoryApp.Models
{
    public class CashTransaction
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public int Amount { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
        // Nullable link back to the sale that generated this transaction (for cascade delete)
        public Guid? SaleId { get; set; }
    }
}
