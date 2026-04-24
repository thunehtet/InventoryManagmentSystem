using ClothInventoryApp.Dto.ProductImport;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Services.ProductImport
{
    public interface IProductImportService
    {
        Task<ProductImportResultDto> ImportAsync(Guid tenantId, IFormFile file, CancellationToken cancellationToken = default);
        byte[] BuildTemplate();
    }
}
