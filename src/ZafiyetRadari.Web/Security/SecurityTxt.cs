using System.Text;
using ZafiyetRadari.Application.Abstractions.Notifications;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// RFC 9116 security.txt: güvenlik araştırmacısı bir açık bulursa nereye bildireceğini bilsin.
/// Bildirim kanalı GitHub'ın özel güvenlik bildirimi (Private vulnerability reporting); açık issue değil.
/// Dosya değil endpoint: statik dosya sunucusu "." ile başlayan klasörleri varsayılan olarak dışarıda bırakır
/// ve Canonical satırı ortamın kendi adresinden (App:PublicBaseUrl) üretilmeli.
/// </summary>
internal static class SecurityTxt
{
    private const string Repository = "https://github.com/MuhammedAsef/zafiyet-radari";

    /// <summary>RFC 9116: en fazla bir yıl sonrası. Süresi dolmadan ileri al.</summary>
    private const string Expires = "2027-11-27T00:00:00.000Z";

    public static IEndpointConventionBuilder MapSecurityTxt(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/.well-known/security.txt", (IAppLinks links, HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Text(Build(links.BaseUrl), "text/plain; charset=utf-8", Encoding.UTF8);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

    internal static string Build(string baseUrl) =>
        new StringBuilder()
            .Append("Contact: ").Append(Repository).Append("/security/advisories/new\n")
            .Append("Expires: ").Append(Expires).Append('\n')
            .Append("Preferred-Languages: tr, en\n")
            .Append("Canonical: ").Append(baseUrl.TrimEnd('/')).Append("/.well-known/security.txt\n")
            .Append("Policy: ").Append(Repository).Append("/blob/main/SECURITY.md\n")
            .ToString();
}
