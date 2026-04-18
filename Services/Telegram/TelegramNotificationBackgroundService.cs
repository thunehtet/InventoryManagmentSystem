using ClothInventoryApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Telegram
{
    /// <summary>
    /// Runs daily to send Telegram alerts to tenant admins:
    ///   1. Subscriptions expiring within 3 days
    ///   2. Product variants with stock below the low-stock threshold
    /// Only notifies users who have set a TelegramChatId on their profile.
    /// </summary>
    public class TelegramNotificationBackgroundService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
        private const int LowStockThreshold = 10;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TelegramNotificationBackgroundService> _logger;

        public TelegramNotificationBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<TelegramNotificationBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TelegramNotificationBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAlertsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in TelegramNotificationBackgroundService.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("TelegramNotificationBackgroundService stopped.");
        }

        private async Task RunAlertsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

            await SendExpiryWarningsAsync(db, telegram, ct);
            await SendLowStockAlertsAsync(db, telegram, ct);
        }

        private async Task SendExpiryWarningsAsync(AppDbContext db, ITelegramService telegram, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var warningDate = now.AddDays(3);

            var expiring = await db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Where(s => s.IsActive && s.EndDate >= now && s.EndDate <= warningDate)
                .ToListAsync(ct);

            if (expiring.Count == 0) return;

            var tenantIds = expiring.Select(s => s.TenantId).Distinct().ToList();

            var admins = await db.Users
                .IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId) && u.IsTenantAdmin && u.TelegramChatId != null)
                .Select(u => new { u.TenantId, u.TelegramChatId, u.FullName })
                .ToListAsync(ct);

            foreach (var sub in expiring)
            {
                var admin = admins.FirstOrDefault(a => a.TenantId == sub.TenantId);
                if (admin?.TelegramChatId == null) continue;

                var daysLeft = (int)Math.Ceiling((sub.EndDate - now).TotalDays);
                var msg = $"⚠️ StockEasy Alert\n\n" +
                          $"Hello {admin.FullName},\n\n" +
                          $"Your {sub.Plan.Name} subscription expires in {daysLeft} day(s) " +
                          $"(on {sub.EndDate:yyyy-MM-dd}).\n\n" +
                          $"Please renew to keep your full access.";

                var sent = await telegram.SendMessageAsync(admin.TelegramChatId, msg, ct);
                if (sent)
                    _logger.LogInformation(
                        "Sent expiry warning to tenant {TenantId} (expires {EndDate}).",
                        sub.TenantId, sub.EndDate);
            }
        }

        private async Task SendLowStockAlertsAsync(AppDbContext db, ITelegramService telegram, CancellationToken ct)
        {
            var lowStockVariants = await db.ProductVariants
                .IgnoreQueryFilters()
                .Select(v => new
                {
                    v.TenantId,
                    ProductName = v.Product.Name,
                    v.Color,
                    v.Size,
                    CurrentStock = v.StockMovements.Sum(m =>
                        (int?)(m.MovementType == "IN" ? m.Quantity : m.MovementType == "OUT" ? -m.Quantity : 0)) ?? 0
                })
                .Where(x => x.CurrentStock < LowStockThreshold && x.CurrentStock >= 0)
                .ToListAsync(ct);

            if (lowStockVariants.Count == 0) return;

            var tenantIds = lowStockVariants.Select(v => v.TenantId).Distinct().ToList();

            var admins = await db.Users
                .IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId) && u.IsTenantAdmin && u.TelegramChatId != null)
                .Select(u => new { u.TenantId, u.TelegramChatId, u.FullName })
                .ToListAsync(ct);

            var byTenant = lowStockVariants.GroupBy(v => v.TenantId);

            foreach (var group in byTenant)
            {
                var admin = admins.FirstOrDefault(a => a.TenantId == group.Key);
                if (admin?.TelegramChatId == null) continue;

                var lines = group.Take(10).Select(v =>
                    $"• {v.ProductName} [{v.Color}/{v.Size}] — {v.CurrentStock} left");

                var more = group.Count() > 10 ? $"\n...and {group.Count() - 10} more." : string.Empty;

                var msg = $"📦 StockEasy Low Stock Alert\n\n" +
                          $"Hello {admin.FullName},\n\n" +
                          $"The following items are below {LowStockThreshold} units:\n\n" +
                          string.Join("\n", lines) + more +
                          "\n\nPlease restock soon.";

                var sent = await telegram.SendMessageAsync(admin.TelegramChatId, msg, ct);
                if (sent)
                    _logger.LogInformation(
                        "Sent low-stock alert to tenant {TenantId} ({Count} variants).",
                        group.Key, group.Count());
            }
        }
    }
}
