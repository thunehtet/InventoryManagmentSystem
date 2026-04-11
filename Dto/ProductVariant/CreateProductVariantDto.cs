using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.ProductVariant
{
    public class CreateProductVariantDto
    {
        public Guid Id { get; set; }

        [Required]
        [Display(Name = "Product")]
        public Guid ProductId { get; set; }

        [Required]
        [StringLength(100)]
        public string SKU { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Size { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Color { get; set; } = string.Empty;

        [Range(0, 999999999)]
        [Display(Name = "Cost Price")]
        public int CostPrice { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Selling Price")]
        public int SellingPrice { get; set; }
    }
}