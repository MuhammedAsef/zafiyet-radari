using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Matching;
using ZafiyetRadari.Domain.Risk;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.Application.Matching;

public interface IAssetMatchingService
{
    Task<MatchRunSummary> MatchAssetAsync(int assetId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bir varlık için uçtan uca eşleştirme:
/// adaylar (SQL: ekosistem + ad) → sürüm aralığı değerlendirmesi (Domain) → risk skoru (Domain) → bulgularla uzlaştırma (Domain) → kaydet.
/// Kullanıcı yetkisi gerektirmez: sadece arka plan işinden (Worker) çağrılır.
/// </summary>
internal sealed class AssetMatchingService(
    IMatchingDataSource dataSource,
    IFindingRepository findings,
    IUnitOfWork unitOfWork,
    IEnumerable<IVersionComparer> comparers,
    IRiskScorer riskScorer,
    TimeProvider timeProvider,
    IFindingNotifier notifier,
    ILogger<AssetMatchingService> logger) : IAssetMatchingService
{
    private readonly IReadOnlyList<IVersionComparer> _comparers = comparers.ToList();

    public async Task<MatchRunSummary> MatchAssetAsync(int assetId, CancellationToken cancellationToken = default)
    {
        var context = await dataSource.GetAssetContextAsync(assetId, cancellationToken);
        if (context is null)
        {
            return MatchRunSummary.Empty(assetId); // varlık iş kuyruktayken silinmiş
        }

        var data = await dataSource.GetMatchDataAsync(assetId, cancellationToken);
        var matches = VersionMatcher.Match(data.Candidates, _comparers);

        var scored = new List<ScoredMatch>(matches.Count);
        foreach (var match in matches)
        {
            if (!data.RiskData.TryGetValue(match.VulnerabilityId, out var vulnerability))
            {
                continue;
            }

            var risk = riskScorer.Assess(new RiskInput(
                vulnerability.CvssScore,
                vulnerability.EpssScore,
                vulnerability.IsKev,
                vulnerability.KevRansomware,
                context.Criticality,
                context.Environment,
                context.IsInternetFacing,
                match.Confidence,
                vulnerability.IsMaliciousPackage));

            scored.Add(new ScoredMatch(match, risk));
        }

        var existing = await findings.GetForAssetAsync(assetId, cancellationToken);
        var result = FindingReconciler.Reconcile(existing, scored, timeProvider.GetUtcNow());

        findings.AddRange(result.NewFindings);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Kayıttan sonra: yeni bulguların kimlikleri artık belli. Bildirim kuyruğa alınır, burada mail gönderilmez.
        var notifiable = result.NewFindings
            .Concat(result.ReopenedFindings)
            .Select(f =>
            {
                data.RiskData.TryGetValue(f.VulnerabilityId, out var vulnerability);
                return new NotifiableFinding(f.Id, f.CurrentPriority, vulnerability?.IsKev ?? false, vulnerability?.IsMaliciousPackage ?? false);
            })
            .ToList();

        await notifier.NotifyAsync(assetId, notifiable, cancellationToken);

        var summary = new MatchRunSummary(
            assetId, scored.Count, result.NewFindings.Count, result.Updated, result.Reopened, result.Resolved);

        logger.LogInformation(
            "Varlık {AssetId} eşleştirildi: {Candidates} aday, {Matches} eşleşme, {Created} yeni, {Reopened} yeniden açılan, {Resolved} kapanan bulgu",
            assetId, data.Candidates.Count, summary.Matches, summary.Created, summary.Reopened, summary.Resolved);

        return summary;
    }
}
