namespace ClothInventoryApp.Dto.Sale
{
    public class ViewSaleDto
    {
        public Guid Id { get; set; }
        public DateTime SaleDate { get; set; }
        public int TotalAmount { get; set; }
        public int TotalProfit { get; set; }
        public int Discount { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public List<ViewSaleItemDto> Items { get; set; } = new();
    }
}