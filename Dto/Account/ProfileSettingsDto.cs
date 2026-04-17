using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dtos.Account
{
    public class ProfileSettingsDto
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Profile Photo")]
        public IFormFile? ProfileImage { get; set; }

        [Display(Name = "Brand Logo")]
        public IFormFile? BrandLogoImage { get; set; }

        public string? CurrentProfileImageUrl { get; set; }
        public string? CurrentBrandLogoUrl { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsTenantAdmin { get; set; }
        public bool CanChangeLoginIdentity { get; set; }
        public DateTime? LoginIdentityChangedAt { get; set; }
    }
}
