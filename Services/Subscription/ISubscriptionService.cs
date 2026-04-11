using System;
using System.Threading.Tasks;

namespace ClothInventoryApp.Services.Subscription
{
    public interface ISubscriptionService
    {
        Task<bool> IsSubscriptionActiveAsync(Guid tenantId);
    }
}