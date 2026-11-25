namespace ZafiyetRadari.Domain.Enums;

/// <summary>Bileşenin envantere nasıl girdiği. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum ComponentSource : byte
{
    Manual = 1,
    Upload = 2
}
