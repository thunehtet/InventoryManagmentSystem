using ClothInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Subscription
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;

        public SubscriptionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsSubscriptionActiveAsync(Guid tenantId)
        {
            var today = DateTime.UtcNow.Date;

            return await _context.TenantSubscriptions
                .AnyAsync(s =>
                    s.TenantId == tenantId &&
                    s.IsActive &&
                    s.StartDate <= today &&
                    s.EndDate >= today);
        }
    }
}