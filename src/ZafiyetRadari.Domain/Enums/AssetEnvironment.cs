namespace ZafiyetRadari.Domain.Enums;

/// <summary>Varlığın çalıştığı ortam. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum AssetEnvironment : byte
{
    Development = 1,
    Test = 2,
    Production = 3
}
