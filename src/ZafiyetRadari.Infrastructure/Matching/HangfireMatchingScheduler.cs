using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Jobs;

namespace ZafiyetRadari.Infrastructure.Matching;

/// <summary>
/// Eşleştirme işlerini "matching" kuyruğuna koyar. Worker bu kuyruğu tek işçiyle dinler:
/// işler sırayla çalışır, aynı bulgu kaydını iki iş aynı anda yazmaz (tekil kısıt yarışı olmaz).
/// </summary>
internal sealed class HangfireMatchingScheduler(IBackgroundJobClient client) : IMatchingScheduler
{
    public const string Queue = "matching";

    public void ScheduleAssetMatching(int assetId) =>
        client.Create(
            Job.FromExpression<MatchingJob>(job => job.RunForAssetAsync(assetId, CancellationToken.None)),
            new EnqueuedState(Queue));

    public void ScheduleFullMatching() =>
        client.Create(
            Job.FromExpression<MatchingJob>(job => job.RunFullAsync(CancellationToken.None)),
            new EnqueuedState(Queue));
}

internal sealed class HangfireNotificationScheduler(IBackgroundJobClient client) : INotificationScheduler
{
    public void ScheduleFindingNotification(int channelId, IReadOnlyCollection<long> findingIds, DateTimeOffset triggeredAt)
    {
        var ids = findingIds.ToArray();
        client.Enqueue<NotificationJob>(job => job.SendFindingsAsync(channelId, ids, triggeredAt, CancellationToken.None));
    }
}
