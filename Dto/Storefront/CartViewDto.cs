namespace ClothInventoryApp.Dto.Storefront
{
    public class CartViewDto
    {
        public List<CartLineDto> Lines { get; set; } = new();
        public int Subtotal { get; set; }
        public int ShippingFee { get; set; }
        public int Total => Subtotal + ShippingFee;
    }

    public class CartLineDto
    {
        public Guid VariantId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int Stock { get; set; }

        public int LineTotal => UnitPrice * Quantity;
    }
}
