using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(200)]
        public string? ContactEmail { get; set; }

        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        [MaxLength(20)]
        public string? CurrencyCode { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } 
        public DateTime? UpdatedAt { get; set; }

        public List<ApplicationUser> Users { get; set; } = new();

        public TenantSetting? TenantSetting { get; set; }
        public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
        public ICollection<TenantFeatureOverride> FeatureOverrides { get; set; } = new List<TenantFeatureOverride>();
    }
}