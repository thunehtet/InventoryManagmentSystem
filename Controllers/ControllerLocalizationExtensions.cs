using ClothInventoryApp.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace ClothInventoryApp.Controllers;

public static class ControllerLocalizationExtensions
{
    public static string LocalizeShared(this Controller controller, string key, params object[] args)
    {
        var localizer = controller.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
        return args.Length == 0
            ? localizer[key].Value
            : localizer[key, args].Value;
    }
}
