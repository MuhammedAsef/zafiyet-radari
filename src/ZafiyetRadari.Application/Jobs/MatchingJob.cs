using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Matching;

namespace ZafiyetRadari.Application.Jobs;

/// <summary>
/// Eşleştirme işleri. Worker'da tek işçili "matching" kuyruğunda çalışır (bkz. IMatchingScheduler).
/// Tam tarama varlık varlık ilerler: her varlık kendi işleminde kaydedilir, biri hata verirse diğerleri etkilenmez.
/// </summary>
public sealed class MatchingJob(
    IAssetMatchingService matching,
    IMatchingDataSource dataSource,
    IMatchingScheduler scheduler,
    ILogger<MatchingJob> logger)
{
    /// <summary>
    /// Zamanlanmış (günlük) tetikleyici: tam taramayı kendisi yapmaz, "matching" kuyruğuna koyar.
    /// Böylece tam tarama da tek işçili kuyruktan geçer ve varlık bazlı işlerle çakışmaz.
    /// </summary>
    public Task EnqueueFullAsync()
    {
        scheduler.ScheduleFullMatching();
        return Task.CompletedTask;
    }

    public Task RunForAssetAsync(int assetId, CancellationToken cancellationToken) =>
        matching.MatchAssetAsync(assetId, cancellationToken);

    public async Task RunFullAsync(CancellationToken cancellationToken)
    {
        var assetIds = await dataSource.GetAllAssetIdsAsync(cancellationToken);
        int created = 0, resolved = 0, reopened = 0, failed = 0;

        foreach (var assetId in assetIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var summary = await matching.MatchAssetAsync(assetId, cancellationToken);
                created += summary.Created;
                resolved += summary.Resolved;
                reopened += summary.Reopened;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogError(ex, "Varlık {AssetId} eşleştirilemedi; tam tarama diğer varlıklarla devam ediyor.", assetId);
            }
        }

        logger.LogInformation(
            "Tam eşleştirme bitti: {Assets} varlık, {Created} yeni, {Reopened} yeniden açılan, {Resolved} kapanan bulgu, {Failed} hata",
            assetIds.Count, created, reopened, resolved, failed);

        if (failed > 0)
        {
            throw new InvalidOperationException($"{failed} varlık eşleştirilemedi. Ayrıntılar logda.");
        }
    }
}
