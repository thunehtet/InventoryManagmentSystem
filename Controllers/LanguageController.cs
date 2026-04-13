using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ClothInventoryApp.Controllers;

[Authorize]
public class LanguageController : Controller
{
    // Map short codes (used in URL) to valid .NET culture names
    private static readonly Dictionary<string, string> _cultureMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"]    = "en",
        ["my"]    = "my-MM",
        ["my-MM"] = "my-MM",
    };

    [HttpGet]
    public IActionResult Set(string culture, string returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(culture) && _cultureMap.TryGetValue(culture, out var resolved))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolved)),
                new CookieOptions
                {
                    Expires    = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite   = SameSiteMode.Strict,
                    HttpOnly   = false   // must be readable by browser redirects
                });
        }

        return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }
}
