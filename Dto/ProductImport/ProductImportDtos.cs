using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Dto.ProductImport
{
    public class ProductImportUploadDto
    {
        public IFormFile? File { get; set; }
        public ProductImportResultDto? Result { get; set; }
    }

    public class ProductImportResultDto
    {
        public int TotalRows { get; set; }
        public int ImportedRows { get; set; }
        public int SkippedRows { get; set; }
        public int CreatedProducts { get; set; }
        public int CreatedVariants { get; set; }
        public int CreatedStockMovements { get; set; }
        public List<ProductImportRowResultDto> Rows { get; set; } = new();
    }

    public class ProductImportRowResultDto
    {
        public int RowNumber { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Variant { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
