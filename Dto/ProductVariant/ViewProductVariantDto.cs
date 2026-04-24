namespace ClothInventoryApp.Dto.ProductVariant
{
    public class ViewProductVariantDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int CostPrice { get; set; }
        public int SellingPrice { get; set; }
        public List<ProductVariantImageDto> Images { get; set; } = new();
        public List<Microsoft.AspNetCore.Http.IFormFile> NewImages { get; set; } = new();
    }

    public class ProductVariantImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
    }
}
