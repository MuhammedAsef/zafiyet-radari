using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Jobs;

namespace ZafiyetRadari.Application.Notifications;

public interface IFindingNotifier
{
    Task NotifyAsync(int assetId, IReadOnlyList<NotifiableFinding> findings, CancellationToken cancellationToken);
}

/// <summary>
/// Eşleştirme sonrası: yeni/yeniden açılan bulguları grubun kanal kurallarına göre süzer ve
/// her kanal için TEK bir gönderim işi kuyruğa koyar (180 bulgu = 180 mail değil, 1 özet mail).
/// </summary>
internal sealed class FindingNotifier(
    INotificationStore store,
    INotificationScheduler scheduler,
    TimeProvider timeProvider,
    ILogger<FindingNotifier> logger) : IFindingNotifier
{
    public async Task NotifyAsync(int assetId, IReadOnlyList<NotifiableFinding> findings, CancellationToken cancellationToken)
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

            scheduler.ScheduleFindingNotification(channel.Id, accepted, now);
            logger.LogInformation("Kanal {ChannelId} için {Count} bulgu bildirimi kuyruğa alındı.", channel.Id, accepted.Count);
        }
    }
}
