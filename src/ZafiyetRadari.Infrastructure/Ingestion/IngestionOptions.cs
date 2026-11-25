using System.ComponentModel.DataAnnotations;

namespace ZafiyetRadari.Infrastructure.Ingestion;

/// <summary>
/// Veri kaynağı ayarları ("Ingestion" bölümü). Adresler sadece konfigürasyondan gelir, kullanıcı belirleyemez (SSRF yok)
/// ve https olmak zorundadır. Varsayılanlar üretim için uygundur.
/// </summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    [Required, Url]
    public string OsvBaseUrl { get; init; } = "https://osv-vulnerabilities.storage.googleapis.com";

    /// <summary>
    /// CISA'nın resmi GitHub aynası (cisagov/kev-data). cisa.gov bot korumasıyla otomatik istemcilere 403 dönebiliyor;
    /// iki kaynak dakikalar içinde senkronize tutuluyor.
    /// </summary>
    [Required, Url]
    public string KevUrl { get; init; } = "https://raw.githubusercontent.com/cisagov/kev-data/main/known_exploited_vulnerabilities.json";

    [Required, Url]
    public string EpssUrl { get; init; } = "https://epss.empiricalsecurity.com/epss_scores-current.csv.gz";

    /// <summary>Tek bir indirmenin (örn. OSV all.zip) üst sınırı. Aşılırsa indirme iptal edilir.</summary>
    [Range(16, 8192)]
    public int MaxDownloadMegabytes { get; init; } = 2048;

    /// <summary>Veritabanına tek seferde yazılan OSV kaydı sayısı.</summary>
    [Range(50, 5000)]
    public int BatchSize { get; init; } = 500;

    /// <summary>OpenSSF kötü amaçlı paket kayıtları (MAL-...). npm'de sayıları çok yüksektir, ilk senkronizasyonu uzatır.</summary>
    public bool IncludeMaliciousPackages { get; init; } = true;

    /// <summary>Artımlı senkronizasyonda bundan fazla değişiklik varsa tam senkronizasyona geçilir.</summary>
    [Range(1, 50_000)]
    public int MaxIncrementalRecords { get; init; } = 5000;

    internal bool AllUrlsAreHttps() =>
        new[] { OsvBaseUrl, KevUrl, EpssUrl }.All(url =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
}
