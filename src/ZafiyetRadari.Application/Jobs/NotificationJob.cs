using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Application.Jobs;

/// <summary>
/// Bir kanala bulgu bildirimi gönderir (Worker, default kuyruk).
/// • Kanal kapatılmış/silinmişse veya alıcı artık doğrulanmış grup üyesi değilse gönderilmez.
/// • İdempotent: Hangfire tekrar denediğinde, bu tetiklemeden sonra zaten gönderilmiş bulgular atlanır.
/// • Kuyrukta beklerken kapanan bulgular (kullanıcı düzeltti/kabul etti) maile girmez.
/// SMTP hatası yeniden fırlatılır → Hangfire 3 kez dener; her deneme NotificationLog'a yazılır.
/// </summary>
public sealed class NotificationJob(
    INotificationStore store,
    IEmailSender emailSender,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<NotificationJob> logger)
{
    public async Task SendFindingsAsync(int channelId, long[] findingIds, DateTimeOffset triggeredAt, CancellationToken cancellationToken)
    {
        var channel = await store.GetDispatchInfoAsync(channelId, cancellationToken);
        if (channel is null || !channel.IsEnabled)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        if (!await store.IsValidRecipientAsync(channel.AssetGroupId, channel.Target, cancellationToken))
        {
            store.AddLogs([NotificationLog.Failed(channelId, null, "Alıcı artık grubun doğrulanmış bir üyesi değil; gönderilmedi.", now)]);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Kanal {ChannelId}: alıcı geçersiz, bildirim gönderilmedi.", channelId);
            return;
        }

        var alreadySent = await store.GetSentFindingIdsAsync(channelId, findingIds, triggeredAt, cancellationToken);
        var pending = findingIds.Where(id => !alreadySent.Contains(id)).Distinct().ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var items = await store.GetActionableFindingsAsync(pending, cancellationToken);
        if (items.Count == 0)
        {
            return;
        }

        var message = FindingEmails.NewFindings(
            channel.Target,
            channel.AssetGroupName,
            items.Take(NotificationLimits.MaxItemsPerEmail).ToList(),
            items.Count,
            links,
            channel.AssetGroupId);

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            store.AddLogs([NotificationLog.Failed(channelId, null, ex.Message, now)]);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        store.AddLogs(items.Select(i => NotificationLog.Sent(channelId, i.FindingId, now)));
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation("Kanal {ChannelId}: {Count} bulgu bildirildi.", channelId, items.Count);
    }
}
