using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Dto.ProductVariant
{
    public class CreateProductVariantDto
    {
        public Guid Id { get; set; }

        [Required]
        [Display(Name = "Product")]
        public Guid ProductId { get; set; }

        [StringLength(100)]
        [Display(Name = "Internal Code / Barcode")]
        public string SKU { get; set; } = string.Empty;

        [StringLength(50)]
        public string Size { get; set; } = string.Empty;

        [StringLength(50)]
        public string Color { get; set; } = string.Empty;

        [Range(0, 999999999)]
        [Display(Name = "Cost Price")]
        public int CostPrice { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Selling Price")]
        public int SellingPrice { get; set; }

        [Display(Name = "Variant images")]
        public List<IFormFile> Images { get; set; } = new();
    }
}
