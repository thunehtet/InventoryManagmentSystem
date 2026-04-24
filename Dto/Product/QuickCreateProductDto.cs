using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Dto.Product
{
    public class QuickCreateProductDto
    {
        [Required]
        [StringLength(150)]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Category { get; set; }

        [StringLength(100)]
        public string? Brand { get; set; }

        [Range(0, 999999999)]
        [Display(Name = "Cost Price")]
        public int CostPrice { get; set; }

        [Range(1, 999999999)]
        [Display(Name = "Selling Price")]
        public int SellingPrice { get; set; }

        [Range(0, 999999)]
        [Display(Name = "Opening Stock")]
        public int OpeningStock { get; set; }

        [Display(Name = "Product Photo")]
        public IFormFile? Image { get; set; }
    }
}
