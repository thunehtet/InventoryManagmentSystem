namespace ClothInventoryApp.Services.Subscription
{
    public interface ISubscriptionService
    {
        Task<bool> IsSubscriptionActiveAsync(Guid tenantId);

        Task<(int Current, int? Max)> GetUserLimitAsync(Guid tenantId);
        Task<(int Current, int? Max)> GetProductLimitAsync(Guid tenantId);
        Task<(int Current, int? Max)> GetVariantLimitAsync(Guid tenantId);

        Task<bool> CanAddUserAsync(Guid tenantId);
        Task<bool> CanAddProductAsync(Guid tenantId);
        Task<bool> CanAddVariantAsync(Guid tenantId);

        // ── Monthly sale transaction limit ───────────────────────────
        Task<(int Current, int? Max)> GetMonthlySaleUsageAsync(Guid tenantId);
        Task<bool> CanCreateSaleAsync(Guid tenantId);

        // ── Per-feature monthly usage (pdf_invoice, receipt_share, customer_invite) ──
        Task<(int Used, int? Max)> GetFeatureUsageAsync(Guid tenantId, string feature);
        Task<bool> CanUseFeatureAsync(Guid tenantId, string feature);
        Task IncrementFeatureUsageAsync(Guid tenantId, string feature);

        // Resets all tracked feature usage counts to 0 for the current month.
        // Call whenever a tenant is downgraded to the free plan.
        Task ResetMonthlyFeatureUsageAsync(Guid tenantId);
    }
}
