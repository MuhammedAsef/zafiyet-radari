using System.Net;
using System.Text;
using ZafiyetRadari.Application.Abstractions.Notifications;

namespace ZafiyetRadari.Application.Notifications;

/// <summary>
/// Mail şablonları. Değişken içerik (paket adı, zafiyet özeti, kullanıcı adı) dış kaynaktan gelir ve
/// her zaman WebUtility.HtmlEncode'dan geçer: OSV özetindeki bir &lt;script&gt; veya link mailde HTML olarak yorumlanmaz.
/// Stil satır içi (inline): mail istemcileri &lt;style&gt; ve dış CSS'i çoğunlukla yok sayar.
/// </summary>
public static class EmailTemplates
{
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>Sadece https/http linkleri üretilir; href değeri de encode edilir.</summary>
    public static string Link(string url, string text)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return Encode(text);
        }

        return $"<a href=\"{Encode(uri.AbsoluteUri)}\" style=\"color:#0d6efd\">{Encode(text)}</a>";
    }

    public static string Button(string url, string text) =>
        $"<p style=\"margin:24px 0\"><a href=\"{Encode(url)}\" style=\"background:#0d6efd;color:#ffffff;padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block\">{Encode(text)}</a></p>";

    /// <param name="innerHtml">Zaten güvenli hale getirilmiş HTML (bu sınıfın yardımcılarıyla üretilmiş).</param>
    public static string Layout(string title, string innerHtml)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>")
            .Append(Encode(title))
            .Append("</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;background:#f4f5f7;margin:0;padding:24px;color:#212529\">")
            .Append("<div style=\"max-width:640px;margin:0 auto;background:#ffffff;border-radius:8px;padding:24px\">")
            .Append("<div style=\"font-weight:600;font-size:18px;margin-bottom:16px\">Zafiyet Radarı</div>")
            .Append(innerHtml)
            .Append("<hr style=\"border:none;border-top:1px solid #dee2e6;margin:24px 0\">")
            .Append("<div style=\"font-size:12px;color:#6c757d\">Bu mail otomatik gönderildi. Bildirim ayarlarını grup sayfasından değiştirebilirsin.</div>")
            .Append("</div></body></html>");
        return html.ToString();
    }

    /// <summary>Konu satırı: satır sonu (başlık enjeksiyonu) ve kontrol karakterleri atılır, uzunluk sınırlanır.</summary>
    public static string Subject(string subject)
    {
        var clean = new string(subject.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length > 150 ? clean[..150] : clean;
    }
}

public static class AccountEmails
{
    public static EmailMessage Confirmation(string to, string confirmUrl) =>
        new(
            to,
            EmailTemplates.Subject("Zafiyet Radarı: e-posta adresini doğrula"),
            EmailTemplates.Layout(
                "E-posta doğrulama",
                "<p>Merhaba,</p><p>Zafiyet Radarı hesabını doğrulamak için aşağıdaki butona tıkla. Link 24 saat geçerlidir.</p>"
                + EmailTemplates.Button(confirmUrl, "E-postamı doğrula")
                + "<p style=\"font-size:13px;color:#6c757d\">Bu kaydı sen yapmadıysan bu maili yok sayabilirsin; adresine bildirim gönderilmez.</p>"),
            $"Zafiyet Radarı hesabını doğrulamak için bu linki aç (24 saat geçerli):\n{confirmUrl}\n\nBu kaydı sen yapmadıysan bu maili yok say.");
}
