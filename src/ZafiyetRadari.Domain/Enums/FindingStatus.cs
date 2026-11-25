namespace ZafiyetRadari.Domain.Enums;

/// <summary>Bulgunun yaşam döngüsündeki durumu. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum FindingStatus : byte
{
    Open = 1,

    InReview = 2,

    Fixed = 3,

    /// <summary>Gerekçe ve bitiş tarihi zorunlu.</summary>
    RiskAccepted = 4,

    /// <summary>Gerekçe zorunlu.</summary>
    FalsePositive = 5
}
