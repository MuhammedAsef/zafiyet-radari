using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZafiyetRadari.Web.Logging;

namespace ZafiyetRadari.Web.Security;

public static class WebSecurityExtensions
{
    public static IServiceCollection AddWebSecurity(this IServiceCollection services)
    {
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        // CSRF: tüm POST/PUT/DELETE istekleri global filtreyle doğrulanır (Program.cs).
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-zr-af";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.HeaderName = "X-CSRF-TOKEN";
        });

        // TempData (işlem sonrası "kaydedildi" mesajları) çerezi de diğerleri gibi: sadece HTTPS, script erişemez, __Host- önekli.
        // Varsayılan ad ve SecurePolicy ile silme yanıtı Secure bayraksız gidiyordu (Mozilla Observatory -5).
        // SameSite=Lax: e-posta doğrulama linkinden (siteler arası gezinme) sonraki yönlendirmede mesaj kaybolmasın.
        services.Configure<CookieTempDataProviderOptions>(options =>
        {
            options.Cookie.Name = "__Host-zr-tmp";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.IsEssential = true;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                FixedWindow(context, permitLimit: 300, window: TimeSpan.FromMinutes(1)));

            options.AddPolicy(RateLimitPolicies.Authentication, context =>
                FixedWindow(context, permitLimit: 10, window: TimeSpan.FromMinutes(5)));

            options.AddPolicy(RateLimitPolicies.FileUpload, context =>
                FixedWindow(context, permitLimit: 20, window: TimeSpan.FromMinutes(10)));

            options.OnRejected = (rejected, _) =>
            {
                var logger = rejected.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Security.RateLimit");

                logger.LogWarning(
                    "Rate limit aşıldı. Yol: {Path}, İstemci: {ClientIp}",
                    LogSanitizer.Path(rejected.HttpContext),
                    ClientKey(rejected.HttpContext));

                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();

    private static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });

    // Reverse proxy arkasında gerçek istemci IP'si UseForwardedHeaders ile gelir (ReverseProxySetup).
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
