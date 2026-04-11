namespace ClothInventoryApp.Dto
{
    public class InventoryViewModel
    {
        public Guid ProductVariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
    }
}