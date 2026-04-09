namespace ClothInventoryApp.Dto.Sale
{
    public class ViewSaleDto
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public int TotalAmount { get; set; }
        public List<ViewSaleItemDto> Items { get; set; } = new();
    }
}