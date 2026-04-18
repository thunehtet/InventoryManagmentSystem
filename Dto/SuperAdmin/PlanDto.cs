using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class PlanCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Monthly Price")]
        public int PriceMonthly { get; set; }

        [Range(0, int.MaxValue)]
        [Display(Name = "Yearly Price")]
        public int? PriceYearly { get; set; }

        [Display(Name = "Max Users")]
        public int? MaxUsers { get; set; }

        [Display(Name = "Max Products")]
        public int? MaxProducts { get; set; }

        [Display(Name = "Max Monthly Sales")]
        public int? MaxMonthlySales { get; set; }

        [Display(Name = "Max Monthly PDF Invoices")]
        public int? MaxMonthlyPdfInvoices { get; set; }

        [Display(Name = "Max Monthly Receipt Shares")]
        public int? MaxMonthlyReceiptShares { get; set; }

        [Display(Name = "Max Monthly Customer Invites")]
        public int? MaxMonthlyCustomerInvites { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;
    }

    public class PlanEditDto : PlanCreateDto
    {
        public Guid Id { get; set; }
    }
}
