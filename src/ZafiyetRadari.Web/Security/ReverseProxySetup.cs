using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Uygulama nginx arkasında çalışır. X-Forwarded-For / X-Forwarded-Proto başlıkları sadece
/// tanınan proxy adresinden gelirse dikkate alınır; aksi halde herkes başlığı yazıp IP'sini
/// değiştirerek rate limit'i ve audit log'daki istemci IP'sini atlatabilirdi.
///
/// Docker'da nginx'in bağlantısı konteynere ağ geçidi adresinden (ör. 172.30.0.1) gelir.
/// Bu adres ReverseProxy:KnownProxies ile verilir; boşsa sadece loopback'e güvenilir (geliştirme).
/// </summary>
public static class ReverseProxySetup
{
    private const string KnownProxiesKey = "ReverseProxy:KnownProxies";

    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection(KnownProxiesKey).Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // nginx tek atlama: başlıktaki sadece en sağdaki (nginx'in eklediği) değer kullanılır.
            options.ForwardLimit = 1;

            foreach (var proxy in proxies)
            {
                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException($"'{KnownProxiesKey}' geçersiz IP içeriyor: {proxy}");
                }

                options.KnownProxies.Add(address);
            }
        });

        return services;
    }
}
