using ZafiyetRadari.Domain.Vulnerabilities;

namespace ZafiyetRadari.UnitTests.DomainModel;

/// <summary>Beklenen değerler FIRST CVSS 3.1 hesaplayıcısından (first.org/cvss/calculator/3.1).</summary>
public sealed class CvssV3CalculatorTests
{
    [Theory]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H", 10.0)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:N/A:N", 6.5)]
    [InlineData("CVSS:3.1/AV:L/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H", 7.8)]
    [InlineData("CVSS:3.1/AV:N/AC:H/PR:N/UI:R/S:U/C:L/I:N/A:N", 3.1)]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:C/C:L/I:L/A:N", 6.1)]
    [InlineData("CVSS:3.0/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H", 9.8)]
    public void KnownVectors_ProduceOfficialScores(string vector, double expected)
    {
        Assert.True(CvssV3Calculator.TryCalculateBaseScore(vector, out var score));
        Assert.Equal((decimal)expected, score);
    }

    [Fact]
    public void NoImpact_IsZero()
    {
        Assert.True(CvssV3Calculator.TryCalculateBaseScore("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:N", out var score));
        Assert.Equal(0m, score);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CVSS:2.0/AV:N/AC:L/Au:N/C:P/I:P/A:P")]
    [InlineData("CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H")]          // A eksik
    [InlineData("CVSS:3.1/AV:X/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")]      // geçersiz değer
    [InlineData("CVSS:3.1/AV:N/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H")] // tekrar eden metrik
    public void InvalidVectors_AreRejected(string? vector)
    {
        Assert.False(CvssV3Calculator.TryCalculateBaseScore(vector, out _));
    }
}
