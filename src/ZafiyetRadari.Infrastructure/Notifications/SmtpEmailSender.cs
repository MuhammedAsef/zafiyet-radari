using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZafiyetRadari.Application.Abstractions.Notifications;

namespace ZafiyetRadari.Infrastructure.Notifications;

/// <summary>
/// SMTP ayarları. Şifre repoda değil: geliştirmede User Secrets, canlıda ortam değişkeni (Smtp__Password).
/// STARTTLS (587) desteklenir; 465 (örtük TLS) System.Net.Mail'de yoktur, sağlayıcıda 587 seçilmeli.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FromName { get; set; } = "Zafiyet Radarı";

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Şifresiz bağlantıya sadece yerel test sunucusunda (Mailpit) izin verilir; kimlik bilgisi düz metin gitmesin.</summary>
    public bool IsTransportSecure() =>
        EnableSsl || Host is "localhost" or "127.0.0.1" or "::1" or "mailpit";
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.Value;

        // MailAddress adresi doğrular; geçersiz/çoklu adres ("a@b.com, c@d.com") FormatException verir.
        var to = new MailAddress(message.To);

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8),
            Subject = message.Subject.Replace('\r', ' ').Replace('\n', ' '),
            SubjectEncoding = Encoding.UTF8,
            Body = message.TextBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        mail.To.Add(to);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = settings.TimeoutSeconds * 1000
        };

        if (!string.IsNullOrEmpty(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);

        // Alıcı adresi loglanır ama içerik (token içeren linkler) loglanmaz.
        logger.LogInformation("Mail gönderildi: {To}, konu {Subject}", to.Address, mail.Subject);
    }
}

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Maillerdeki linklerin tabanı, örn. https://radar.muhammedasef.com. Host başlığından türetilmez (host header injection).</summary>
    [Required, Url]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsValid() =>
        Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class AppLinks(IOptions<AppOptions> options) : IAppLinks
{
    public string BaseUrl => options.Value.PublicBaseUrl.TrimEnd('/');

    public string FindingDetails(long findingId) => $"{BaseUrl}/Findings/Findings/Details/{findingId}";

    public string AssetGroupDetails(int assetGroupId) => $"{BaseUrl}/Inventory/Groups/Details/{assetGroupId}";

    public string ConfirmEmail(string userId, string encodedToken) =>
        $"{BaseUrl}/Account/ConfirmEmail?userId={Uri.EscapeDataString(userId)}&code={Uri.EscapeDataString(encodedToken)}";
}
