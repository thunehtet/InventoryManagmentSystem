using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dtos.Tenant
{
    public class TenantEditDto
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Business Type")]
        public string? BusinessType { get; set; }

        [MaxLength(300)]
        public string? LegalName { get; set; }

        [MaxLength(500)]
        [Display(Name = "Logo URL")]
        public string? LogoUrl { get; set; }

        [EmailAddress]
        [Display(Name = "Contact Email")]
        public string? ContactEmail { get; set; }

        [Display(Name = "Contact Phone")]
        public string? ContactPhone { get; set; }

        public string? Country { get; set; }
        public string? CurrencyCode { get; set; }
        public string? TimeZoneId { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; }
    }
}
