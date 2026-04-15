using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class CustomerInviteLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [MaxLength(64)]
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; }

        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }
}
