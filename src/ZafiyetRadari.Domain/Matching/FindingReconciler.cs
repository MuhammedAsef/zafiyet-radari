using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Risk;

namespace ZafiyetRadari.Domain.Matching;

/// <summary>Risk hesaplanmış eşleşme.</summary>
public sealed record ScoredMatch(VersionMatch Match, RiskAssessment Risk);

/// <param name="ReopenedFindings">Regresyonla veya risk kabulü süresi dolarak yeniden açılanlar (bildirim için).</param>
/// <param name="EscalatedFindings">
/// Hâlâ açık olup önceliği yükselenler (ör. CISA KEV'e eklendi, EPSS arttı, varlık kritikliği yükseltildi).
/// Daha önce bildirilmiş bir bulgu artık daha acilse ekip bunu ayrıca duymalı.
/// </param>
public sealed record ReconcileResult(
    IReadOnlyList<Finding> NewFindings,
    IReadOnlyList<Finding> ReopenedFindings,
    int Updated,
    int Resolved,
    IReadOnlyList<Finding> EscalatedFindings)
{
    public int Reopened => ReopenedFindings.Count;
}

/// <summary>
/// Bir varlığın güncel eşleşmelerini mevcut bulgularla uzlaştırır (saf mantık, veritabanı yok):
/// • Yeni eşleşme → yeni bulgu (Open).
/// • Devam eden eşleşme → LastSeen + risk güncellenir; "Düzeltildi" olan tekrar görülürse yeniden açılır (regresyon).
///   Açık bulgunun önceliği yükseldiyse "yükselen" olarak işaretlenir (bildirim için).
/// • Kaybolan eşleşme → aksiyon bekleyen bulgu sistem tarafından "Düzeltildi" yapılır.
///   Risk kabulü ve yanlış alarm kararlarına dokunulmaz: kullanıcının kararıdır.
/// • Süresi dolan risk kabulleri yeniden açılır.
/// </summary>
public static class FindingReconciler
{
    public static ReconcileResult Reconcile(IReadOnlyCollection<Finding> existing, IReadOnlyList<ScoredMatch> matches, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(matches);

        var index = new Dictionary<(long, int), Finding>();
        foreach (var finding in existing)
        {
            index.TryAdd((finding.ComponentId, finding.VulnerabilityId), finding);
        }

        var seen = new HashSet<(long, int)>();
        var created = new List<Finding>();
        var reopened = new List<Finding>();
        var escalated = new List<Finding>();
        int updated = 0, resolved = 0;

        foreach (var scored in matches)
        {
            var key = (scored.Match.ComponentId, scored.Match.VulnerabilityId);
            if (!seen.Add(key))
            {
                continue;
            }

            if (index.TryGetValue(key, out var finding))
            {
                var wasFixed = finding.CurrentStatus == FindingStatus.Fixed;
                var previousPriority = finding.CurrentPriority;
                finding.MarkSeen(now);
                finding.UpdateConfidence(scored.Match.Confidence);
                finding.UpdateRisk(scored.Risk.Score, scored.Risk.Priority);

                if (wasFixed)
                {
                    reopened.Add(finding);
                }
                else
                {
                    updated++;

                    // Küçük değer daha acil (Now = 1). Kullanıcının kapattığı/kabul ettiği bulgular rahatsız edilmez.
                    if (finding.IsActionable && finding.CurrentPriority < previousPriority)
                    {
                        escalated.Add(finding);
                    }
                }

                continue;
            }

            var result = Finding.Create(
                scored.Match.ComponentId, scored.Match.VulnerabilityId, scored.Match.Confidence,
                scored.Risk.Score, scored.Risk.Priority, now);

            if (result.IsSuccess)
            {
                created.Add(result.Value);
            }
        }

        foreach (var finding in existing)
        {
            if (!seen.Contains((finding.ComponentId, finding.VulnerabilityId)) && finding.IsActionable)
            {
                finding.ResolveBySystem(now);
                resolved++;
            }

            if (finding.ExpireRiskAcceptance(now))
            {
                reopened.Add(finding);
            }
        }

        return new ReconcileResult(created, reopened, updated, resolved, escalated);
    }
}
