using System.Text;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Infrastructure.Inventory.Parsing;

namespace ZafiyetRadari.UnitTests.Inventory;

public sealed class InventoryFileParserTests
{
    private readonly InventoryFileParser _parser = new([new CycloneDxParser(), new NpmLockParser(), new NuGetLockParser()]);

    [Fact]
    public void CycloneDx_ReadsNestedComponents_FromPurl()
    {
        const string json = """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "metadata": { "component": { "name": "my-app", "purl": "pkg:npm/my-app@1.0.0" } },
              "components": [
                { "name": "node", "group": "@types", "version": "20.11.5", "purl": "pkg:npm/%40types/node@20.11.5",
                  "components": [ { "purl": "pkg:npm/undici-types@5.26.5" } ] },
                { "name": "Newtonsoft.Json", "purl": "pkg:nuget/Newtonsoft.Json@13.0.3?repository_url=https://x" },
                { "name": "requests", "purl": "pkg:pypi/requests@2.31.0" },
                { "name": "no-purl", "version": "1.0.0" }
              ]
            }
            """;

        var result = Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadFormat.CycloneDx, result.Value.Format);
        Assert.Equal(
            new[] { "NuGet|Newtonsoft.Json|13.0.3", "npm|@types/node|20.11.5", "npm|undici-types|5.26.5" },
            Keys(result.Value).Order(StringComparer.Ordinal));
        Assert.Equal(2, result.Value.IgnoredCount); // pypi + purl'süz
        Assert.Equal(new[] { Ecosystems.NuGet, Ecosystems.Npm }, result.Value.CoveredEcosystems.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NpmLockV3_SkipsRootLinksWorkspacesAndNonRegistry_AndResolvesAliases()
    {
        const string json = """
            {
              "name": "my-app", "lockfileVersion": 3,
              "packages": {
                "": { "name": "my-app", "version": "1.0.0" },
                "node_modules/lodash": { "version": "4.17.21" },
                "node_modules/express/node_modules/debug": { "version": "2.6.9" },
                "node_modules/@babel/core": { "version": "7.23.0" },
                "node_modules/old-lodash": { "name": "lodash", "version": "3.10.1" },
                "node_modules/my-lib": { "resolved": "packages/my-lib", "link": true },
                "packages/my-lib": { "version": "0.0.1" },
                "node_modules/local": { "version": "file:../local" },
                "node_modules/from-git": { "version": "git+https://github.com/x/y.git#abc" }
              },
              "dependencies": { "lodash": { "version": "4.17.21" } }
            }
            """;

        var result = Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadFormat.NpmLock, result.Value.Format);
        Assert.Equal(
            new[] { "npm|lodash|4.17.21", "npm|debug|2.6.9", "npm|@babel/core|7.23.0", "npm|lodash|3.10.1" },
            Keys(result.Value));
        Assert.Equal(4, result.Value.IgnoredCount);
        Assert.Equal(new[] { Ecosystems.Npm }, result.Value.CoveredEcosystems);
    }

    [Fact]
    public void NpmLockV1_WalksNestedDependencies()
    {
        const string json = """
            {
              "lockfileVersion": 1,
              "dependencies": {
                "express": { "version": "4.17.1", "dependencies": { "debug": { "version": "2.6.9" } } },
                "old": { "version": "npm:lodash@3.10.1" },
                "gh": { "version": "github:user/repo#main" }
              }
            }
            """;

        var result = Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "npm|debug|2.6.9", "npm|express|4.17.1", "npm|lodash|3.10.1" }, Keys(result.Value).Order(StringComparer.Ordinal));
        Assert.Equal(1, result.Value.IgnoredCount);
    }

    [Fact]
    public void NuGetLock_ReadsAllFrameworks_AndSkipsProjects()
    {
        const string json = """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {
                  "Serilog": { "type": "Direct", "requested": "[4.0.0, )", "resolved": "4.0.0", "contentHash": "x" },
                  "System.Text.Json": { "type": "Transitive", "resolved": "8.0.5" },
                  "MyCompany.Core": { "type": "Project" }
                },
                "net8.0": {
                  "Serilog": { "type": "Direct", "resolved": "4.0.0" }
                }
              }
            }
            """;

        var result = Parse(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadFormat.NuGetLock, result.Value.Format);
        Assert.Equal(3, result.Value.Packages.Count); // tekrar eden Serilog Domain'de tekilleşir
        Assert.Equal(1, result.Value.IgnoredCount);
        Assert.Equal(new[] { Ecosystems.NuGet }, result.Value.CoveredEcosystems);
    }

    [Fact]
    public void Utf8Bom_IsAccepted()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("""{ "version": 1, "dependencies": {} }""")).ToArray();

        Assert.True(_parser.Parse(bytes).IsSuccess);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"a\": 1, }")]          // sondaki virgül
    [InlineData("{ /* yorum */ \"a\": 1 }")]
    public void InvalidJson_IsRejected(string content)
    {
        Assert.Equal(UploadErrors.InvalidJson, Parse(content).Error);
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"name\": \"something-else\" }")]
    public void UnknownFormat_IsRejected(string content)
    {
        Assert.Equal(UploadErrors.UnsupportedFormat, Parse(content).Error);
    }

    [Fact]
    public void DeeplyNestedJson_IsRejected()
    {
        var json = new string('[', 200) + new string(']', 200);

        Assert.Equal(UploadErrors.InvalidJson, Parse(json).Error);
    }

    [Fact]
    public void TooManyPackages_IsRejected()
    {
        var builder = new StringBuilder("""{ "lockfileVersion": 3, "packages": {""");
        for (var i = 0; i <= InventoryLimits.MaxComponentsPerUpload; i++)
        {
            builder.Append(i == 0 ? "" : ",").Append("\"node_modules/p").Append(i).Append("\": { \"version\": \"1.0.0\" }");
        }

        builder.Append("} }");

        Assert.Equal("Upload.TooManyComponents", Parse(builder.ToString()).Error.Code);
    }

    [Theory]
    [InlineData("pkg:npm/%40scope/name@1.0.0", "npm", "@scope/name", "1.0.0")]
    [InlineData("pkg:NPM/lodash@4.17.21#sub", "npm", "lodash", "4.17.21")]
    [InlineData("pkg:nuget/Serilog@4.0.0", "NuGet", "Serilog", "4.0.0")]
    public void Purl_IsParsed(string purl, string ecosystem, string name, string version)
    {
        Assert.True(PurlParser.TryParse(purl, out var e, out var n, out var v));
        Assert.Equal((ecosystem, name, version), (e, n, v));
    }

    [Theory]
    [InlineData("pkg:npm/lodash")]          // sürüm yok
    [InlineData("pkg:maven/org.x/y@1.0")]   // desteklenmeyen tip
    [InlineData("npm/lodash@1.0.0")]        // pkg: öneki yok
    [InlineData("pkg:npm/a/b/c@1.0.0")]     // npm'de en fazla bir kapsam
    public void InvalidPurl_IsRejected(string purl)
    {
        Assert.False(PurlParser.TryParse(purl, out _, out _, out _));
    }

    private ZafiyetRadari.Domain.Common.Result<ParsedInventory> Parse(string json) => _parser.Parse(Encoding.UTF8.GetBytes(json));

    private static string[] Keys(ParsedInventory inventory) =>
        inventory.Packages.Select(p => $"{p.Ecosystem}|{p.Name}|{p.Version}").ToArray();
}
