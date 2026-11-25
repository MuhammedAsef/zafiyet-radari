namespace ZafiyetRadari.Domain.Enums;

/// <summary>Bildirim gönderim sonucu. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum NotificationStatus : byte
{
    Sent = 1,
    Failed = 2
}
