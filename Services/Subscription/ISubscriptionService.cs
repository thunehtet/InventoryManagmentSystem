namespace ClothInventoryApp.Services.Subscription
{
    public interface ISubscriptionService
    {
        Task<bool> IsSubscriptionActiveAsync(Guid tenantId);

        /// <summary>Returns (currentCount, planMax) for users. planMax=null means unlimited.</summary>
        Task<(int Current, int? Max)> GetUserLimitAsync(Guid tenantId);

        /// <summary>Returns (currentCount, planMax) for products. planMax=null means unlimited.</summary>
        Task<(int Current, int? Max)> GetProductLimitAsync(Guid tenantId);

        /// <summary>True if tenant can add one more user under their active plan.</summary>
        Task<bool> CanAddUserAsync(Guid tenantId);

        /// <summary>True if tenant can add one more product under their active plan.</summary>
        Task<bool> CanAddProductAsync(Guid tenantId);
    }
}
