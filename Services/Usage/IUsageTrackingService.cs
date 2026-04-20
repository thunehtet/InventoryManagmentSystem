using ClothInventoryApp.Models;

namespace ClothInventoryApp.Services.Usage
{
    public interface IUsageTrackingService
    {
        Task TrackLoginAsync(ApplicationUser? user, string attemptedIdentity, bool isSuccess, CancellationToken cancellationToken = default);
        Task TrackActionAsync(Guid? tenantId, string feature, string action, string entityType, string? entityId, string? description, string? userId = null, CancellationToken cancellationToken = default);
    }
}
