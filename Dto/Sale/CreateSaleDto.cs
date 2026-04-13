using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Sale
{
    public class CreateSaleDto
    {
        [Display(Name = "Sale Date")]
        [DataType(DataType.Date)]
        public DateTime SaleDate { get; set; } = DateTime.Today;

        [Display(Name = "Customer")]
        public Guid? CustomerId { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Discount")]
        public int Discount { get; set; } = 0;

        public List<CreateSaleItemDto> Items { get; set; } = new()
        {
            new CreateSaleItemDto()
        };
    }
}