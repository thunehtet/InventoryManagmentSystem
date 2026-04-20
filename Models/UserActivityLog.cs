using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class UserActivityLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string Feature { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EntityId { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? IpAddress { get; set; }
    }
}
