namespace ZafiyetRadari.Web.Security;

/// <summary>Controller'larda [EnableRateLimiting(...)] ile kullanılan politika adları.</summary>
public static class RateLimitPolicies
{
    /// <summary>Giriş, kayıt, şifre sıfırlama: IP başına 5 dakikada 10 istek.</summary>
    public const string Authentication = "auth";

    /// <summary>SBOM / lock dosyası yükleme: IP başına 10 dakikada 20 istek.</summary>
    public const string FileUpload = "upload";
}
