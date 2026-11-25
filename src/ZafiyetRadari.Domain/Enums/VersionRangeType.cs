namespace ZafiyetRadari.Domain.Enums;

/// <summary>Etkilenen sürüm aralığının karşılaştırma biçimi (OSV). Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum VersionRangeType : byte
{
    Semver = 1,
    Ecosystem = 2
}
