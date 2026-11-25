using ZafiyetRadari.Domain.Matching;

namespace ZafiyetRadari.UnitTests.Matching;

public sealed class FixVersionAdvisorTests
{
    [Fact]
    public void RecommendsSmallestFixAboveCurrent()
    {
        Assert.Equal("4.17.21", FixVersionAdvisor.Recommend("4.17.20", ["4.17.21", "5.0.0", "3.10.0"]));
    }

    [Fact]
    public void PrefersStableOverPreRelease()
    {
        Assert.Equal("16.2.5", FixVersionAdvisor.Recommend("16.2.4", ["16.2.5-canary.1", "16.2.5", "17.0.0"]));
    }

    [Fact]
    public void UsesPreRelease_WhenNoStableFixExists()
    {
        Assert.Equal("2.0.0-rc.1", FixVersionAdvisor.Recommend("1.9.0", ["2.0.0-rc.1"]));
    }

    [Theory]
    [InlineData("4.17.21")]   // zaten düzeltilmiş
    [InlineData("not-a-version")]
    public void ReturnsNull_WhenNothingToRecommend(string current)
    {
        Assert.Null(FixVersionAdvisor.Recommend(current, ["4.17.21", null, "garbage"]));
    }
}
