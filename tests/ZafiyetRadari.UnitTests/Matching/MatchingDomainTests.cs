using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Matching;
using ZafiyetRadari.Domain.Risk;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.UnitTests.Matching;

public sealed class MatchingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly IVersionComparer[] Comparers = [new SemVerComparer()];

    [Fact]
    public void Matcher_AffectedVersion_IsExactMatch()
    {
        var matches = VersionMatcher.Match([Candidate(1, "4.17.20", 100, Range("0", fixedIn: "4.17.21"))], Comparers);

        Assert.Equal(new VersionMatch(1, 100, MatchConfidenceLevel.Exact), Assert.Single(matches));
    }

    [Fact]
    public void Matcher_FixedVersion_IsNotMatched()
    {
        var matches = VersionMatcher.Match([Candidate(1, "4.17.21", 100, Range("0", fixedIn: "4.17.21"))], Comparers);

        Assert.Empty(matches);
    }

    [Fact]
    public void Matcher_MergesRangesFromMultipleRows()
    {
        // Aynı zafiyetin iki aralığı ayrı satırlarda gelir; ikinci aralık bileşeni etkiler.
        var matches = VersionMatcher.Match(
            [
                Candidate(1, "2.5.0", 100, Range("1.0.0", fixedIn: "1.5.0")),
                Candidate(1, "2.5.0", 100, Range("2.0.0", fixedIn: "2.6.0"))
            ],
            Comparers);

        Assert.Equal(MatchConfidenceLevel.Exact, Assert.Single(matches).Confidence);
    }

    [Fact]
    public void Matcher_NoRanges_IsLowConfidence_NotSilentlySafe()
    {
        var matches = VersionMatcher.Match([new MatchCandidate(1, Ecosystems.Npm, "1.0.0", 100, [])], Comparers);

        Assert.Equal(MatchConfidenceLevel.Low, Assert.Single(matches).Confidence);
    }

    [Fact]
    public void Reconciler_CreatesNewFindings()
    {
        var result = FindingReconciler.Reconcile([], [Scored(1, 100)], Now);

        var finding = Assert.Single(result.NewFindings);
        Assert.Equal(FindingStatus.Open, finding.CurrentStatus);
        Assert.Equal(FindingPriority.Now, finding.CurrentPriority);
    }

    [Fact]
    public void Reconciler_ResolvesFindingThatNoLongerMatches()
    {
        var existing = NewFinding(1, 100);

        var result = FindingReconciler.Reconcile([existing], [], Now.AddDays(1));

        Assert.Equal(1, result.Resolved);
        Assert.Equal(FindingStatus.Fixed, existing.CurrentStatus);
        Assert.NotNull(existing.ResolvedAt);
    }

    [Fact]
    public void Reconciler_ReopensFixedFinding_WhenSeenAgain()
    {
        var existing = NewFinding(1, 100);
        FindingReconciler.Reconcile([existing], [], Now.AddDays(1)); // düzeltildi

        var result = FindingReconciler.Reconcile([existing], [Scored(1, 100)], Now.AddDays(2)); // eski sürüme dönüldü

        Assert.Equal(1, result.Reopened);
        Assert.Equal(FindingStatus.Open, existing.CurrentStatus);
        Assert.Empty(result.NewFindings);
    }

    [Fact]
    public void Reconciler_DoesNotTouchUserDecisions()
    {
        var existing = NewFinding(1, 100);
        existing.ChangeStatus(FindingStatus.FalsePositive, "user-1", "Kullanılmayan kod yolu.", null, Now);

        var result = FindingReconciler.Reconcile([existing], [], Now.AddDays(1));

        Assert.Equal(0, result.Resolved);
        Assert.Equal(FindingStatus.FalsePositive, existing.CurrentStatus);
    }

    [Fact]
    public void Reconciler_UpdatesRiskOfExistingFinding()
    {
        var existing = NewFinding(1, 100);
        var lowered = new ScoredMatch(new VersionMatch(1, 100, MatchConfidenceLevel.Exact), new RiskAssessment(30m, FindingPriority.Plan, []));

        var result = FindingReconciler.Reconcile([existing], [lowered], Now.AddDays(1));

        Assert.Equal(1, result.Updated);
        Assert.Equal(30m, existing.RiskScore);
        Assert.Equal(FindingPriority.Plan, existing.CurrentPriority);
        Assert.Equal(Now.AddDays(1), existing.LastSeenAt);
    }

    [Fact]
    public void RiskScorer_MaliciousPackage_IsAlwaysNow()
    {
        var result = new DefaultRiskScorer().Assess(new RiskInput(
            CvssScore: null, EpssScore: null, IsKev: false, KevRansomware: false,
            AssetCriticality.Low, AssetEnvironment.Development, IsInternetFacing: false,
            MatchConfidenceLevel.Exact, IsMaliciousPackage: true));

        Assert.Equal(100m, result.Score);
        Assert.Equal(FindingPriority.Now, result.Priority);
    }

    private static MatchCandidate Candidate(long componentId, string version, int vulnerabilityId, VersionRangeSpec range) =>
        new(componentId, Ecosystems.Npm, version, vulnerabilityId, [range]);

    private static VersionRangeSpec Range(string introduced, string? fixedIn = null, string? lastAffected = null) =>
        new(introduced, fixedIn, lastAffected);

    private static ScoredMatch Scored(long componentId, int vulnerabilityId) =>
        new(new VersionMatch(componentId, vulnerabilityId, MatchConfidenceLevel.Exact), new RiskAssessment(85m, FindingPriority.Now, []));

    private static Finding NewFinding(long componentId, int vulnerabilityId) =>
        Finding.Create(componentId, vulnerabilityId, MatchConfidenceLevel.Exact, 85m, FindingPriority.Now, Now).Value;
}
