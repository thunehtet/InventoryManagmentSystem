namespace ClothInventoryApp.Models
{
    public class Sale
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public DateTime SaleDate { get; set; }
        public int TotalAmount { get; set; }
        public int TotalProfit { get; set; }
        public int Discount { get; set; } = 0;
        public string? PublicReceiptToken { get; set; }

        public List<SaleItem> Items { get; set; } = new();
    }
}
