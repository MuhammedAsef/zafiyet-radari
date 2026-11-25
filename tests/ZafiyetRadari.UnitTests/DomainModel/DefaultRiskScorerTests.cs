using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Risk;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class DefaultRiskScorerTests
{
    private readonly DefaultRiskScorer _scorer = new();

    private static RiskInput Input(
        decimal? cvss = 7.5m,
        decimal? epss = 0.02m,
        bool kev = false,
        bool ransomware = false,
        AssetCriticality criticality = AssetCriticality.High,
        AssetEnvironment environment = AssetEnvironment.Production,
        bool internetFacing = false,
        MatchConfidenceLevel confidence = MatchConfidenceLevel.Exact) =>
        new(cvss, epss, kev, ransomware, criticality, environment, internetFacing, confidence);

    [Fact]
    public void KevOnExposedProduction_IsNow()
    {
        var result = _scorer.Assess(Input(cvss: 9.8m, epss: 0.9m, kev: true, criticality: AssetCriticality.Critical, internetFacing: true));

        Assert.Equal(FindingPriority.Now, result.Priority);
        Assert.Equal(100m, result.Score); // sınırlandırılmış
        Assert.Contains(result.Factors, f => f.Contains("KEV"));
    }

    [Fact]
    public void KevOnInternalProductionWithHighCriticality_IsNow_EvenWithLowCvss()
    {
        var result = _scorer.Assess(Input(cvss: 4.0m, kev: true, criticality: AssetCriticality.High, internetFacing: false));

        Assert.Equal(FindingPriority.Now, result.Priority);
    }

    [Fact]
    public void KevInDevelopment_IsAtLeastThisWeek()
    {
        var result = _scorer.Assess(Input(cvss: 3.0m, kev: true, criticality: AssetCriticality.Low, environment: AssetEnvironment.Development));

        Assert.True(result.Priority <= FindingPriority.ThisWeek);
    }

    [Fact]
    public void LowSeverityLowLikelihoodInDevelopment_IsWatch()
    {
        var result = _scorer.Assess(Input(cvss: 3.1m, epss: 0.001m, criticality: AssetCriticality.Low, environment: AssetEnvironment.Development));

        Assert.Equal(FindingPriority.Watch, result.Priority);
        Assert.InRange(result.Score, 0m, 20m);
    }

    [Fact]
    public void LowConfidenceMatch_NeverRaisesNowAlarm()
    {
        var result = _scorer.Assess(Input(cvss: 10m, epss: 0.97m, kev: true, criticality: AssetCriticality.Critical,
            internetFacing: true, confidence: MatchConfidenceLevel.Low));

        Assert.Equal(FindingPriority.ThisWeek, result.Priority);
        Assert.Contains(result.Factors, f => f.Contains("doğrulama"));
    }

    [Fact]
    public void InternetExposure_IncreasesScore()
    {
        var internalScore = _scorer.Assess(Input(internetFacing: false)).Score;
        var exposedScore = _scorer.Assess(Input(internetFacing: true)).Score;

        Assert.True(exposedScore > internalScore);
    }

    [Fact]
    public void HigherEpss_IncreasesScore()
    {
        var low = _scorer.Assess(Input(epss: 0.01m)).Score;
        var high = _scorer.Assess(Input(epss: 0.5m)).Score;

        Assert.True(high > low);
    }

    [Fact]
    public void MissingCvssAndEpss_UsesDefaults_AndExplainsIt()
    {
        var result = _scorer.Assess(Input(cvss: null, epss: null));

        Assert.InRange(result.Score, 0m, 100m);
        Assert.Contains(result.Factors, f => f.Contains("CVSS puanı yok"));
        Assert.Contains(result.Factors, f => f.Contains("EPSS verisi yok"));
    }

    [Theory]
    [InlineData(-0.1, 0.5)]
    [InlineData(10.1, 0.5)]
    [InlineData(5.0, 1.5)]
    public void OutOfRangeInputs_AreRejected(double cvss, double epss)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _scorer.Assess(Input(cvss: (decimal)cvss, epss: (decimal)epss)));
    }
}
