using System.ComponentModel.DataAnnotations;

namespace ClothInventoryApp.Models
{
    public class SubscriptionPaymentRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [MaxLength(450)]
        public string RequestedByUserId { get; set; } = string.Empty;
        public ApplicationUser RequestedByUser { get; set; } = null!;

        public Guid PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string PlanNameSnapshot { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string BillingCycle { get; set; } = "Monthly";

        public int ExpectedPrice { get; set; }

        [Required]
        [MaxLength(6)]
        public string Last6TransactionId { get; set; } = string.Empty;

        public Guid PaymentProofFileId { get; set; }
        public UploadedFile PaymentProofFile { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(450)]
        public string? ReviewedByUserId { get; set; }
        public ApplicationUser? ReviewedByUser { get; set; }

        [MaxLength(500)]
        public string? ReviewRemarks { get; set; }

        public Guid? ApprovedSubscriptionId { get; set; }
        public TenantSubscription? ApprovedSubscription { get; set; }
    }
}
