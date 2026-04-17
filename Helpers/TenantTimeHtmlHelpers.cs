using ClothInventoryApp.Services.Time;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace ClothInventoryApp.Helpers
{
    public static class TenantTimeHtmlHelpers
    {
        public static string TenantDate(this IHtmlHelper html, DateTime? value, string format = "dd MMM yyyy HH:mm")
        {
            var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<ITenantTimeService>();
            return string.IsNullOrWhiteSpace(service.FormatForTenant(value, format))
                ? "—"
                : service.FormatForTenant(value, format);
        }

        public static string TenantDate(this IHtmlHelper html, DateTime? value, string? country, string format = "dd MMM yyyy HH:mm")
        {
            var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<ITenantTimeService>();
            return string.IsNullOrWhiteSpace(service.FormatForTenant(value, country, format))
                ? "—"
                : service.FormatForTenant(value, country, format);
        }
    }
}
