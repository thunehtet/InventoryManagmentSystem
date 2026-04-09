using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Product
{
    public class CreateProductDto
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string Brand { get; set; } = "";

        public bool IsActive { get; set; } = true;
    }
}
