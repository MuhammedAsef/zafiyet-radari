using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class FindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private static Finding NewFinding() =>
        Finding.Create(componentId: 1, vulnerabilityId: 1, MatchConfidenceLevel.Exact, 75m, FindingPriority.Now, Now).Value;

    [Fact]
    public void Create_StartsOpen_WithHistory()
    {
        var finding = NewFinding();

        Assert.Equal(FindingStatus.Open, finding.CurrentStatus);
        Assert.True(finding.IsActionable);
        Assert.Single(finding.FindingHistories);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Create_WithInvalidRiskScore_Fails(double riskScore)
    {
        var result = Finding.Create(1, 1, MatchConfidenceLevel.Exact, (decimal)riskScore, FindingPriority.Now, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FindingErrors.InvalidRiskScore, result.Error);
    }

    [Fact]
    public void ChangeStatus_ToSameStatus_Fails()
    {
        var result = NewFinding().ChangeStatus(FindingStatus.Open, "user-1", null, null, Now);

        Assert.Equal(FindingErrors.StatusUnchanged, result.Error);
    }

    [Fact]
    public void RiskAccepted_WithoutJustification_Fails()
    {
        var result = NewFinding().ChangeStatus(FindingStatus.RiskAccepted, "user-1", "  ", Today.AddDays(30), Now);

        Assert.Equal(FindingErrors.JustificationRequired, result.Error);
    }

    [Fact]
    public void RiskAccepted_WithPastOrTodayDate_Fails()
    {
        var result = NewFinding().ChangeStatus(FindingStatus.RiskAccepted, "user-1", "Telafi edici kontrol var.", Today, Now);

        Assert.Equal(FindingErrors.RiskAcceptanceDateRequired, result.Error);
    }

    [Fact]
    public void RiskAccepted_BeyondMaximumDuration_Fails()
    {
        var until = Today.AddDays(Finding.MaxRiskAcceptanceDays + 1);

        var result = NewFinding().ChangeStatus(FindingStatus.RiskAccepted, "user-1", "Telafi edici kontrol var.", until, Now);

        Assert.Equal(FindingErrors.RiskAcceptanceTooLong, result.Error);
    }

    [Fact]
    public void RiskAccepted_WithJustificationAndDate_Succeeds_AndIsRecorded()
    {
        var finding = NewFinding();
        var until = Today.AddDays(30);

        var result = finding.ChangeStatus(FindingStatus.RiskAccepted, "user-1", "WAF kuralı ile engellendi.", until, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(FindingStatus.RiskAccepted, finding.CurrentStatus);
        Assert.Equal(until, finding.RiskAcceptedUntil);

        var last = finding.FindingHistories.Last();
        Assert.Equal((byte)FindingStatus.Open, last.FromStatus);
        Assert.Equal((byte)FindingStatus.RiskAccepted, last.ToStatus);
        Assert.Equal("user-1", last.ChangedByUserId);
    }

    [Fact]
    public void FalsePositive_WithoutJustification_Fails()
    {
        var result = NewFinding().ChangeStatus(FindingStatus.FalsePositive, "user-1", null, null, Now);

        Assert.Equal(FindingErrors.JustificationRequired, result.Error);
    }

    [Fact]
    public void Fixed_SetsResolvedAt_AndReopenClearsIt()
    {
        var finding = NewFinding();

        finding.ChangeStatus(FindingStatus.Fixed, "user-1", null, null, Now);
        Assert.Equal(Now, finding.ResolvedAt);

        finding.ChangeStatus(FindingStatus.Open, "user-1", null, null, Now.AddHours(1));
        Assert.Null(finding.ResolvedAt);
    }

    [Fact]
    public void MarkSeen_OnFixedFinding_ReopensAsRegression()
    {
        var finding = NewFinding();
        finding.ChangeStatus(FindingStatus.Fixed, "user-1", null, null, Now);

        finding.MarkSeen(Now.AddDays(1));

        Assert.Equal(FindingStatus.Open, finding.CurrentStatus);
        Assert.Null(finding.FindingHistories.Last().ChangedByUserId); // sistem değişikliği
    }

    [Fact]
    public void ResolveBySystem_OnlyAffectsActionableFindings()
    {
        var finding = NewFinding();
        finding.ChangeStatus(FindingStatus.FalsePositive, "user-1", "Kullanılmayan kod yolu.", null, Now);
        var historyCount = finding.FindingHistories.Count;

        finding.ResolveBySystem(Now.AddDays(1));

        Assert.Equal(FindingStatus.FalsePositive, finding.CurrentStatus);
        Assert.Equal(historyCount, finding.FindingHistories.Count);
    }

    [Fact]
    public void ExpireRiskAcceptance_AfterEndDate_Reopens()
    {
        var finding = NewFinding();
        finding.ChangeStatus(FindingStatus.RiskAccepted, "user-1", "Geçici istisna.", Today.AddDays(10), Now);

        Assert.False(finding.ExpireRiskAcceptance(Now.AddDays(5)));
        Assert.True(finding.ExpireRiskAcceptance(Now.AddDays(11)));

        Assert.Equal(FindingStatus.Open, finding.CurrentStatus);
        Assert.Null(finding.RiskAcceptedUntil);
    }
}
