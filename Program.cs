using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using QuestPDF.Infrastructure;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Identity;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

// ── MVC + Localization ─────────────────────────────────────────
builder.Services.AddLocalization(options => options.ResourcesPath = "");
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

// ── Database ───────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ── Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password policy
    options.Password.RequireDigit            = true;
    options.Password.RequireLowercase        = true;
    options.Password.RequireUppercase        = false;
    options.Password.RequireNonAlphanumeric  = false;
    options.Password.RequiredLength          = 8;

    // Account lockout — block after 5 failed attempts for 15 minutes
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers      = true;

    // User
    options.User.RequireUniqueEmail = false;
});

// ── Cookie security ─────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/AccessDenied";

    options.Cookie.HttpOnly  = true;
    options.Cookie.Name      = ".StockEasy.Session";
    options.Cookie.SameSite  = SameSiteMode.Strict;

    // Secure in production, allow HTTP only in development
    options.Cookie.SecurePolicy = env.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.ExpireTimeSpan    = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ── Rate limiting (login endpoint) ──────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Max 5 login attempts per minute per IP
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit            = 5;
        limiter.Window                 = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder   = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit             = 0;
    });

    // Max 10 customer registrations per 10 minutes per IP (prevents spam)
    options.AddFixedWindowLimiter("public-registration", limiter =>
    {
        limiter.PermitLimit          = 10;
        limiter.Window               = TimeSpan.FromMinutes(10);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit           = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── App services ────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();
builder.Services.AddHostedService<ClothInventoryApp.Services.Subscription.SubscriptionExpiryService>();

var app = builder.Build();

// ── Security response headers ────────────────────────────────────
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.Append("X-Frame-Options",        "DENY");
    headers.Append("X-Content-Type-Options", "nosniff");
    headers.Append("X-XSS-Protection",       "1; mode=block");
    headers.Append("Referrer-Policy",         "strict-origin-when-cross-origin");
    headers.Append("Permissions-Policy",      "camera=(), microphone=(), geolocation=()");
    await next();
});

// ── HTTP pipeline ────────────────────────────────────────────────
if (!env.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

var supportedCultures = new[] { "en", "my-MM" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
// Ensure cookie provider is first so it takes precedence over Accept-Language header
localizationOptions.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

app.UseRateLimiter();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// ── Force password change on first login ─────────────────────────
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Only intercept authenticated users on non-exempt paths
    if (context.User.Identity?.IsAuthenticated == true &&
        !path.StartsWith("/Account/ChangePassword", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/p/", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
    {
        var userManager = context.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(context.User);
        if (user?.MustChangePassword == true)
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }
    }

    await next();
});
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}")
    .WithStaticAssets();

// Always seed SuperAdmin; only seed demo data in development
await DatabaseSeeder.SeedAsync(app.Services);

app.Run();
