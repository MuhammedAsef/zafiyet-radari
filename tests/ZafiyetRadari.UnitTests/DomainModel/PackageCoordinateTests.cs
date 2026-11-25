using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class PackageCoordinateTests
{
    [Theory]
    [InlineData("npm", "lodash", "4.17.21")]
    [InlineData("npm", "@types/node", "20.11.5")]
    [InlineData("npm", "JSONStream", "1.3.5")]      // eski paketlerde büyük harf olabilir
    [InlineData("NuGet", "Newtonsoft.Json", "13.0.3")]
    [InlineData("nuget", "Serilog", "4.0.0-dev-02108")] // ekosistem adı kanonik hale gelir
    [InlineData("NuGet", "System.Text.Json", "8.0.0.1")]
    public void ValidCoordinates_AreAccepted(string ecosystem, string name, string version)
    {
        var result = PackageCoordinate.Create(ecosystem, name, version);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Ecosystem, Ecosystems.All);
    }

    [Theory]
    [InlineData("npm", "../../etc/passwd")]
    [InlineData("npm", "a b")]
    [InlineData("npm", "@scope")]
    [InlineData("npm", "@/pkg")]
    [InlineData("npm", ".hidden")]
    [InlineData("npm", "<script>")]
    [InlineData("NuGet", "Bad Name")]
    [InlineData("NuGet", "Name/Other")]
    [InlineData("NuGet", ".Leading")]
    [InlineData("npm", "")]
    public void InvalidNames_AreRejected(string ecosystem, string name)
    {
        Assert.Equal(ComponentErrors.InvalidName, PackageCoordinate.Create(ecosystem, name, "1.0.0").Error);
    }

    [Fact]
    public void TooLongNpmName_IsRejected()
    {
        Assert.Equal(ComponentErrors.InvalidName, PackageCoordinate.Create("npm", new string('a', 215), "1.0.0").Error);
    }

    [Theory]
    [InlineData("^4.17.0")]
    [InlineData("~1.2.3")]
    [InlineData("latest")]
    [InlineData(">=1.0.0")]
    [InlineData("")]
    public void VersionRanges_AreRejected(string version)
    {
        Assert.Equal(ComponentErrors.InvalidVersion, PackageCoordinate.Create("npm", "lodash", version).Error);
    }

    [Fact]
    public void UnsupportedEcosystem_IsRejected()
    {
        Assert.Equal(ComponentErrors.UnsupportedEcosystem, PackageCoordinate.Create("PyPI", "requests", "2.31.0").Error);
    }

    [Fact]
    public void Purl_IsGenerated_WithEncodedScope()
    {
        Assert.Equal("pkg:npm/%40types/node@20.11.5", PackageCoordinate.Create("npm", "@types/node", "20.11.5").Value.Purl);
        Assert.Equal("pkg:nuget/Newtonsoft.Json@13.0.3", PackageCoordinate.Create("NuGet", "Newtonsoft.Json", "13.0.3").Value.Purl);
    }

    [Fact]
    public void Key_IsCaseInsensitive()
    {
        var a = PackageCoordinate.Create("NuGet", "Newtonsoft.Json", "13.0.3").Value;
        var b = PackageCoordinate.Create("nuget", "newtonsoft.json", "13.0.3").Value;

        Assert.Equal(a.Key, b.Key);
    }
}
