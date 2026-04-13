namespace ClothInventoryApp.Dto.Sale
{
    public class ViewSaleItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductVariantId { get; set; }
        public string ProductVariantName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int UnitPrice { get; set; }
        public int CostPrice { get; set; }
        public int LineTotal { get; set; }
        public int LineProfit { get; set; }
    }
}