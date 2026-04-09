using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Sale
{
    public class CreateSaleItemDto
    {
        [Required]
        [Display(Name = "Product Variant")]
        public int ProductVariantId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Display(Name = "Unit Price")]
        [Range(0, int.MaxValue)]
        public int UnitPrice { get; set; }

        [Display(Name = "Cost Price")]
        [Range(0, int.MaxValue)]
        public int CostPrice { get; set; }
    }
}