using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Subscription
{
    /// <summary>
    /// Background service that periodically checks for expired subscriptions.
    ///
    /// On each run it:
    ///   1. Finds every TenantSubscription where IsActive=true AND EndDate &lt; UtcNow.
    ///   2. Writes a PastSubscription record (snapshot / audit trail).
    ///   3. Sets the TenantSubscription.IsActive = false.
    ///   4. Creates a new FREE-plan subscription for that tenant so access
    ///      is never completely cut — features simply downgrade to the free tier.
    ///
    /// Runs immediately on startup, then every <see cref="CheckInterval"/>.
    /// Uses IServiceScopeFactory because AppDbContext is scoped (not singleton).
    /// </summary>
    public class SubscriptionExpiryService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionExpiryService> _logger;

        public SubscriptionExpiryService(
            IServiceScopeFactory scopeFactory,
            ILogger<SubscriptionExpiryService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubscriptionExpiryService started.");

            // Run once at startup, then on the regular interval
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredSubscriptionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing expired subscriptions.");
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }

            _logger.LogInformation("SubscriptionExpiryService stopped.");
        }

        private async Task ProcessExpiredSubscriptionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;

            // ── 1. Find expired active subscriptions ──────────────────
            // IgnoreQueryFilters: this service runs without a tenant HTTP context,
            // so CurrentTenantId would be Guid.Empty — we must bypass query filters.
            var expired = await db.TenantSubscriptions
                .IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Include(s => s.Tenant)
                .Where(s => s.IsActive && s.EndDate < now)
                .ToListAsync(ct);

            if (expired.Count == 0)
                return;

            _logger.LogInformation(
                "Found {Count} expired subscription(s). Processing…", expired.Count);

            // ── 2. Resolve FREE plan (fallback if not found: skip auto-assign) ─
            var freePlan = await db.Plans
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Code == "FREE" && p.IsActive, ct);

            if (freePlan == null)
                _logger.LogWarning(
                    "FREE plan not found. Expired tenants will have no active subscription.");

            // ── 3. Process each expired subscription ──────────────────
            foreach (var sub in expired)
            {
                // Archive snapshot
                db.PastSubscriptions.Add(BuildArchiveRecord(sub, now, "Expired"));

                // Deactivate
                sub.IsActive = false;
                sub.UpdatedAt = now;

                // Auto-downgrade to FREE — only if the tenant doesn't already
                // have the free plan active (prevents duplicate free subs)
                if (freePlan != null)
                {
                    var alreadyFree = await db.TenantSubscriptions
                        .IgnoreQueryFilters()
                        .AnyAsync(s =>
                            s.TenantId == sub.TenantId &&
                            s.IsActive &&
                            s.PlanId == freePlan.Id, ct);

                    if (!alreadyFree)
                    {
                        db.TenantSubscriptions.Add(new TenantSubscription
                        {
                            TenantId     = sub.TenantId,
                            PlanId       = freePlan.Id,
                            StartDate    = now,
                            EndDate      = new DateTime(2099, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                            BillingCycle = "Free",
                            Price        = 0,
                            IsTrial      = false,
                            IsActive     = true,
                            Notes        = $"Auto-assigned after '{sub.Plan.Name}' expired on " +
                                           $"{sub.EndDate:yyyy-MM-dd}.",
                            CreatedAt    = now
                        });
                    }
                }

                _logger.LogInformation(
                    "Subscription {SubId} ({TenantName} / {PlanName}) expired — archived and downgraded.",
                    sub.Id, sub.Tenant.Name, sub.Plan.Name);
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Processed {Count} expired subscription(s) successfully.", expired.Count);
        }

        // ── Helper: build a PastSubscription snapshot ─────────────────
        internal static PastSubscription BuildArchiveRecord(
            TenantSubscription sub,
            DateTime archivedAt,
            string reason,
            string? extraNotes = null)
        {
            return new PastSubscription
            {
                TenantId               = sub.TenantId,
                PlanId                 = sub.PlanId,
                OriginalSubscriptionId = sub.Id,
                PlanName               = sub.Plan?.Name   ?? sub.PlanId.ToString(),
                TenantName             = sub.Tenant?.Name ?? sub.TenantId.ToString(),
                BillingCycle           = sub.BillingCycle,
                Price                  = sub.Price,
                IsTrial                = sub.IsTrial,
                StartDate              = sub.StartDate,
                EndDate                = sub.EndDate,
                ArchivedAt             = archivedAt,
                Reason                 = reason,
                Notes                  = extraNotes ?? sub.Notes
            };
        }
    }
}
