namespace ClothInventoryApp.Dto.Sale
{
    public class ViewSaleDto
    {
        public Guid Id { get; set; }
        public DateTime SaleDate { get; set; }
        public int TotalAmount { get; set; }
        public List<ViewSaleItemDto> Items { get; set; } = new();
    }
}