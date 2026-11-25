using System.Security.Cryptography.X509Certificates;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Data Protection anahtar halkası veritabanında (ops.DataProtectionKeys) durur. Bu anahtarlar oturum
/// cookie'lerini ve e-posta doğrulama token'larını imzalar; tablo sızarsa cookie sahtelenebilir.
/// Production'da anahtarlar bir sertifikayla şifrelenir; sertifika veritabanında değil, sunucuda
/// salt okunur bir dosya olarak durur. İkisi birlikte ele geçirilmeden anahtarlar okunamaz.
/// </summary>
internal static class DataProtectionCertificate
{
    private const string PathKey = "DataProtection:CertificatePath";
    private const string PasswordKey = "DataProtection:CertificatePassword";

    public static X509Certificate2? Load(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration[PathKey];
        if (string.IsNullOrWhiteSpace(path))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    $"Production'da '{PathKey}' zorunlu: anahtar halkası veritabanında şifresiz kalmamalı.");
            }

            return null;
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Data Protection sertifikası bulunamadı: {path}");
        }

        // EphemeralKeySet: özel anahtar diske (kullanıcı sertifika deposuna) yazılmaz, sadece bellekte tutulur.
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            path,
            configuration[PasswordKey],
            X509KeyStorageFlags.EphemeralKeySet);

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException("Data Protection sertifikasının özel anahtarı yok.");
        }

        return certificate;
    }
}
