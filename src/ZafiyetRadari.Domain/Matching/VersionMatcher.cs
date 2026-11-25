using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.Domain.Matching;

/// <summary>
/// Veritabanından gelen aday: bileşenin adı/ekosistemi bir zafiyetin etkilenen paketiyle aynı.
/// Bir zafiyetin aynı paket için birden çok kaydı olabilir; aralıklar birleştirilir.
/// </summary>
public sealed record MatchCandidate(
    long ComponentId,
    string Ecosystem,
    string Version,
    int VulnerabilityId,
    IReadOnlyList<VersionRangeSpec> Ranges);

public sealed record VersionMatch(long ComponentId, int VulnerabilityId, MatchConfidenceLevel Confidence);

/// <summary>
/// Adayları sürüm aralıklarına göre süzer.
/// Etkileniyor → Exact (purl + OSV aralığı). Belirsiz (aralık yok/okunamadı) → Low: sessizce "güvenli" saymak yerine
/// kullanıcıya doğrulanacak bir bulgu olarak gösterilir. Etkilenmiyor → bulgu yok.
/// </summary>
public static class VersionMatcher
{
    public static IReadOnlyList<VersionMatch> Match(IEnumerable<MatchCandidate> candidates, IReadOnlyList<IVersionComparer> comparers)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(comparers);

        var matches = new List<VersionMatch>();

        var grouped = candidates.GroupBy(c => (c.ComponentId, c.VulnerabilityId));
        foreach (var group in grouped)
        {
            var first = group.First();
            var comparer = comparers.FirstOrDefault(c => c.Supports(first.Ecosystem));
            if (comparer is null)
            {
                continue; // desteklenmeyen ekosistem: tahmin yürütülmez
            }

            var ranges = group.SelectMany(c => c.Ranges).ToList();
            var evaluation = VersionRangeEvaluator.EvaluateAny(comparer, first.Version, ranges);

            switch (evaluation)
            {
                case RangeEvaluation.Affected:
                    matches.Add(new VersionMatch(first.ComponentId, first.VulnerabilityId, MatchConfidenceLevel.Exact));
                    break;
                case RangeEvaluation.Unknown:
                    matches.Add(new VersionMatch(first.ComponentId, first.VulnerabilityId, MatchConfidenceLevel.Low));
                    break;
            }
        }

        return matches;
    }
}
