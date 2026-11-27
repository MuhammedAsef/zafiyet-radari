using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.UnitTests.Notifications;

public sealed class NotificationDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(FindingPriority.Now, true)]
    [InlineData(FindingPriority.ThisWeek, true)]
    [InlineData(FindingPriority.Plan, false)]
    [InlineData(FindingPriority.Watch, false)]
    public void Channel_AcceptsPriorityAtOrAboveThreshold(FindingPriority priority, bool expected)
    {
        var channel = Channel(FindingPriority.ThisWeek, onlyKev: false);

        Assert.Equal(expected, channel.Accepts(priority, isKev: false, isMaliciousPackage: false));
    }

    [Fact]
    public void OnlyKevChannel_AcceptsKevAndMalicious_ButNotOthers()
    {
        var channel = Channel(FindingPriority.Watch, onlyKev: true);

        Assert.False(channel.Accepts(FindingPriority.Now, isKev: false, isMaliciousPackage: false));
        Assert.True(channel.Accepts(FindingPriority.ThisWeek, isKev: true, isMaliciousPackage: false));
        Assert.True(channel.Accepts(FindingPriority.Now, isKev: false, isMaliciousPackage: true));
    }

    [Fact]
    public void DisabledChannel_AcceptsNothing()
    {
        var channel = Channel(FindingPriority.Watch, onlyKev: false);
        channel.SetEnabled(false);

        Assert.False(channel.Accepts(FindingPriority.Now, isKev: true, isMaliciousPackage: true));
    }

    [Fact]
    public void Channel_RequiresName()
    {
        Assert.Equal(NotificationErrors.InvalidName,
            NotificationChannel.CreateEmail(1, "  ", "a@b.com", FindingPriority.Now, false, Now).Error);
    }

    [Fact]
    public void FailedLog_TruncatesLongError()
    {
        var log = NotificationLog.Failed(1, null, new string('x', 2000), Now);

        Assert.Equal(500, log.Error!.Length);
        Assert.Equal((byte)NotificationStatus.Failed, log.Status);
    }

    [Fact]
    public void FindingEmail_EncodesExternalContent_AndLimitsItems()
    {
        FindingEmailItem[] items =
        [
            new(1, "CVE-2026-0001", "<img src=x onerror=alert(1)>", "evil<pkg>", "1.0.0", "web", FindingPriority.Now, 90m, IsKev: true, IsMaliciousPackage: false)
        ];

        var mail = FindingEmails.NewFindings("a@b.com", "Ekip <script>", items, totalCount: 30, new FakeLinks(), assetGroupId: 7);

        Assert.DoesNotContain("<img", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("evil<pkg>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("29 bulgu daha", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("1 acil", mail.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyDigest_IsOnByDefault_AndFollowsChannelState()
    {
        var channel = Channel(FindingPriority.Now, onlyKev: false);
        Assert.True(channel.ReceivesWeeklyDigest);

        channel.SetWeeklyDigest(false);
        Assert.False(channel.ReceivesWeeklyDigest);

        channel.SetWeeklyDigest(true);
        channel.SetEnabled(false);
        Assert.False(channel.ReceivesWeeklyDigest);
    }

    [Fact]
    public void EscalationEmail_NamesRecentKevAddition()
    {
        FindingEmailItem[] items =
        [
            new(1, "CVE-2026-0002", null, "lodash", "4.17.20", "web", FindingPriority.Now, 90m,
                IsKev: true, IsMaliciousPackage: false, KevAddedAt: new DateOnly(2026, 11, 20))
        ];

        var mail = FindingEmails.Escalated("a@b.com", "Ekip", items, totalCount: 1, new FakeLinks(), assetGroupId: 7, Now);

        Assert.Contains("CISA KEV listesine girdi", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("KEV&#39;e eklendi 20.11.2026", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void EscalationEmail_OldKevEntry_IsReportedAsRiskIncrease()
    {
        FindingEmailItem[] items =
        [
            new(1, "CVE-2021-0003", null, "minimist", "1.2.5", "web", FindingPriority.ThisWeek, 60m,
                IsKev: true, IsMaliciousPackage: false, KevAddedAt: new DateOnly(2022, 1, 10))
        ];

        var mail = FindingEmails.Escalated("a@b.com", "Ekip", items, totalCount: 1, new FakeLinks(), assetGroupId: 7, Now);

        Assert.Contains("önceliği yükseldi", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("risk arttı", mail.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyDigest_EncodesContent_AndShowsCounts()
    {
        var digest = new GroupDigest(
            new Dictionary<FindingPriority, int> { [FindingPriority.Now] = 2, [FindingPriority.Plan] = 5 },
            NewThisPeriod: 3,
            FixedThisPeriod: 4,
            OpenKev: 1,
            [new(1, "CVE-2026-0001", "<b>x</b>", "evil<pkg>", "1.0.0", "web", FindingPriority.Now, 90m, IsKev: true, IsMaliciousPackage: false)]);

        var mail = FindingEmails.WeeklyDigest("a@b.com", "Ekip <script>", digest, new FakeLinks(), assetGroupId: 7);

        Assert.Equal(7, digest.OpenTotal);
        Assert.False(digest.IsEmpty);
        Assert.Contains("7 açık bulgu, 2 acil", mail.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("evil<pkg>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("CISA KEV", mail.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyDigest_WithNothingToReport_IsEmpty()
    {
        var digest = new GroupDigest(new Dictionary<FindingPriority, int>(), 0, 0, 0, []);

        Assert.True(digest.IsEmpty);
    }

    private static NotificationChannel Channel(FindingPriority min, bool onlyKev) =>
        NotificationChannel.CreateEmail(1, "Ekip", "a@b.com", min, onlyKev, Now).Value;

    private sealed class FakeLinks : IAppLinks
    {
        public string BaseUrl => "https://radar.example.com";
        public string FindingDetails(long findingId) => $"{BaseUrl}/Findings/Findings/Details/{findingId}";
        public string AssetGroupDetails(int assetGroupId) => $"{BaseUrl}/Inventory/Groups/Details/{assetGroupId}";
        public string ConfirmEmail(string userId, string encodedToken) => $"{BaseUrl}/Account/ConfirmEmail";
    }
}
