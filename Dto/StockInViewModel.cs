namespace ClothInventoryApp.Dto
{
    public class StockInViewModel
    {
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public string Remarks { get; set; } = "";
    }
}
