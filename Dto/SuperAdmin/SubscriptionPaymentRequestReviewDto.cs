namespace ClothInventoryApp.Dto.SuperAdmin
{
    public class SubscriptionPaymentRequestReviewDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string RequestedByName { get; set; } = string.Empty;
        public string RequestedByEmail { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = string.Empty;
        public int ExpectedPrice { get; set; }
        public string Last6TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string PaymentProofUrl { get; set; } = string.Empty;
        public string PaymentProofName { get; set; } = string.Empty;
        public string? ReviewRemarks { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByName { get; set; }
    }
}
