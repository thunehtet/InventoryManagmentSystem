using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class UploadedFile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [MaxLength(450)]
        public string? UploadedByUserId { get; set; }
        public ApplicationUser? UploadedByUser { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Extension { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        [Required]
        [MaxLength(500)]
        public string RelativePath { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
