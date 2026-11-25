namespace ZafiyetRadari.Application.Abstractions.Notifications;

/// <param name="HtmlBody">Tüm değişken içerik HTML-encode edilmiş olmalı (EmailTemplates kullanılır).</param>
/// <param name="TextBody">HTML göstermeyen istemciler ve spam filtreleri için düz metin sürüm.</param>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>E-posta gönderimi. Infrastructure SMTP ile uygular; Application taşıma katmanını bilmez.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Maillerde kullanılan mutlak linkler. Taban adres ayardan gelir (App:PublicBaseUrl), istekten (Host başlığı) asla.</summary>
public interface IAppLinks
{
    string BaseUrl { get; }

    string FindingDetails(long findingId);

    string AssetGroupDetails(int assetGroupId);

    string ConfirmEmail(string userId, string encodedToken);
}
