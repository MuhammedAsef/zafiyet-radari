using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Jobs;

namespace ZafiyetRadari.Application.Notifications;

public interface IFindingNotifier
{
    Task NotifyAsync(
        int assetId,
        IReadOnlyList<NotifiableFinding> findings,
        FindingNotificationKind kind,
        CancellationToken cancellationToken);
}

/// <summary>
/// Eşleştirme sonrası: bulguları grubun kanal kurallarına göre süzer ve
/// her kanal için TEK bir gönderim işi kuyruğa koyar (180 bulgu = 180 mail değil, 1 özet mail).
/// Yeni bulgular ve önceliği yükselenler ayrı mail olarak gider: başlık ve tekrar gönderim kuralı farklı.
/// </summary>
internal sealed class FindingNotifier(
    INotificationStore store,
    INotificationScheduler scheduler,
    TimeProvider timeProvider,
    ILogger<FindingNotifier> logger) : IFindingNotifier
{
    public async Task NotifyAsync(
        int assetId,
        IReadOnlyList<NotifiableFinding> findings,
        FindingNotificationKind kind,
        CancellationToken cancellationToken)
    {
        if (findings.Count == 0)
        {
            return;
        }

        var channels = await store.GetEnabledChannelsForAssetAsync(assetId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var channel in channels)
        {
            var accepted = findings
                .Where(f => channel.Accepts(f.Priority, f.IsKev, f.IsMaliciousPackage))
                .Select(f => f.FindingId)
                .Distinct()
                .ToList();

            if (accepted.Count == 0)
            {
                continue;
            }

            if (kind == FindingNotificationKind.Escalated)
            {
                scheduler.ScheduleEscalationNotification(channel.Id, accepted, now);
            }
            else
            {
                scheduler.ScheduleFindingNotification(channel.Id, accepted, now);
            }

            logger.LogInformation(
                "Kanal {ChannelId} için {Count} bulgu bildirimi ({Kind}) kuyruğa alındı.", channel.Id, accepted.Count, kind);
        }
    }
}
