using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class UserLoginAudit
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [MaxLength(256)]
        public string? AttemptedIdentity { get; set; }

        public bool IsSuccess { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(64)]
        public string? IpAddress { get; set; }

        [MaxLength(1024)]
        public string? UserAgent { get; set; }
    }
}
