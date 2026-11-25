using System.Globalization;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Risk;

/// <summary>
/// Risk = Tehdit × Bağlam
///
/// Tehdit (0–1)  = %50 şiddet (CVSS/10) + %50 istismar olasılığı
///                 İstismar olasılığı: KEV'de ise 1, değilse √EPSS
///                 (EPSS değerleri sıfıra yığılı; karekök küçük farkları ayırt edilebilir kılar: 0.01→0.10, 0.10→0.32)
/// Bağlam (çarpan) = kritiklik × internete açıklık × ortam × eşleşme güveni
/// Skor (0–100)  = Tehdit × Bağlam × 100, sınırlandırılmış
///
/// Kova: ≥70 Hemen, ≥45 Bu hafta, ≥20 Planla, altı İzle. Ardından iki kural:
///   • KEV'deki zafiyet en az "Bu hafta"; üretimde ve (internete açık ya da kritik) varlıktaysa "Hemen".
///   • Düşük güvenli eşleşme (isim benzerliği) en fazla "Bu hafta": doğrulanmamış eşleşme alarm yağdırmasın.
///
/// Kötü amaçlı paket (OSV MAL-*): CVSS/EPSS anlamsızdır; kurulu olması zaten ihlaldir. Skor 100, öncelik "Hemen"
/// (düşük güvenli eşleşme kuralı yine geçerlidir).
/// </summary>
public sealed class DefaultRiskScorer : IRiskScorer
{
    private const double DefaultSeverity = 0.5;     // CVSS yoksa orta şiddet varsay
    private const double DefaultEpss = 0.01;        // EPSS yoksa düşük ama sıfır olmayan olasılık

    private const decimal NowThreshold = 70m;
    private const decimal ThisWeekThreshold = 45m;
    private const decimal PlanThreshold = 20m;

    public RiskAssessment Assess(RiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var factors = new List<string>();

        if (input.IsMaliciousPackage)
        {
            factors.Add("Kötü amaçlı paket (OSV MAL): bu sürüm doğrudan zararlı kod içeriyor, kurulu olduğu her ortam risk altında");
            if (input.Confidence == MatchConfidenceLevel.Low)
            {
                factors.Add("Eşleşme isim benzerliğine dayanıyor; doğrulama gerekli");
            }

            return new RiskAssessment(100m, ApplyRules(FindingPriority.Now, input), factors);
        }

        var severity = input.CvssScore is { } cvss ? (double)cvss / 10 : DefaultSeverity;
        factors.Add(input.CvssScore is { } score
            ? $"CVSS {score.ToString("0.0", CultureInfo.InvariantCulture)}"
            : "CVSS puanı yok; orta şiddet varsayıldı");

        double exploitLikelihood;
        if (input.IsKev)
        {
            exploitLikelihood = 1.0;
            factors.Add("Gerçek saldırılarda istismar ediliyor (CISA KEV)");
            if (input.KevRansomware)
            {
                factors.Add("Fidye yazılımı kampanyalarında kullanılıyor");
            }
        }
        else
        {
            var epss = input.EpssScore is { } e ? (double)e : DefaultEpss;
            exploitLikelihood = Math.Sqrt(epss);
            factors.Add(input.EpssScore is { } epssScore
                ? $"30 günde istismar olasılığı %{(epssScore * 100).ToString("0.#", CultureInfo.InvariantCulture)} (EPSS)"
                : "EPSS verisi yok");
        }

        var threat = (0.5 * severity) + (0.5 * exploitLikelihood);
        var context = CriticalityWeight(input.Criticality)
            * (input.IsInternetFacing ? 1.2 : 1.0)
            * EnvironmentWeight(input.Environment)
            * ConfidenceWeight(input.Confidence);

        factors.Add($"Varlık kritikliği: {CriticalityLabel(input.Criticality)}");
        factors.Add($"Ortam: {EnvironmentLabel(input.Environment)}");
        if (input.IsInternetFacing)
        {
            factors.Add("İnternete açık");
        }

        if (input.Confidence == MatchConfidenceLevel.Low)
        {
            factors.Add("Eşleşme isim benzerliğine dayanıyor; doğrulama gerekli");
        }

        var riskScore = Math.Clamp(decimal.Round((decimal)(threat * context * 100), 2), 0m, 100m);
        var priority = ApplyRules(BucketOf(riskScore), input);

        return new RiskAssessment(riskScore, priority, factors);
    }

    private static FindingPriority ApplyRules(FindingPriority priority, RiskInput input)
    {
        if (input.IsKev)
        {
            priority = Highest(priority, FindingPriority.ThisWeek);

            var exposedProduction = input.Environment == AssetEnvironment.Production
                && (input.IsInternetFacing || input.Criticality >= AssetCriticality.High);

            if (exposedProduction)
            {
                priority = FindingPriority.Now;
            }
        }

        if (input.Confidence == MatchConfidenceLevel.Low)
        {
            priority = Lowest(priority, FindingPriority.ThisWeek);
        }

        return priority;
    }

    private static FindingPriority BucketOf(decimal score) => score switch
    {
        >= NowThreshold => FindingPriority.Now,
        >= ThisWeekThreshold => FindingPriority.ThisWeek,
        >= PlanThreshold => FindingPriority.Plan,
        _ => FindingPriority.Watch
    };

    // FindingPriority'de küçük değer daha acil: Now = 1 ... Watch = 4.
    private static FindingPriority Highest(FindingPriority a, FindingPriority b) => a < b ? a : b;

    private static FindingPriority Lowest(FindingPriority a, FindingPriority b) => a > b ? a : b;

    private static double CriticalityWeight(AssetCriticality criticality) => criticality switch
    {
        AssetCriticality.Low => 0.6,
        AssetCriticality.Medium => 0.8,
        AssetCriticality.High => 1.0,
        AssetCriticality.Critical => 1.2,
        _ => throw new ArgumentOutOfRangeException(nameof(criticality))
    };

    private static double EnvironmentWeight(AssetEnvironment environment) => environment switch
    {
        AssetEnvironment.Development => 0.5,
        AssetEnvironment.Test => 0.7,
        AssetEnvironment.Production => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(environment))
    };

    private static double ConfidenceWeight(MatchConfidenceLevel confidence) => confidence switch
    {
        MatchConfidenceLevel.Low => 0.5,
        MatchConfidenceLevel.Medium => 0.75,
        MatchConfidenceLevel.High => 0.9,
        MatchConfidenceLevel.Exact => 1.0,
        _ => throw new ArgumentOutOfRangeException(nameof(confidence))
    };

    private static string CriticalityLabel(AssetCriticality criticality) => criticality switch
    {
        AssetCriticality.Low => "Düşük",
        AssetCriticality.Medium => "Orta",
        AssetCriticality.High => "Yüksek",
        _ => "Kritik"
    };

    private static string EnvironmentLabel(AssetEnvironment environment) => environment switch
    {
        AssetEnvironment.Development => "Geliştirme",
        AssetEnvironment.Test => "Test",
        _ => "Üretim"
    };

    private static void Validate(RiskInput input)
    {
        if (input.CvssScore is < 0m or > 10m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "CVSS 0 ile 10 arasında olmalı.");
        }

        if (input.EpssScore is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "EPSS 0 ile 1 arasında olmalı.");
        }
    }
}
