using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Stock
{
    public class CreateStockDto
    {
        public Guid Id { get; set; }

        [Required]
        [Display(Name = "Product Variant")]
        public Guid ProductVariantId { get; set; }

        [Required]
        [Display(Name = "Movement Type")]
        public string MovementType { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Display(Name = "Movement Date")]
        [DataType(DataType.Date)]
        public DateTime MovementDate { get; set; } = DateTime.Today;

        public string? Remarks { get; set; }
    }
}