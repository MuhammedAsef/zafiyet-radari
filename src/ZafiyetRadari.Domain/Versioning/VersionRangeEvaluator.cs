namespace ZafiyetRadari.Domain.Versioning;

/// <summary>Bir paketin birden fazla etkilenen aralığını birlikte değerlendirir.</summary>
public static class VersionRangeEvaluator
{
    /// <summary>
    /// Herhangi bir aralık etkileniyorsa Affected; hiçbiri etkilemiyor ama biri okunamıyorsa Unknown;
    /// aralık hiç yoksa Unknown (OSV bazen sadece tekil sürüm listesi verir, onu "güvenli" saymayız).
    /// </summary>
    public static RangeEvaluation EvaluateAny(
        IVersionComparer comparer,
        string version,
        IEnumerable<VersionRangeSpec> ranges)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(ranges);

        var sawAny = false;
        var sawUnknown = false;

        foreach (var range in ranges)
        {
            sawAny = true;

            switch (comparer.Evaluate(version, range))
            {
                case RangeEvaluation.Affected:
                    return RangeEvaluation.Affected;
                case RangeEvaluation.Unknown:
                    sawUnknown = true;
                    break;
            }
        }

        return !sawAny || sawUnknown ? RangeEvaluation.Unknown : RangeEvaluation.NotAffected;
    }
}
