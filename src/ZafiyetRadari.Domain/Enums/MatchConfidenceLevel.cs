namespace ZafiyetRadari.Domain.Enums;

/// <summary>Bileşen-zafiyet eşleşmesinin güvenilirliği. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum MatchConfidenceLevel : byte
{
    /// <summary>İsim benzerliği; kullanıcı onayı gerekir.</summary>
    Low = 1,

    /// <summary>CVE kaydındaki ürün/sürüm aralığı.</summary>
    Medium = 2,

    /// <summary>CPE eşleşmesi.</summary>
    High = 3,

    /// <summary>PURL + OSV sürüm aralığı.</summary>
    Exact = 4
}
