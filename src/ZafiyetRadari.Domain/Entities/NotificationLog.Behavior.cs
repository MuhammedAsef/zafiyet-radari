using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Entities;

/// <summary>Gönderim kaydı: aynı bulgunun aynı kanala tekrar gönderilmemesi ve iz için.</summary>
public partial class NotificationLog
{
    public static NotificationLog Sent(int channelId, long? findingId, DateTimeOffset now) =>
        new() { ChannelId = channelId, FindingId = findingId, Status = (byte)NotificationStatus.Sent, CreatedAt = now };

    public static NotificationLog Failed(int channelId, long? findingId, string error, DateTimeOffset now) =>
        new()
        {
            ChannelId = channelId,
            FindingId = findingId,
            Status = (byte)NotificationStatus.Failed,
            Error = error.Length > 500 ? error[..500] : error,
            CreatedAt = now
        };
}
