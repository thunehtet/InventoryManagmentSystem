using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ClothInventoryApp.Dtos.ApplicationUser
{
    public class ApplicationUserEditDto
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Tenant")]
        public Guid TenantId { get; set; }

        [Display(Name = "New Password")]
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [Display(Name = "Tenant Admin")]
        public bool IsTenantAdmin { get; set; }

        [Display(Name = "Super Admin")]
        public bool IsSuperAdmin { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }

        public List<SelectListItem> Tenants { get; set; } = new();
    }
}