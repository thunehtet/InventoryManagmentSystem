using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class PlanFeatureCreateDto
    {
        [Required]
        [Display(Name = "Plan")]
        public Guid PlanId { get; set; }

        [Required]
        [Display(Name = "Feature")]
        public Guid FeatureId { get; set; }

        [Display(Name = "Enabled")]
        public bool IsEnabled { get; set; } = true;
    }
}
