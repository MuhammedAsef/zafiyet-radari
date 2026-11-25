namespace ZafiyetRadari.Application.Abstractions.Jobs;

/// <summary>
/// Eşleştirme işlerini kuyruğa koyar. Application, Hangfire'ı bilmez; Infrastructure uygular.
/// Tüm eşleştirme işleri tek işçili ayrı bir kuyrukta sırayla çalışır (aynı bulguyu iki iş aynı anda yazmaz).
/// </summary>
public interface IMatchingScheduler
{
    void ScheduleAssetMatching(int assetId);

    void ScheduleFullMatching();
}

/// <summary>Bildirim gönderimini kuyruğa koyar (default kuyruk). SMTP hatası Hangfire'da tekrar denenir.</summary>
public interface INotificationScheduler
{
    void ScheduleFindingNotification(int channelId, IReadOnlyCollection<long> findingIds, DateTimeOffset triggeredAt);
}
