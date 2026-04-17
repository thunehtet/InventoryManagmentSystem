using ClothInventoryApp.Data;
using ClothInventoryApp.Services.Tenant;
using Microsoft.EntityFrameworkCore;

namespace ClothInventoryApp.Services.Time
{
    public class TenantTimeService : ITenantTimeService
    {
        private static readonly IReadOnlyDictionary<string, string[]> TimeZoneCandidates =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Myanmar"] = new[] { "Myanmar Standard Time", "Asia/Yangon" },
                ["Burma"] = new[] { "Myanmar Standard Time", "Asia/Yangon" },
                ["Singapore"] = new[] { "Singapore Standard Time", "Asia/Singapore" },
                ["Thailand"] = new[] { "SE Asia Standard Time", "Asia/Bangkok" },
                ["Malaysia"] = new[] { "Singapore Standard Time", "Asia/Kuala_Lumpur" },
                ["Indonesia"] = new[] { "SE Asia Standard Time", "Asia/Jakarta" },
                ["United States"] = new[] { "UTC" }
            };

        private readonly ITenantProvider _tenantProvider;
        private readonly AppDbContext _db;

        public TenantTimeService(ITenantProvider tenantProvider, AppDbContext db)
        {
            _tenantProvider = tenantProvider;
            _db = db;
        }

        public TimeZoneInfo ResolveTimeZone(string? country)
        {
            if (!string.IsNullOrWhiteSpace(country))
            {
                if (TimeZoneCandidates.TryGetValue(country.Trim(), out var candidates))
                {
                    foreach (var candidate in candidates)
                    {
                        try
                        {
                            return TimeZoneInfo.FindSystemTimeZoneById(candidate);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return TimeZoneInfo.Utc;
        }

        public string GetCurrentTenantCountry()
        {
            try
            {
                var tenantId = _tenantProvider.GetTenantId();
                return _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Country ?? string.Empty)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public DateTime ConvertUtcToTenantTime(DateTime value)
        {
            return ConvertUtcToTenantTime(value, GetCurrentTenantCountry());
        }

        public DateTime ConvertUtcToTenantTime(DateTime value, string? country)
        {
            var normalized = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(normalized, ResolveTimeZone(country));
        }

        public string FormatForTenant(DateTime? value, string format)
        {
            return FormatForTenant(value, GetCurrentTenantCountry(), format);
        }

        public string FormatForTenant(DateTime? value, string? country, string format)
        {
            if (!value.HasValue)
                return string.Empty;

            return ConvertUtcToTenantTime(value.Value, country).ToString(format);
        }
    }
}
