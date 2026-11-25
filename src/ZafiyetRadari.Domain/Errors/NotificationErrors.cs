using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Domain.Errors;

public static class NotificationErrors
{
    public static readonly Error InvalidName =
        Error.Validation("Notification.InvalidName", $"Kanal adı 1-{NotificationChannel.MaxNameLength} karakter olmalı.");

    public static readonly Error InvalidRecipient =
        Error.Validation("Notification.InvalidRecipient", "Alıcı, e-postası doğrulanmış bir grup üyesi olmalı.");

    public static readonly Error InvalidRule =
        Error.Validation("Notification.InvalidRule", "Geçersiz öncelik eşiği.");

    public static readonly Error NotFound =
        Error.NotFound("Notification.NotFound", "Bildirim kanalı bulunamadı.");

    public static readonly Error SendFailed =
        Error.Conflict("Notification.SendFailed", "Test maili gönderilemedi. SMTP ayarlarını ve sunucunun çalıştığını kontrol et.");
}
