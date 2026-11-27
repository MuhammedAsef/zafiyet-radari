using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Infrastructure.Notifications;

namespace ZafiyetRadari.UnitTests.Notifications;

public sealed class EmailTemplateTests
{
    [Fact]
    public void ExternalContent_IsHtmlEncoded()
    {
        var html = EmailTemplates.Encode("<script>alert(1)</script> & \"x\"");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("/relative/path")]
    public void UnsafeLinks_AreRenderedAsText(string url)
    {
        var html = EmailTemplates.Link(url, "tıkla");

        Assert.DoesNotContain("href", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HttpsLink_IsRendered()
    {
        Assert.Contains("href=\"https://example.com/a?b=1&amp;c=2\"", EmailTemplates.Link("https://example.com/a?b=1&c=2", "x"), StringComparison.Ordinal);
    }

    [Fact]
    public void Subject_StripsLineBreaks_AgainstHeaderInjection()
    {
        var subject = EmailTemplates.Subject("Merhaba\r\nBcc: saldirgan@example.com");

        Assert.DoesNotContain("\n", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", subject, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost", false, true)]
    [InlineData("smtp-relay.brevo.com", true, true)]
    [InlineData("smtp-relay.brevo.com", false, false)]   // kimlik bilgisi düz metin gitmesin
    public void Smtp_RequiresTls_ExceptLocalTestServer(string host, bool enableSsl, bool expected)
    {
        Assert.Equal(expected, new SmtpOptions { Host = host, EnableSsl = enableSsl }.IsTransportSecure());
    }

    [Theory]
    [InlineData("ornek.kisi@firma.com", "o***@firma.com")]
    [InlineData("a@b.com", "a***@b.com")]
    public void SentMailLog_MasksRecipient(string address, string expected)
    {
        Assert.Equal(expected, SmtpEmailSender.MaskAddress(new System.Net.Mail.MailAddress(address)));
    }

    [Theory]
    [InlineData("https://radar.example.com", true)]
    [InlineData("http://radar.example.com", false)]
    [InlineData("radar.example.com", false)]
    public void PublicBaseUrl_MustBeHttps(string url, bool expected)
    {
        Assert.Equal(expected, new AppOptions { PublicBaseUrl = url }.IsValid());
    }
}
