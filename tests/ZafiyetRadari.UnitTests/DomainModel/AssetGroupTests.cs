using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class AssetGroupTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_AddsCreatorAsOwner()
    {
        var group = AssetGroup.Create("  Web Sunucuları  ", null, "user-1", Now).Value;

        Assert.Equal("Web Sunucuları", group.Name);
        Assert.Equal(AssetGroupRole.Owner, group.RoleOf("user-1"));
        Assert.Null(group.RoleOf("user-2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Fails(string name)
    {
        var result = AssetGroup.Create(name, null, "user-1", Now);

        Assert.Equal(AssetGroupErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Create_WithoutOwner_Fails()
    {
        var result = AssetGroup.Create("Grup", null, " ", Now);

        Assert.Equal(AssetGroupErrors.OwnerRequired, result.Error);
    }

    [Fact]
    public void Asset_Create_WithUndefinedCriticality_Fails()
    {
        var result = Asset.Create(1, "api", AssetEnvironment.Production, (AssetCriticality)9, false, Now);

        Assert.Equal(AssetErrors.InvalidClassification, result.Error);
    }
}
