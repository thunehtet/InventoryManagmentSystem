namespace ClothInventoryApp.Models
{
    public class SaleItem
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid SaleId { get; set; }
        public Sale Sale { get; set; } = null!;

        public Guid? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        public string ProductNameSnapshot { get; set; } = string.Empty;
        public string ProductSkuSnapshot { get; set; } = string.Empty;
        public string ProductColorSnapshot { get; set; } = string.Empty;
        public string ProductSizeSnapshot { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int CostPrice { get; set; }
        public int Profit { get; set; }
    }
}
