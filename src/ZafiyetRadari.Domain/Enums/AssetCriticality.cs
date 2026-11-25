namespace ZafiyetRadari.Domain.Enums;

/// <summary>Varlığın iş açısından kritikliği. Risk skorunu etkiler. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum AssetCriticality : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
