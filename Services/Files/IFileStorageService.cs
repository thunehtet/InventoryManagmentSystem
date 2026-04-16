using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Services.Files
{
    public interface IFileStorageService
    {
        Task<UploadedFile> SaveImageAsync(
            IFormFile file,
            string category,
            string? uploadedByUserId,
            Guid? tenantId,
            CancellationToken cancellationToken = default);

        string GetPublicUrl(UploadedFile file);
    }
}
