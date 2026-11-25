using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Notifications;

public sealed record NotifiableFinding(long FindingId, FindingPriority Priority, bool IsKev, bool IsMaliciousPackage);

public sealed record ChannelListItem(
    int Id,
    string Name,
    string Target,
    FindingPriority MinPriority,
    bool OnlyKev,
    bool IsEnabled,
    DateTimeOffset? LastSentAt);

public sealed record RecipientOption(string UserId, string Email);

public sealed record GroupNotificationSettings(
    IReadOnlyList<ChannelListItem> Channels,
    IReadOnlyList<RecipientOption> Recipients);

public sealed record CreateEmailChannelCommand(
    int AssetGroupId,
    string Name,
    string RecipientUserId,
    FindingPriority MinPriority,
    bool OnlyKev);

/// <summary>Bildirim mailindeki bir satır.</summary>
public sealed record FindingEmailItem(
    long FindingId,
    string Title,
    string? Summary,
    string PackageName,
    string Version,
    string AssetName,
    FindingPriority Priority,
    decimal RiskScore,
    bool IsKev,
    bool IsMaliciousPackage);

/// <summary>Gönderim için kanal bilgisi.</summary>
public sealed record ChannelDispatchInfo(int ChannelId, int AssetGroupId, string AssetGroupName, string Target, bool IsEnabled);

public static class NotificationLimits
{
    public const int MaxChannelNameLength = NotificationChannel.MaxNameLength;

    /// <summary>Bir mailde listelenen en fazla bulgu; fazlası için "tümünü gör" linki.</summary>
    public const int MaxItemsPerEmail = 25;
}

/// <summary>Bildirim verileri (Infrastructure). Alıcı kontrolü auth tablosunu okur; Worker'a sadece gereken kolonlar açık.</summary>
public interface INotificationStore
{
    Task<IReadOnlyList<ChannelListItem>> ListChannelsAsync(int assetGroupId, CancellationToken cancellationToken);

    Task<NotificationChannel?> GetChannelAsync(int channelId, CancellationToken cancellationToken);

    void AddChannel(NotificationChannel channel);

    void RemoveChannel(NotificationChannel channel);

    /// <summary>Varlığın grubundaki etkin kanallar, kurallarıyla.</summary>
    Task<IReadOnlyList<NotificationChannel>> GetEnabledChannelsForAssetAsync(int assetId, CancellationToken cancellationToken);

    Task<ChannelDispatchInfo?> GetDispatchInfoAsync(int channelId, CancellationToken cancellationToken);

    /// <summary>Alıcı hâlâ grubun üyesi ve e-postası doğrulanmış mı?</summary>
    Task<bool> IsValidRecipientAsync(int assetGroupId, string email, CancellationToken cancellationToken);

    /// <summary>Verilen bulgulardan bu kanala belirtilen andan sonra başarıyla gönderilmiş olanlar (tekrar denemede çift mail önlemi).</summary>
    Task<IReadOnlySet<long>> GetSentFindingIdsAsync(int channelId, IReadOnlyCollection<long> findingIds, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Hâlâ aksiyon bekleyen (Açık/İncelemede) bulguların mail satırları, önceliğe göre sıralı.</summary>
    Task<IReadOnlyList<FindingEmailItem>> GetActionableFindingsAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken);

    void AddLogs(IEnumerable<NotificationLog> logs);
}
