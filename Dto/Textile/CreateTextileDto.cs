using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Textile
{
    public class CreateTextileDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Purchase From")]
        public string PurchaseFrom { get; set; } = string.Empty;

        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Display(Name = "Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        [Display(Name = "Unit Price")]
        [Range(0, int.MaxValue)]
        public int UnitPrice { get; set; }
    }
}