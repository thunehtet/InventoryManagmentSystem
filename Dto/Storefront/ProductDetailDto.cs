namespace ClothInventoryApp.Dto.Storefront
{
    public class ProductDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> GalleryImageUrls { get; set; } = new();
        public List<ProductVariantCardDto> Variants { get; set; } = new();
    }

    public class ProductVariantCardDto
    {
        public Guid Id { get; set; }
        public string SKU { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int SellingPrice { get; set; }
        public int Stock { get; set; }
        public int QuantityInCart { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public List<string> ImageUrls { get; set; } = new();
    }
}
