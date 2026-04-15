using ClothInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Stock
{
    public class StockService : IStockService
    {
        private readonly AppDbContext _context;

        public StockService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetCurrentStockAsync(Guid tenantId, Guid productVariantId)
        {
            return await BuildStockQuery(tenantId)
                .Where(x => x.ProductVariantId == productVariantId)
                .SumAsync(x => x.StockDelta);
        }

        public async Task<Dictionary<Guid, int>> GetCurrentStockMapAsync(
            Guid tenantId,
            IEnumerable<Guid> productVariantIds)
        {
            var variantIds = productVariantIds
                .Distinct()
                .ToList();

            if (variantIds.Count == 0)
                return new Dictionary<Guid, int>();

            return await BuildStockQuery(tenantId)
                .Where(x => variantIds.Contains(x.ProductVariantId))
                .GroupBy(x => x.ProductVariantId)
                .Select(g => new
                {
                    ProductVariantId = g.Key,
                    Stock = g.Sum(x => x.StockDelta)
                })
                .ToDictionaryAsync(x => x.ProductVariantId, x => x.Stock);
        }

        public async Task<bool> CanApplyMovementAsync(
            Guid tenantId,
            Guid productVariantId,
            string movementType,
            int quantity,
            Guid? excludingMovementId = null)
        {
            var normalizedType = NormalizeMovementType(movementType);
            if (normalizedType != "OUT")
                return true;

            var stockQuery = BuildStockQuery(tenantId)
                .Where(x => x.ProductVariantId == productVariantId);

            if (excludingMovementId.HasValue)
                stockQuery = stockQuery.Where(x => x.Id != excludingMovementId.Value);

            var currentStock = await stockQuery.SumAsync(x => x.StockDelta);
            return currentStock >= quantity;
        }

        private IQueryable<StockLedgerRow> BuildStockQuery(Guid tenantId)
        {
            return _context.StockMovements
                .Where(x => x.TenantId == tenantId)
                .Select(x => new StockLedgerRow
                {
                    Id = x.Id,
                    ProductVariantId = x.ProductVariantId,
                    StockDelta = x.MovementType == "IN"
                        ? x.Quantity
                        : x.MovementType == "OUT"
                            ? -x.Quantity
                            : 0
                });
        }

        private static string NormalizeMovementType(string? movementType)
        {
            return movementType?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        private sealed class StockLedgerRow
        {
            public Guid Id { get; init; }
            public Guid ProductVariantId { get; init; }
            public int StockDelta { get; init; }
        }
    }
}
