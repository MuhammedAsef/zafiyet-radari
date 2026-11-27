using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Notifications;

public sealed record NotifiableFinding(long FindingId, FindingPriority Priority, bool IsKev, bool IsMaliciousPackage);

/// <summary>Bildirimin sebebi: maildeki başlık ve tekrar gönderim kuralı buna göre seçilir.</summary>
public enum FindingNotificationKind
{
    /// <summary>Yeni veya yeniden açılan bulgu.</summary>
    New,

    /// <summary>Açık bulgunun önceliği yükseldi (ör. CISA KEV'e eklendi).</summary>
    Escalated
}

public sealed record ChannelListItem(
    int Id,
    string Name,
    string Target,
    FindingPriority MinPriority,
    bool OnlyKev,
    bool IsEnabled,
    DateTimeOffset? LastSentAt,
    bool WeeklyDigest);

public sealed record RecipientOption(string UserId, string Email);

public sealed record GroupNotificationSettings(
    IReadOnlyList<ChannelListItem> Channels,
    IReadOnlyList<RecipientOption> Recipients);

public sealed record CreateEmailChannelCommand(
    int AssetGroupId,
    string Name,
    string RecipientUserId,
    FindingPriority MinPriority,
    bool OnlyKev,
    bool WeeklyDigest = true);

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
    bool IsMaliciousPackage,
    DateOnly? KevAddedAt = null);

/// <summary>Haftalık özetteki sayılar ve en riskli açık bulgular (grup bazında).</summary>
/// <param name="OpenByPriority">Aksiyon bekleyen (Açık/İncelemede) bulgu sayısı, önceliğe göre.</param>
/// <param name="NewThisPeriod">Dönem içinde ilk kez görülen bulgular.</param>
/// <param name="FixedThisPeriod">Dönem içinde düzeltildi olarak kapanan bulgular.</param>
/// <param name="OpenKev">Aksiyon bekleyen ve CISA KEV listesindeki bulgular.</param>
/// <param name="TopFindings">En acil açık bulgular (öncelik, sonra risk skoru).</param>
public sealed record GroupDigest(
    IReadOnlyDictionary<FindingPriority, int> OpenByPriority,
    int NewThisPeriod,
    int FixedThisPeriod,
    int OpenKev,
    IReadOnlyList<FindingEmailItem> TopFindings)
{
    public int OpenTotal => OpenByPriority.Values.Sum();

    public int Count(FindingPriority priority) => OpenByPriority.TryGetValue(priority, out var count) ? count : 0;

    /// <summary>Açık bulgu yok ve dönemde hareket olmadıysa mail gönderilmez (gürültü olmasın).</summary>
    public bool IsEmpty => OpenTotal == 0 && NewThisPeriod == 0 && FixedThisPeriod == 0;
}

/// <summary>Gönderim için kanal bilgisi.</summary>
public sealed record ChannelDispatchInfo(int ChannelId, int AssetGroupId, string AssetGroupName, string Target, bool IsEnabled);

public static class NotificationLimits
{
    public const int MaxChannelNameLength = NotificationChannel.MaxNameLength;

    /// <summary>Bir mailde listelenen en fazla bulgu; fazlası için "tümünü gör" linki.</summary>
    public const int MaxItemsPerEmail = 25;

    /// <summary>Haftalık özette listelenen en acil bulgu sayısı.</summary>
    public const int DigestTopFindings = 10;

    /// <summary>Haftalık özetin kapsadığı dönem.</summary>
    public static readonly TimeSpan DigestPeriod = TimeSpan.FromDays(7);

    /// <summary>
    /// Önceliği yükselen bulgu, bu süre içinde aynı kanala zaten gönderildiyse tekrar gönderilmez.
    /// EPSS gibi günlük değişen skorlar eşik etrafında gidip gelirse her gün mail atılmasın.
    /// </summary>
    public static readonly TimeSpan EscalationCooldown = TimeSpan.FromDays(3);
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

    /// <summary>Haftalık özet alacak kanallar: etkin, özet açık, e-posta.</summary>
    Task<IReadOnlyList<int>> GetDigestChannelIdsAsync(CancellationToken cancellationToken);

    /// <summary>Kanala belirtilen andan sonra haftalık özet gönderilmiş mi? (Hangfire tekrar denemesinde çift mail önlemi)</summary>
    Task<bool> HasDigestSentSinceAsync(int channelId, DateTimeOffset since, CancellationToken cancellationToken);

    Task<GroupDigest> GetGroupDigestAsync(int assetGroupId, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Alıcı hâlâ grubun üyesi ve e-postası doğrulanmış mı?</summary>
    Task<bool> IsValidRecipientAsync(int assetGroupId, string email, CancellationToken cancellationToken);

    /// <summary>Verilen bulgulardan bu kanala belirtilen andan sonra başarıyla gönderilmiş olanlar (tekrar denemede çift mail önlemi).</summary>
    Task<IReadOnlySet<long>> GetSentFindingIdsAsync(int channelId, IReadOnlyCollection<long> findingIds, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Hâlâ aksiyon bekleyen (Açık/İncelemede) bulguların mail satırları, önceliğe göre sıralı.</summary>
    Task<IReadOnlyList<FindingEmailItem>> GetActionableFindingsAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken);

    void AddLogs(IEnumerable<NotificationLog> logs);
}
