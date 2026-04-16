using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ClothInventoryApp.Dto.SubscriptionRequest
{
    public class CreateSubscriptionPaymentRequestDto
    {
        [Required]
        public Guid PlanId { get; set; }

        [Required]
        [RegularExpression("Monthly|Yearly", ErrorMessage = "Invalid billing cycle.")]
        public string BillingCycle { get; set; } = "Monthly";

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Last 6 transaction ID must be exactly 6 characters.")]
        public string Last6TransactionId { get; set; } = string.Empty;

        [Required]
        public IFormFile? PaymentProof { get; set; }
    }

    public class SubscriptionRequestPlanCardDto
    {
        public Guid PlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PriceMonthly { get; set; }
        public int? PriceYearly { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxProducts { get; set; }
        public int? MaxVariants { get; set; }
        public bool IsCurrent { get; set; }
        public bool HasPendingRequest { get; set; }
    }

    public class SubscriptionPaymentPageDto
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanCode { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = "Monthly";
        public int Price { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string WalletDisplayName { get; set; } = string.Empty;
        public string WalletAccountName { get; set; } = string.Empty;
        public string WalletAccountNo { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public CreateSubscriptionPaymentRequestDto Form { get; set; } = new();
    }
}
