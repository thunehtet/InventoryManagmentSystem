namespace ClothInventoryApp.Models
{
    public class SaleItem
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid SaleId { get; set; }
        public Sale Sale { get; set; } = null!;

        public Guid ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; } = null!;

        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int CostPrice { get; set; }
    }
}