using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class TenantFeatureOverrideIndexItemDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string ActivePlanName { get; set; } = "No Plan";
        public int OverrideCount { get; set; }
        public bool IsActive { get; set; }
    }

    public class TenantFeatureOverrideManageDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public string ActivePlanName { get; set; } = "No Plan";
        public List<TenantFeatureOverrideItemDto> Items { get; set; } = new();
    }

    public class TenantFeatureOverrideItemDto
    {
        public Guid FeatureId { get; set; }
        public string FeatureName { get; set; } = string.Empty;
        public string FeatureCode { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool PlanEnabled { get; set; }
        public bool EffectiveEnabled { get; set; }

        [Required]
        public string OverrideMode { get; set; } = "Default";
    }
}
