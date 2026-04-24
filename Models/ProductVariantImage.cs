using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class ProductVariantImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Guid ProductVariantId { get; set; }
        public ProductVariant ProductVariant { get; set; } = null!;

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
