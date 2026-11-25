namespace ZafiyetRadari.Domain.Enums;

/// <summary>Varlık grubundaki üyelik rolü. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum AssetGroupRole : byte
{
    /// <summary>Grubu yönetir, üye ekler/çıkarır.</summary>
    Owner = 1,

    /// <summary>Varlık ve bulguları düzenler.</summary>
    Editor = 2,

    /// <summary>Sadece okur.</summary>
    Viewer = 3
}
