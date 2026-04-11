using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class Feature
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
        public ICollection<TenantFeatureOverride> TenantFeatureOverrides { get; set; } = new List<TenantFeatureOverride>();
    }
}