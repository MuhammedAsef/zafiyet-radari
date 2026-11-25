using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, 0)]
    [InlineData("v1.2.3", 1, 2, 3, 0)]
    [InlineData("=1.2.3", 1, 2, 3, 0)]
    [InlineData(" 1.2.3 ", 1, 2, 3, 0)]
    [InlineData("1.2", 1, 2, 0, 0)]
    [InlineData("1", 1, 0, 0, 0)]
    [InlineData("4.0.1.2", 4, 0, 1, 2)] // NuGet 4 bileşenli sürüm
    [InlineData("1.0.0+build.5", 1, 0, 0, 0)]
    public void TryParse_AcceptsEcosystemFormats(string input, int major, int minor, int patch, int revision)
    {
        Assert.True(SemanticVersion.TryParse(input, out var version));
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(revision, version.Revision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1..3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3-al$pha")]
    [InlineData("-1.2.3")]
    [InlineData("1.2.x")]
    [InlineData("^1.2.3")] // aralık ifadesi, sürüm değil
    [InlineData("99999999999.0.0")] // int taşması
    public void TryParse_RejectsInvalidInput_WithoutThrowing(string? input)
    {
        Assert.False(SemanticVersion.TryParse(input, out _));
    }

    [Fact]
    public void TryParse_RejectsOverlongInput()
    {
        var input = "1.0.0-" + new string('a', SemanticVersion.MaxLength);

        Assert.False(SemanticVersion.TryParse(input, out _));
    }

    /// <summary>semver.org §11'deki resmi sıralama örneği.</summary>
    [Fact]
    public void Precedence_FollowsSemVerSpecification()
    {
        string[] ordered =
        [
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
            "1.0.1",
            "1.1.0",
            "2.0.0"
        ];

        for (var i = 0; i < ordered.Length - 1; i++)
        {
            var lower = SemanticVersion.Parse(ordered[i]);
            var higher = SemanticVersion.Parse(ordered[i + 1]);

            Assert.True(lower < higher, $"{ordered[i]} < {ordered[i + 1]} olmalı");
        }
    }

    [Theory]
    [InlineData("1.0.0+build.1", "1.0.0+build.2")]
    [InlineData("1.0.0", "v1.0.0")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.2.3", "1.2.3.0")]
    public void Equality_IgnoresBuildMetadataAndFormatting(string left, string right)
    {
        var a = SemanticVersion.Parse(left);
        var b = SemanticVersion.Parse(right);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void NumericIdentifiers_CompareNumerically_NotAlphabetically()
    {
        Assert.True(SemanticVersion.Parse("1.0.0-beta.9") < SemanticVersion.Parse("1.0.0-beta.10"));
        Assert.True(SemanticVersion.Parse("1.9.0") < SemanticVersion.Parse("1.10.0"));
    }

    [Fact]
    public void FourthComponent_AffectsOrdering()
    {
        Assert.True(SemanticVersion.Parse("4.0.1") < SemanticVersion.Parse("4.0.1.1"));
    }
}
