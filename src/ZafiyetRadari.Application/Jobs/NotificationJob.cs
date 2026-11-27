using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Application.Jobs;

/// <summary>
/// Bir kanala bildirim gönderir (Worker, default kuyruk).
/// • Kanal kapatılmış/silinmişse veya alıcı artık doğrulanmış grup üyesi değilse gönderilmez.
/// • İdempotent: Hangfire tekrar denediğinde zaten gönderilmiş bulgular (veya bu haftanın özeti) atlanır.
/// • Kuyrukta beklerken kapanan bulgular (kullanıcı düzeltti/kabul etti) maile girmez.
/// SMTP hatası yeniden fırlatılır → Hangfire 3 kez dener; her deneme NotificationLog'a yazılır.
/// </summary>
public sealed class NotificationJob(
    INotificationStore store,
    INotificationScheduler scheduler,
    IEmailSender emailSender,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<NotificationJob> logger)
{
    /// <summary>Yeni veya yeniden açılan bulgular.</summary>
    public Task SendFindingsAsync(int channelId, long[] findingIds, DateTimeOffset triggeredAt, CancellationToken cancellationToken) =>
        SendFindingMailAsync(
            channelId,
            findingIds,
            alreadySentSince: triggeredAt,
            (channel, items, total) => FindingEmails.NewFindings(channel.Target, channel.AssetGroupName, items, total, links, channel.AssetGroupId),
            cancellationToken);

    /// <summary>
    /// Önceliği yükselen açık bulgular (ör. CISA KEV'e eklendi).
    /// Bekleme süresi içinde bu kanala zaten gönderilmiş bulgu tekrar gönderilmez: skor eşik etrafında gidip gelirse mail yağmasın.
    /// </summary>
    public Task SendEscalationsAsync(int channelId, long[] findingIds, DateTimeOffset triggeredAt, CancellationToken cancellationToken) =>
        SendFindingMailAsync(
            channelId,
            findingIds,
            alreadySentSince: triggeredAt - NotificationLimits.EscalationCooldown,
            (channel, items, total) => FindingEmails.Escalated(channel.Target, channel.AssetGroupName, items, total, links, channel.AssetGroupId, timeProvider.GetUtcNow()),
            cancellationToken);

    /// <summary>
    /// Zamanlanmış tetikleyici (haftalık): özeti kendisi göndermez, özet açık her kanal için ayrı iş kuyruğa koyar.
    /// </summary>
    public async Task EnqueueWeeklyDigestsAsync(CancellationToken cancellationToken)
    {
        var channelIds = await store.GetDigestChannelIdsAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        foreach (var channelId in channelIds)
        {
            scheduler.ScheduleWeeklyDigest(channelId, now);
        }

        logger.LogInformation("Haftalık özet: {Count} kanal için iş kuyruğa alındı.", channelIds.Count);
    }

    public async Task SendWeeklyDigestAsync(int channelId, DateTimeOffset triggeredAt, CancellationToken cancellationToken)
    {
        var channel = await GetDeliverableChannelAsync(channelId, cancellationToken);
        if (channel is null)
        {
            return;
        }

        // Tekrar denemede veya tetikleyici iki kez çalışırsa aynı hafta ikinci özet gitmesin.
        if (await store.HasDigestSentSinceAsync(channelId, triggeredAt, cancellationToken))
        {
            return;
        }

        var digest = await store.GetGroupDigestAsync(channel.AssetGroupId, triggeredAt - NotificationLimits.DigestPeriod, cancellationToken);
        if (digest.IsEmpty)
        {
            logger.LogInformation("Kanal {ChannelId}: özetlenecek bulgu yok, haftalık özet gönderilmedi.", channelId);
            return;
        }

        var message = FindingEmails.WeeklyDigest(channel.Target, channel.AssetGroupName, digest, links, channel.AssetGroupId);
        await DeliverAsync(channelId, message, [NotificationLog.Sent(channelId, null, timeProvider.GetUtcNow())], cancellationToken);

        logger.LogInformation("Kanal {ChannelId}: haftalık özet gönderildi ({Open} açık bulgu).", channelId, digest.OpenTotal);
    }

    private async Task SendFindingMailAsync(
        int channelId,
        long[] findingIds,
        DateTimeOffset alreadySentSince,
        Func<ChannelDispatchInfo, IReadOnlyList<FindingEmailItem>, int, EmailMessage> buildMessage,
        CancellationToken cancellationToken)
    {
        var channel = await GetDeliverableChannelAsync(channelId, cancellationToken);
        if (channel is null)
        {
            return;
        }

        var alreadySent = await store.GetSentFindingIdsAsync(channelId, findingIds, alreadySentSince, cancellationToken);
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

        var message = buildMessage(channel, items.Take(NotificationLimits.MaxItemsPerEmail).ToList(), items.Count);
        var now = timeProvider.GetUtcNow();
        await DeliverAsync(channelId, message, items.Select(i => NotificationLog.Sent(channelId, i.FindingId, now)).ToList(), cancellationToken);

        logger.LogInformation("Kanal {ChannelId}: {Count} bulgu bildirildi.", channelId, items.Count);
    }

    /// <summary>Kanal hâlâ açık ve alıcı hâlâ doğrulanmış grup üyesi mi? Değilse null (ve gerekiyorsa iz kaydı).</summary>
    private async Task<ChannelDispatchInfo?> GetDeliverableChannelAsync(int channelId, CancellationToken cancellationToken)
    {
        var channel = await store.GetDispatchInfoAsync(channelId, cancellationToken);
        if (channel is null || !channel.IsEnabled)
        {
            return null;
        }

        if (!await store.IsValidRecipientAsync(channel.AssetGroupId, channel.Target, cancellationToken))
        {
            store.AddLogs([NotificationLog.Failed(channelId, null, "Alıcı artık grubun doğrulanmış bir üyesi değil; gönderilmedi.", timeProvider.GetUtcNow())]);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Kanal {ChannelId}: alıcı geçersiz, bildirim gönderilmedi.", channelId);
            return null;
        }

        return channel;
    }

    private async Task DeliverAsync(int channelId, EmailMessage message, IReadOnlyList<NotificationLog> sentLogs, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            store.AddLogs([NotificationLog.Failed(channelId, null, ex.Message, timeProvider.GetUtcNow())]);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        store.AddLogs(sentLogs);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }
}
