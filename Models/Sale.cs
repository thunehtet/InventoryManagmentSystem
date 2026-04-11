namespace ClothInventoryApp.Models
{
    public class Sale
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public DateTime SaleDate { get; set; }
        public int TotalAmount { get; set; }

        public List<SaleItem> Items { get; set; } = new();
    }
}