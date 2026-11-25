namespace ZafiyetRadari.Domain.Vulnerabilities;

/// <summary>
/// CVSS v3.0 / v3.1 base score hesaplayıcı (FIRST CVSS v3.1 spesifikasyonu, bölüm 7).
/// OSV, puanı değil vektörü verir; puanı kendimiz hesaplarız.
/// Temporal/environmental metrikler vektörde bulunabilir, base score'a etkileri yoktur, yok sayılır.
/// </summary>
public static class CvssV3Calculator
{
    public const int MaxVectorLength = 200;

    public static bool TryCalculateBaseScore(string? vector, out decimal score)
    {
        score = 0;

        if (string.IsNullOrWhiteSpace(vector) || vector.Length > MaxVectorLength)
        {
            return false;
        }

        var parts = vector.Trim().Split('/');
        if (parts[0] is not ("CVSS:3.0" or "CVSS:3.1"))
        {
            return false;
        }

        var metrics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts.AsSpan(1))
        {
            var pair = part.Split(':');
            if (pair.Length != 2 || !metrics.TryAdd(pair[0], pair[1]))
            {
                return false; // bozuk ya da tekrarlanan metrik
            }
        }

        if (!metrics.TryGetValue("S", out var scope) || scope is not ("U" or "C"))
        {
            return false;
        }

        var scopeChanged = scope == "C";

        var attackVector = Weight(metrics, "AV", v => v switch { "N" => 0.85, "A" => 0.62, "L" => 0.55, "P" => 0.2, _ => null });
        var attackComplexity = Weight(metrics, "AC", v => v switch { "L" => 0.77, "H" => 0.44, _ => null });
        var privileges = Weight(metrics, "PR", v => v switch
        {
            "N" => 0.85,
            "L" => scopeChanged ? 0.68 : 0.62,
            "H" => scopeChanged ? 0.5 : 0.27,
            _ => null
        });
        var userInteraction = Weight(metrics, "UI", v => v switch { "N" => 0.85, "R" => 0.62, _ => null });
        var confidentiality = Weight(metrics, "C", ImpactWeight);
        var integrity = Weight(metrics, "I", ImpactWeight);
        var availability = Weight(metrics, "A", ImpactWeight);

        if (attackVector is null || attackComplexity is null || privileges is null || userInteraction is null
            || confidentiality is null || integrity is null || availability is null)
        {
            return false;
        }

        var iss = 1 - ((1 - confidentiality.Value) * (1 - integrity.Value) * (1 - availability.Value));
        var impact = scopeChanged
            ? (7.52 * (iss - 0.029)) - (3.25 * Math.Pow(iss - 0.02, 15))
            : 6.42 * iss;
        var exploitability = 8.22 * attackVector.Value * attackComplexity.Value * privileges.Value * userInteraction.Value;

        double baseScore;
        if (impact <= 0)
        {
            baseScore = 0;
        }
        else
        {
            baseScore = scopeChanged
                ? RoundUp(Math.Min(1.08 * (impact + exploitability), 10))
                : RoundUp(Math.Min(impact + exploitability, 10));
        }

        score = (decimal)Math.Round(baseScore, 1);
        return true;
    }

    private static double? ImpactWeight(string value) => value switch
    {
        "H" => 0.56,
        "L" => 0.22,
        "N" => 0,
        _ => null
    };

    private static double? Weight(Dictionary<string, string> metrics, string key, Func<string, double?> map) =>
        metrics.TryGetValue(key, out var value) ? map(value) : null;

    /// <summary>Spesifikasyondaki Roundup: kayan nokta hatalarına karşı tamsayı üzerinden yukarı yuvarlama.</summary>
    private static double RoundUp(double value)
    {
        var scaled = (long)Math.Round(value * 100_000);
        return scaled % 10_000 == 0
            ? scaled / 100_000.0
            : (Math.Floor(scaled / 10_000.0) + 1) / 10.0;
    }
}
