using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Files
{
    public class LocalFileStorageService : IFileStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png"
        };

        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };

        private const long MaxImageSizeBytes = 5 * 1024 * 1024;

        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _db;

        public LocalFileStorageService(IWebHostEnvironment environment, IConfiguration configuration, AppDbContext db)
        {
            _environment = environment;
            _configuration = configuration;
            _db = db;
        }

        public async Task<UploadedFile> SaveImageAsync(
            IFormFile file,
            string category,
            string? uploadedByUserId,
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            ValidateImage(file, category);

            var extension = Path.GetExtension(file.FileName);
            var safeExtension = extension.ToLowerInvariant();
            var storedFileName = $"{Guid.NewGuid():N}{safeExtension}";
            var relativeDirectory = Path.Combine("uploads", category, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
            var absoluteDirectory = Path.Combine(GetStorageRootPath(), relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var absolutePath = Path.Combine(absoluteDirectory, storedFileName);
            await using (var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var uploadedFile = new UploadedFile
            {
                TenantId = tenantId,
                UploadedByUserId = uploadedByUserId,
                OriginalFileName = Path.GetFileName(file.FileName),
                StoredFileName = storedFileName,
                ContentType = file.ContentType,
                Extension = safeExtension,
                SizeBytes = file.Length,
                RelativePath = Path.Combine(relativeDirectory, storedFileName).Replace("\\", "/"),
                Category = category
            };

            _db.UploadedFiles.Add(uploadedFile);
            await _db.SaveChangesAsync(cancellationToken);
            return uploadedFile;
        }

        public string GetPublicUrl(UploadedFile file) => "/" + file.RelativePath.TrimStart('/');

        private string GetStorageRootPath()
        {
            var configuredRoot = _configuration["FILE_STORAGE_ROOT"];
            if (!string.IsNullOrWhiteSpace(configuredRoot))
            {
                return configuredRoot;
            }

            return _environment.WebRootPath;
        }

        private static void ValidateImage(IFormFile file, string category)
        {
            if (!UploadCategories.All.Contains(category))
                throw new InvalidOperationException("Unsupported upload category.");

            if (file == null || file.Length == 0)
                throw new InvalidOperationException("Please choose an image file.");

            if (file.Length > MaxImageSizeBytes)
                throw new InvalidOperationException("Image file is too large. Maximum allowed size is 5 MB.");

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
                throw new InvalidOperationException("Only JPG, JPEG, and PNG image files are allowed.");

            if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
                throw new InvalidOperationException("Unsupported image content type.");

            if (!HasAllowedImageSignature(file))
                throw new InvalidOperationException("The uploaded file content does not match a supported image format.");
        }

        private static bool HasAllowedImageSignature(IFormFile file)
        {
            Span<byte> header = stackalloc byte[8];
            using var stream = file.OpenReadStream();
            var bytesRead = stream.Read(header);

            var isJpeg = bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF;

            var isPng = bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A;

            return isJpeg || isPng;
        }
    }
}
