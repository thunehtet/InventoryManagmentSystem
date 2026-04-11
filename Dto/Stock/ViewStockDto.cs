namespace ClothInventoryApp.Dto.Stock
{
    public class ViewStockDto
    {
        public Guid Id { get; set; }
        public Guid ProductVariantId { get; set; }
        public string ProductVariantName { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime MovementDate { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}