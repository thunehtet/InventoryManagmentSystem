using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        //  Link to Tenant (Company)
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        //  User Info
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        //  Role Flags (simple version)
        public bool IsTenantAdmin { get; set; } = false;
        public bool IsSuperAdmin { get; set; } = false;

        public string Type { get; set; }

        //  Status
        public bool IsActive { get; set; } = true;

        //  Audit Fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }

       
        public string? ProfileImageUrl { get; set; }
    }
}