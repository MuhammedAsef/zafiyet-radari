namespace ZafiyetRadari.Domain.Versioning;

/// <summary>
/// OSV'deki bir etkilenen sürüm aralığı (vuln.AffectedRanges satırı).
/// Anlamı: Introduced &lt;= sürüm &lt; Fixed  veya  Introduced &lt;= sürüm &lt;= LastAffected.
/// Introduced boş ya da "0" ise alt sınır yoktur (tüm eski sürümler etkilenir).
/// </summary>
public sealed record VersionRangeSpec(string? Introduced, string? Fixed, string? LastAffected);

/// <summary>
/// Üç durumlu sonuç. "Unknown" bilinçli bir tercih: sürüm veya sınır okunamadığında "etkilenmiyor" demek
/// bir zafiyeti sessizce kaçırmak olur. Unknown sonuçlar düşük güvenli bulgu olarak kullanıcıya gösterilir.
/// </summary>
public enum RangeEvaluation
{
    NotAffected = 0,
    Affected = 1,
    Unknown = 2
}
