namespace ClothInventoryApp.Dto
{
    public class StockInViewModel
    {
        public Guid ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public string? Remarks { get; set; }
    }
}
