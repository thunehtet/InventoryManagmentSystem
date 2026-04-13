using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class SubscriptionCreateDto
    {
        [Required]
        [Display(Name = "Tenant")]
        public Guid TenantId { get; set; }

        [Required]
        [Display(Name = "Plan")]
        public Guid PlanId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.UtcNow.Date.AddMonths(1);

        [Required]
        [Display(Name = "Billing Cycle")]
        public string BillingCycle { get; set; } = "Monthly";

        [Required]
        [Range(0, int.MaxValue)]
        public int Price { get; set; }

        [Display(Name = "Trial")]
        public bool IsTrial { get; set; } = false;

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class SubscriptionEditDto : SubscriptionCreateDto
    {
        public Guid Id { get; set; }
    }
}
