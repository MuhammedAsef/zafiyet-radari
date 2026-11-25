using System.Text.Json;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Ingestion.Osv;

namespace ZafiyetRadari.UnitTests.Ingestion;

public sealed class OsvRecordMapperTests
{
    private const string LodashAdvisory = """
        {
          "id": "GHSA-35jh-r3h4-6jhm",
          "modified": "2024-01-10T12:00:00Z",
          "published": "2021-02-15T00:00:00Z",
          "aliases": ["CVE-2021-23337", "GHSA-35jh-r3h4-6jhm"],
          "summary": "Command Injection in lodash",
          "severity": [{ "type": "CVSS_V3", "score": "CVSS:3.1/AV:N/AC:L/PR:H/UI:N/S:U/C:H/I:H/A:H" }],
          "affected": [
            {
              "package": { "ecosystem": "npm", "name": "lodash", "purl": "pkg:npm/lodash" },
              "ranges": [{ "type": "SEMVER", "events": [{ "introduced": "0" }, { "fixed": "4.17.21" }] }]
            },
            {
              "package": { "ecosystem": "PyPI", "name": "lodash-py" },
              "ranges": [{ "type": "ECOSYSTEM", "events": [{ "introduced": "0" }] }]
            }
          ],
          "references": [
            { "type": "ADVISORY", "url": "https://nvd.nist.gov/vuln/detail/CVE-2021-23337" },
            { "type": "WEB", "url": "javascript:alert(1)" },
            { "type": "WEB", "url": "ftp://example.com/x" }
          ]
        }
        """;

    [Fact]
    public void NpmAdvisory_IsMapped_WithAliasesRangesAndScore()
    {
        Assert.True(OsvRecordMapper.TryMap(Parse(LodashAdvisory), Ecosystems.Npm, includeMalicious: true, out var mapped));

        Assert.Equal("GHSA-35jh-r3h4-6jhm", mapped.Vulnerability.ExternalId);
        Assert.Equal(new[] { "CVE-2021-23337" }, mapped.Aliases); // kendi kimliği alias olarak tekrar yazılmaz
        Assert.Equal(7.2m, mapped.Vulnerability.CvssScore);

        var package = Assert.Single(mapped.Packages); // PyPI paketi bu ekosisteme ait değil
        Assert.Equal("lodash", package.PackageName);
        var range = Assert.Single(package.Ranges);
        Assert.Equal(new OsvRangeRow((byte)VersionRangeType.Semver, "0", "4.17.21", null), range);
    }

    [Fact]
    public void UnsafeReferenceSchemes_AreDropped()
    {
        Assert.True(OsvRecordMapper.TryMap(Parse(LodashAdvisory), Ecosystems.Npm, includeMalicious: true, out var mapped));

        var reference = Assert.Single(mapped.References);
        Assert.StartsWith("https://", reference.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordWithoutTargetEcosystem_IsSkipped()
    {
        Assert.False(OsvRecordMapper.TryMap(Parse(LodashAdvisory), Ecosystems.NuGet, includeMalicious: true, out _));
    }

    [Fact]
    public void MaliciousPackageRecords_CanBeExcluded()
    {
        const string json = """
            { "id": "MAL-2024-1234", "affected": [{ "package": { "ecosystem": "npm", "name": "evil-pkg" }, "versions": ["1.0.0"] }] }
            """;

        Assert.False(OsvRecordMapper.TryMap(Parse(json), Ecosystems.Npm, includeMalicious: false, out _));
        Assert.True(OsvRecordMapper.TryMap(Parse(json), Ecosystems.Npm, includeMalicious: true, out _));
    }

    [Fact]
    public void ExplicitVersions_BecomeExactRanges()
    {
        const string json = """
            { "id": "MAL-2024-1", "affected": [{ "package": { "ecosystem": "npm", "name": "evil-pkg" }, "versions": ["1.0.0", "1.0.1"] }] }
            """;

        Assert.True(OsvRecordMapper.TryMap(Parse(json), Ecosystems.Npm, includeMalicious: true, out var mapped));

        var ranges = Assert.Single(mapped.Packages).Ranges;
        Assert.Equal(2, ranges.Count);
        Assert.All(ranges, r => Assert.Equal(r.Introduced, r.LastAffected));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("GHSA x")]
    [InlineData("")]
    public void UnsafeIdentifiers_AreRejected(string id)
    {
        Assert.False(OsvRecordMapper.IsSafeIdentifier(id));
    }

    [Fact]
    public void Events_AreFlattenedIntoRanges()
    {
        OsvEvent[] events =
        [
            new() { Introduced = "0" },
            new() { Fixed = "1.2.0" },
            new() { Introduced = "2.0.0" },
            new() { LastAffected = "2.3.0" },
            new() { Introduced = "3.0.0" }
        ];

        var ranges = OsvRecordMapper.FlattenEvents(events).ToList();

        (string? Introduced, string? Fixed, string? LastAffected)[] expected =
        [
            ("0", "1.2.0", null),
            ("2.0.0", null, "2.3.0"),
            ("3.0.0", null, null)
        ];

        Assert.Equal(expected, ranges);
    }

    private static OsvRecord Parse(string json) =>
        JsonSerializer.Deserialize<OsvRecord>(json, OsvJson.Options)!;
}
