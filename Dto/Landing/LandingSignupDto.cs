using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Landing
{
    public class LandingSignupDto
    {
        [Required]
        [MaxLength(200)]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string BusinessType { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Phone { get; set; }
    }
}
