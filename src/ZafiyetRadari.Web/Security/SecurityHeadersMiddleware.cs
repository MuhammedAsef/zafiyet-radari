namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Her yanıta tarayıcı güvenlik başlıklarını ekler.
/// CSP'de inline script/style'a izin yoktur; tüm kaynaklar kendi origin'imizden gelir.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    // Hangfire paneli inline script/style kullanır. Sadece Admin'e açık bu yolda CSP gevşetilir.
    private static readonly PathString HangfireDashboardPath = new("/ops/jobs");

    private readonly RequestDelegate _next;
    private readonly string _contentSecurityPolicy;
    private readonly string _dashboardContentSecurityPolicy;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _contentSecurityPolicy = BuildContentSecurityPolicy(environment.IsDevelopment(), allowInline: false);
        _dashboardContentSecurityPolicy = BuildContentSecurityPolicy(environment.IsDevelopment(), allowInline: true);
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = context.Request.Path.StartsWithSegments(HangfireDashboardPath)
                ? _dashboardContentSecurityPolicy
                : _contentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static string BuildContentSecurityPolicy(bool isDevelopment, bool allowInline)
    {
        var inline = allowInline ? " 'unsafe-inline'" : string.Empty;

        // Geliştirmede dotnet watch'ın tarayıcı yenileme WebSocket'i farklı portta çalışır.
        var connectSources = isDevelopment
            ? "'self' ws://localhost:* wss://localhost:*"
            : "'self'";

        return string.Join("; ",
            "default-src 'self'",
            $"script-src 'self'{inline}",
            $"style-src 'self'{inline}",
            "img-src 'self' data:",
            "font-src 'self'",
            $"connect-src {connectSources}",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'",
            "upgrade-insecure-requests");
    }
}
