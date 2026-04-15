namespace ClothInventoryApp.Services.Stock
{
    public interface IStockService
    {
        Task<int> GetCurrentStockAsync(Guid tenantId, Guid productVariantId);
        Task<Dictionary<Guid, int>> GetCurrentStockMapAsync(Guid tenantId, IEnumerable<Guid> productVariantIds);
        Task<bool> CanApplyMovementAsync(
            Guid tenantId,
            Guid productVariantId,
            string movementType,
            int quantity,
            Guid? excludingMovementId = null);
    }
}
