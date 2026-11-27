using Hangfire;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Application.Jobs;

namespace ZafiyetRadari.Worker.Jobs;

/// <summary>
/// Tüm zamanlanmış işlerin tek bir yerden tanımlandığı kayıt noktası.
/// İş sınıfları Application katmanındadır; böylece Web (Hangfire paneli) de onları çözebilir. Worker sadece host.
/// </summary>
internal static class RecurringJobRegistry
{
    public static void Register(IRecurringJobManager jobs)
    {
        jobs.AddOrUpdate<HeartbeatJob>(
            "ops-heartbeat",
            job => job.RunAsync(CancellationToken.None),
            Cron.Hourly());

        jobs.AddOrUpdate<AuditChainAnchorJob>(
            "ops-audit-anchor",
            job => job.RunAsync(CancellationToken.None),
            Cron.Hourly());

        // Zafiyet kaynakları. Dakikalar kaydırıldı: aynı anda büyük indirme/yazma olmasın.
        jobs.AddOrUpdate<SyncVulnerabilityFeedJob>(
            "vuln-sync-osv-npm",
            job => job.RunAsync(FeedSources.OsvNpm, CancellationToken.None),
            "0 */6 * * *");

        jobs.AddOrUpdate<SyncVulnerabilityFeedJob>(
            "vuln-sync-osv-nuget",
            job => job.RunAsync(FeedSources.OsvNuGet, CancellationToken.None),
            "10 */6 * * *");

        jobs.AddOrUpdate<SyncVulnerabilityFeedJob>(
            "vuln-sync-kev",
            job => job.RunAsync(FeedSources.Kev, CancellationToken.None),
            "20 */6 * * *");

        // Günlük tam eşleştirme: risk kabulü süreleri dolanlar açılır, kaçan değişiklikler yakalanır.
        jobs.AddOrUpdate<MatchingJob>(
            "match-all-daily",
            job => job.EnqueueFullAsync(),
            "45 2 * * *");

        // Haftalık özet: pazartesi 06:30 UTC = 09:30 İstanbul. Günlük eşleştirmeden (02:45) sonra, güncel verilerle.
        jobs.AddOrUpdate<NotificationJob>(
            "notify-weekly-digest",
            job => job.EnqueueWeeklyDigestsAsync(CancellationToken.None),
            "30 6 * * 1");

        // EPSS skorları her gün ~13:30 UTC'de yayımlanır.
        jobs.AddOrUpdate<SyncVulnerabilityFeedJob>(
            "vuln-sync-epss",
            job => job.RunAsync(FeedSources.Epss, CancellationToken.None),
            "0 14 * * *");
    }
}
