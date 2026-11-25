namespace ZafiyetRadari.Domain.Enums;

/// <summary>Bildirim kanalı türü. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum NotificationChannelType : byte
{
    Email = 1,
    Webhook = 2,
    Telegram = 3
}
