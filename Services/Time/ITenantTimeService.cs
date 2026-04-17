namespace ClothInventoryApp.Services.Time
{
    public interface ITenantTimeService
    {
        TimeZoneInfo ResolveTimeZone(string? country);
        string GetCurrentTenantCountry();
        DateTime ConvertUtcToTenantTime(DateTime value);
        DateTime ConvertUtcToTenantTime(DateTime value, string? country);
        string FormatForTenant(DateTime? value, string format);
        string FormatForTenant(DateTime? value, string? country, string format);
    }
}
