using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.Customer
{
    public class CreateCustomerDto
    {
        [Required]
        [MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(30)]
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        [MaxLength(200)]
        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [MaxLength(200)]
        [Display(Name = "Facebook Account")]
        public string? FacebookAccount { get; set; }

        [MaxLength(500)]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        [MaxLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }

    public class EditCustomerDto : CreateCustomerDto
    {
        public Guid Id { get; set; }
    }

    public class ViewCustomerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? FacebookAccount { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalSales { get; set; }
        public int TotalRevenue { get; set; }
    }
}
