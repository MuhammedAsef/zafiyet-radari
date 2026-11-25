using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Risk;

/// <summary>Bir bulgunun riskini belirleyen tüm girdiler: zafiyetin kendisi + etkilediği varlığın bağlamı.</summary>
/// <param name="CvssScore">0–10. Bilinmiyorsa null (NVD artık her CVE'yi puanlamıyor).</param>
/// <param name="EpssScore">0–1. Önümüzdeki 30 günde istismar edilme olasılığı.</param>
/// <param name="IsKev">CISA KEV: gerçek saldırılarda istismar edildiği biliniyor.</param>
/// <param name="KevRansomware">KEV kaydına göre fidye yazılımı kampanyalarında kullanılıyor.</param>
/// <param name="IsMaliciousPackage">OSV MAL kaydı: paketin bu sürümü doğrudan zararlı kod içeriyor (zafiyet değil, saldırı).</param>
public sealed record RiskInput(
    decimal? CvssScore,
    decimal? EpssScore,
    bool IsKev,
    bool KevRansomware,
    AssetCriticality Criticality,
    AssetEnvironment Environment,
    bool IsInternetFacing,
    MatchConfidenceLevel Confidence,
    bool IsMaliciousPackage = false);

/// <param name="Score">0–100.</param>
/// <param name="Factors">Skoru neyin belirlediği, kullanıcıya gösterilmek üzere. "Neden Hemen?" sorusunun cevabı.</param>
public sealed record RiskAssessment(decimal Score, FindingPriority Priority, IReadOnlyList<string> Factors);

public interface IRiskScorer
{
    RiskAssessment Assess(RiskInput input);
}
