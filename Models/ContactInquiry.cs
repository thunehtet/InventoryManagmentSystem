using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class ContactInquiry
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string BusinessName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string BusinessType { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(2000)]
        public string? Message { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(450)]
        public string? ReviewedByUserId { get; set; }
        public ApplicationUser? ReviewedByUser { get; set; }

        [MaxLength(500)]
        public string? ReviewRemarks { get; set; }

        public Guid? ApprovedTenantId { get; set; }
        public Tenant? ApprovedTenant { get; set; }
    }
}
