using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Usage
{
    public class UsageTrackingService : IUsageTrackingService
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UsageTrackingService> _logger;

        public UsageTrackingService(
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor,
            UserManager<ApplicationUser> userManager,
            ILogger<UsageTrackingService> logger)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task TrackLoginAsync(ApplicationUser? user, string attemptedIdentity, bool isSuccess, CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var audit = new UserLoginAudit
                {
                    TenantId = user?.TenantId,
                    UserId = user?.Id,
                    AttemptedIdentity = Trim(attemptedIdentity, 256),
                    IsSuccess = isSuccess,
                    AttemptedAt = now,
                    IpAddress = Trim(GetIpAddress(), 64),
                    UserAgent = Trim(GetUserAgent(), 1024)
                };

                _db.UserLoginAudits.Add(audit);

                if (user != null && isSuccess)
                {
                    await TouchUserAndTenantAsync(user, now, countLogin: true, feature: null, cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track login audit for identity {Identity}.", attemptedIdentity);
            }
        }

        public async Task TrackActionAsync(
            Guid? tenantId,
            string feature,
            string action,
            string entityType,
            string? entityId,
            string? description,
            string? userId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var actor = await ResolveActorAsync(userId, cancellationToken);
                var effectiveTenantId = tenantId ?? actor?.TenantId;

                _db.UserActivityLogs.Add(new UserActivityLog
                {
                    TenantId = effectiveTenantId,
                    UserId = actor?.Id ?? userId,
                    Feature = Trim(feature, 100) ?? "general",
                    Action = Trim(action, 50) ?? "unknown",
                    EntityType = Trim(entityType, 100) ?? "unknown",
                    EntityId = Trim(entityId, 100),
                    Description = Trim(description, 500),
                    CreatedAt = now,
                    IpAddress = Trim(GetIpAddress(), 64)
                });

                if (actor != null)
                {
                    await TouchUserAndTenantAsync(actor, now, countLogin: false, feature, cancellationToken);
                }
                else if (effectiveTenantId.HasValue)
                {
                    await TouchTenantOnlyAsync(effectiveTenantId.Value, now, cancellationToken);
                    await IncrementDailyUsageAsync(effectiveTenantId.Value, null, now, feature, false, cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track action {Feature}/{Action} for entity {EntityType}:{EntityId}.", feature, action, entityType, entityId);
            }
        }

        private async Task<ApplicationUser?> ResolveActorAsync(string? userId, CancellationToken cancellationToken)
        {
            var effectiveUserId = userId;
            if (string.IsNullOrWhiteSpace(effectiveUserId))
            {
                effectiveUserId = _userManager.GetUserId(_httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal());
            }

            if (string.IsNullOrWhiteSpace(effectiveUserId))
            {
                return null;
            }

            return await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == effectiveUserId, cancellationToken);
        }

        private async Task TouchUserAndTenantAsync(ApplicationUser user, DateTime now, bool countLogin, string? feature, CancellationToken cancellationToken)
        {
            var dbUser = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
            if (dbUser == null)
            {
                return;
            }

            var previousLastActivityAt = dbUser.LastActivityAt;

            if (countLogin)
            {
                dbUser.LastLoginAt = now;
            }

            dbUser.LastActivityAt = now;

            if (!dbUser.IsSuperAdmin)
            {
                var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == dbUser.TenantId, cancellationToken);
                if (tenant != null)
                {
                    tenant.LastActivityAt = now;
                }

                await IncrementDailyUsageAsync(dbUser.TenantId, previousLastActivityAt, now, feature, countLogin, cancellationToken);
            }
        }

        private async Task TouchTenantOnlyAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
            if (tenant != null)
            {
                tenant.LastActivityAt = now;
            }
        }

        private async Task IncrementDailyUsageAsync(
            Guid tenantId,
            DateTime? previousLastActivityAt,
            DateTime now,
            string? feature,
            bool countLogin,
            CancellationToken cancellationToken)
        {
            var day = now.Date;
            var usage = await _db.TenantDailyUsages
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UsageDate == day, cancellationToken);

            if (usage == null)
            {
                usage = new TenantDailyUsage
                {
                    TenantId = tenantId,
                    UsageDate = day
                };
                _db.TenantDailyUsages.Add(usage);
            }

            if (!previousLastActivityAt.HasValue || previousLastActivityAt.Value.Date != day)
            {
                usage.ActiveUsers++;
            }

            if (countLogin)
            {
                usage.LoginCount++;
            }

            if (!string.IsNullOrWhiteSpace(feature))
            {
                usage.TotalActionCount++;
                switch (feature.Trim().ToLowerInvariant())
                {
                    case "users":
                        usage.UserActionCount++;
                        break;
                    case "products":
                        usage.ProductActionCount++;
                        break;
                    case "variants":
                        usage.VariantActionCount++;
                        break;
                    case "stock":
                        usage.StockActionCount++;
                        break;
                    case "sales":
                        usage.SaleActionCount++;
                        break;
                    case "customers":
                        usage.CustomerActionCount++;
                        break;
                    case "cashtransactions":
                        usage.CashTransactionActionCount++;
                        break;
                }
            }

            usage.LastActivityAt = now;
        }

        private string? GetIpAddress()
            => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent()
            => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

        private static string? Trim(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }
    }
}
