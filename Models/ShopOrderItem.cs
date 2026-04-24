using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class ShopOrderItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid ShopOrderId { get; set; }
        public ShopOrder ShopOrder { get; set; } = null!;

        public Guid? ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

        [MaxLength(255)] public string ProductNameSnapshot { get; set; } = string.Empty;
        [MaxLength(255)] public string ProductSkuSnapshot { get; set; } = string.Empty;
        [MaxLength(255)] public string ProductColorSnapshot { get; set; } = string.Empty;
        [MaxLength(255)] public string ProductSizeSnapshot { get; set; } = string.Empty;

        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int LineTotal { get; set; }
    }
}
