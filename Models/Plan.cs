using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class Plan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public decimal PriceMonthly { get; set; }
        public decimal? PriceYearly { get; set; }

        public int? MaxUsers { get; set; }
        public int? MaxProducts { get; set; }
        public int? MaxStorageMb { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
        public ICollection<TenantSubscription> TenantSubscriptions { get; set; } = new List<TenantSubscription>();
    }
}