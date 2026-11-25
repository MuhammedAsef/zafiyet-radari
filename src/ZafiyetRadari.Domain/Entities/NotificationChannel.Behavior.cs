using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Domain.Entities;

/// <summary>
/// Grup bildirim kanalı. Şimdilik e-posta: alıcı, grubun e-postası doğrulanmış bir üyesi olmak zorunda
/// (uygulama keyfi adreslere mail atan bir spam aracına dönüşmesin). Bu kontrol Application'da yapılır,
/// çünkü üyelik ve doğrulama bilgisi aggregate dışında.
/// </summary>
public partial class NotificationChannel
{
    public const int MaxNameLength = 100;
    public const int MaxTargetLength = 256;

    public NotificationChannelType ChannelType => (NotificationChannelType)Type;

    public static Result<NotificationChannel> CreateEmail(
        int assetGroupId,
        string name,
        string email,
        FindingPriority minPriority,
        bool onlyKev,
        DateTimeOffset now)
    {
        name = name?.Trim() ?? string.Empty;
        email = email?.Trim() ?? string.Empty;

        if (name.Length is 0 or > MaxNameLength)
        {
            return NotificationErrors.InvalidName;
        }

        if (email.Length is 0 or > MaxTargetLength)
        {
            return NotificationErrors.InvalidRecipient;
        }

        if (!Enum.IsDefined(minPriority))
        {
            return NotificationErrors.InvalidRule;
        }

        var channel = new NotificationChannel
        {
            AssetGroupId = assetGroupId,
            Type = (byte)NotificationChannelType.Email,
            Name = name,
            Target = email,
            IsEnabled = true,
            CreatedAt = now
        };

        channel.NotificationRules.Add(new NotificationRule
        {
            MinPriority = (byte)minPriority,
            OnlyKev = onlyKev,
            IsEnabled = true
        });

        return channel;
    }

    /// <summary>
    /// Bulgu bu kanaldan bildirilmeli mi? Öncelik kuralın eşiği veya daha acil olmalı (Now=1 en acil).
    /// Kötü amaçlı paketler "sadece KEV" kuralında da bildirilir: aktif saldırı, KEV kadar acil.
    /// </summary>
    public bool Accepts(FindingPriority priority, bool isKev, bool isMaliciousPackage) =>
        IsEnabled
        && NotificationRules.Any(rule =>
            rule.IsEnabled
            && (byte)priority <= rule.MinPriority
            && (!rule.OnlyKev || isKev || isMaliciousPackage));

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}
