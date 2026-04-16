using ClothInventoryApp.Data;
using ClothInventoryApp.Models;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using ClothInventoryApp.Services.Feature;
using ClothInventoryApp.Services.Identity;
using ClothInventoryApp.Services.Stock;
using ClothInventoryApp.Services.Subscription;
using ClothInventoryApp.Services.Tenant;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Environment;

RegisterQuestPdfFonts(env.ContentRootPath);

// MVC + Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "");
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

// Database
// Railway provides MYSQLCONNSTR_DefaultConnection.
// Local development falls back to appsettings.json / user secrets.
var railwayConnectionString = builder.Configuration["MYSQLCONNSTR_DefaultConnection"];
var connectionString = railwayConnectionString
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string not found.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    // Account lockout
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    // User
    options.User.RequireUniqueEmail = false;
});

// Cookie security
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Cookie.HttpOnly = true;
    options.Cookie.Name = ".StockEasy.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Railway is behind proxy / HTTPS at edge
    options.Cookie.SecurePolicy = env.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Forwarded headers for Railway proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // Railway proxy can vary, so clear known networks/proxies
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("public-registration", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(10);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// App services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppClaimsPrincipalFactory>();
builder.Services.AddHostedService<ClothInventoryApp.Services.Subscription.SubscriptionExpiryService>();

var app = builder.Build();

// Railway port binding
// Only bind explicitly when Railway injects PORT.
// In local development, let launchSettings.json or dotnet run control the URLs.
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

// Security response headers
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Frame-Options"] = "DENY";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-XSS-Protection"] = "1; mode=block";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

// HTTP pipeline
if (!env.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

var supportedCultures = new[] { "en", "my-MM" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Insert(
    0,
    new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());

app.UseRequestLocalization(localizationOptions);

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Force password change on first login
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}");

// Always seed SuperAdmin; only seed demo data in development
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // FIRST create tables
    db.Database.Migrate();

    // THEN seed data
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();

static void RegisterQuestPdfFonts(string contentRootPath)
{
    var fontPaths = new[]
    {
        Path.Combine(contentRootPath, "Resources", "Fonts", "mmrtext.ttf"),
        Path.Combine(contentRootPath, "Resources", "Fonts", "mmrtextb.ttf")
    };

    foreach (var fontPath in fontPaths)
    {
        if (!File.Exists(fontPath))
            continue;

        using var stream = File.OpenRead(fontPath);
        FontManager.RegisterFont(stream);
    }
}
