using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Sale
{
    public class CreateSaleDto
    {
        [Display(Name = "Sale Date")]
        [DataType(DataType.Date)]
        public DateTime SaleDate { get; set; } = DateTime.Today;

        public List<CreateSaleItemDto> Items { get; set; } = new()
        {
            new CreateSaleItemDto()
        };
    }
}