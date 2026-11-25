using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class SemVerComparerTests
{
    private readonly SemVerComparer _comparer = new();

    /// <summary>Gerçek örnek: lodash prototype pollution (CVE-2021-23337), 4.17.21'de düzeltildi.</summary>
    private static readonly VersionRangeSpec Lodash = new(Introduced: "0", Fixed: "4.17.21", LastAffected: null);

    [Theory]
    [InlineData("4.17.20", RangeEvaluation.Affected)]
    [InlineData("1.0.0", RangeEvaluation.Affected)]
    [InlineData("4.17.21-beta.1", RangeEvaluation.Affected)] // düzeltmenin pre-release'i, düzeltmeden önce gelir
    [InlineData("4.17.21", RangeEvaluation.NotAffected)]     // Fixed hariç
    [InlineData("4.18.0", RangeEvaluation.NotAffected)]
    public void FixedBound_IsExclusive(string version, RangeEvaluation expected)
    {
        Assert.Equal(expected, _comparer.Evaluate(version, Lodash));
    }

    [Theory]
    [InlineData("2.0.0", RangeEvaluation.NotAffected)] // introduced öncesi
    [InlineData("2.5.0", RangeEvaluation.Affected)]    // introduced dahil
    [InlineData("2.6.3", RangeEvaluation.Affected)]
    [InlineData("2.7.0", RangeEvaluation.Affected)]    // last_affected dahil
    [InlineData("2.7.1", RangeEvaluation.NotAffected)]
    public void IntroducedAndLastAffected_AreInclusive(string version, RangeEvaluation expected)
    {
        var range = new VersionRangeSpec(Introduced: "2.5.0", Fixed: null, LastAffected: "2.7.0");

        Assert.Equal(expected, _comparer.Evaluate(version, range));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("^4.17.0")]
    [InlineData("")]
    public void UnparseableVersion_IsUnknown_NotSilentlySafe(string version)
    {
        Assert.Equal(RangeEvaluation.Unknown, _comparer.Evaluate(version, Lodash));
    }

    [Fact]
    public void UnparseableBound_IsUnknown()
    {
        var broken = new VersionRangeSpec(Introduced: "0", Fixed: "4.x", LastAffected: null);

        Assert.Equal(RangeEvaluation.Unknown, _comparer.Evaluate("4.17.20", broken));
    }

    [Fact]
    public void SupportsOnlySemVerEcosystems()
    {
        Assert.True(_comparer.Supports(Ecosystems.Npm));
        Assert.True(_comparer.Supports(Ecosystems.NuGet));
        Assert.False(_comparer.Supports("Maven"));
        Assert.False(_comparer.Supports("NPM")); // OSV ekosistem adları büyük/küçük harfe duyarlı
    }

    [Fact]
    public void EvaluateAny_AffectedIfAnyRangeMatches()
    {
        VersionRangeSpec[] ranges =
        [
            new("1.0.0", "1.2.0", null),
            new("2.0.0", "2.3.1", null)
        ];

        Assert.Equal(RangeEvaluation.Affected, VersionRangeEvaluator.EvaluateAny(_comparer, "2.1.0", ranges));
        Assert.Equal(RangeEvaluation.NotAffected, VersionRangeEvaluator.EvaluateAny(_comparer, "1.5.0", ranges));
    }

    [Fact]
    public void EvaluateAny_WithNoRanges_IsUnknown()
    {
        Assert.Equal(RangeEvaluation.Unknown, VersionRangeEvaluator.EvaluateAny(_comparer, "1.0.0", []));
    }

    [Fact]
    public void EvaluateAny_AffectedWinsOverUnknown()
    {
        VersionRangeSpec[] ranges =
        [
            new("0", "bozuk", null),
            new("0", "2.0.0", null)
        ];

        Assert.Equal(RangeEvaluation.Affected, VersionRangeEvaluator.EvaluateAny(_comparer, "1.0.0", ranges));
    }
}
