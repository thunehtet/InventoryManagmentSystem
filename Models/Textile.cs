namespace ClothInventoryApp.Models
{
    public class Textile
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string Name { get; set; } = "";
        public string PurchaseFrom { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int UnitPrice { get; set; }

        public int TotalPrice { get; set; }
    }
}
