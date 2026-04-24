namespace ClothInventoryApp.Dto.Storefront
{
    public class ProductCardDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int MinPrice { get; set; }
        public int MaxPrice { get; set; }
        public int TotalStock { get; set; }
        public int VariantCount { get; set; }
    }
}
