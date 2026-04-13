using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class FeatureCreateDto
    {
        [Required]
        [MaxLength(50)]
        [Display(Name = "Feature Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Display(Name = "Feature Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }

    public class FeatureEditDto : FeatureCreateDto
    {
        public Guid Id { get; set; }
    }
}
