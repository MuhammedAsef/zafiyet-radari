using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.UnitTests.DomainModel;

public sealed class AssetInventoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstUpload_AddsAllComponents_AndIgnoresDuplicatesInFile()
    {
        var asset = NewAsset();

        var summary = asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.21"), Npm("express", "4.18.2"), Npm("lodash", "4.17.21")], [Ecosystems.Npm], Now);

        Assert.Equal(2, summary.Added);
        Assert.Equal(2, asset.Components.Count);
        Assert.All(asset.Components, c => Assert.Equal(ComponentSource.Upload, c.SourceKind));
        Assert.Equal(2, Assert.Single(asset.InventoryUploads).ComponentCount);
    }

    [Fact]
    public void SecondUpload_DeactivatesMissing_AndReactivatesReturning()
    {
        var asset = NewAsset();
        asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.20"), Npm("express", "4.18.2")], [Ecosystems.Npm], Now);

        // lodash güncellendi: eski sürüm pasife, yeni sürüm eklenir
        var second = asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.21"), Npm("express", "4.18.2")], [Ecosystems.Npm], Now);
        Assert.Equal(new InventorySyncSummary(Added: 1, Reactivated: 0, Unchanged: 1, Deactivated: 1), second);

        // geri dönüş: eski sürüm yeniden etkinleşir (yeni kayıt açılmaz, bulgu geçmişi korunur)
        var third = asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.20"), Npm("express", "4.18.2")], [Ecosystems.Npm], Now);
        Assert.Equal(new InventorySyncSummary(Added: 0, Reactivated: 1, Unchanged: 1, Deactivated: 1), third);
        Assert.Equal(3, asset.Components.Count);
    }

    [Fact]
    public void Upload_OnlyAffectsCoveredEcosystems()
    {
        var asset = NewAsset();
        asset.ApplyUpload(NewUpload(), [NuGet("Newtonsoft.Json", "13.0.3")], [Ecosystems.NuGet], Now);

        // npm lock dosyası NuGet bileşenlerini pasife almamalı
        var summary = asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.21")], [Ecosystems.Npm], Now);

        Assert.Equal(0, summary.Deactivated);
        Assert.All(asset.Components, c => Assert.True(c.IsActive));
    }

    [Fact]
    public void Upload_DoesNotDeactivateManualComponents()
    {
        var asset = NewAsset();
        Assert.True(asset.AddManualComponent(Npm("left-pad", "1.3.0"), Now).IsSuccess);

        asset.ApplyUpload(NewUpload(), [Npm("lodash", "4.17.21")], [Ecosystems.Npm], Now);

        var manual = asset.Components.Single(c => c.Name == "left-pad");
        Assert.True(manual.IsActive);
        Assert.Equal(ComponentSource.Manual, manual.SourceKind);
    }

    [Fact]
    public void AddManualComponent_RejectsActiveDuplicate_ButReactivatesInactive()
    {
        var asset = NewAsset();
        var component = asset.AddManualComponent(Npm("lodash", "4.17.21"), Now).Value;
        component.Id = 42;

        Assert.Equal(ComponentErrors.AlreadyExists, asset.AddManualComponent(Npm("LODASH", "4.17.21"), Now).Error);

        Assert.True(asset.DeactivateComponent(42, Now).IsSuccess);
        Assert.False(component.IsActive);

        Assert.True(asset.AddManualComponent(Npm("lodash", "4.17.21"), Now).IsSuccess);
        Assert.True(component.IsActive);
        Assert.Single(asset.Components);
    }

    [Fact]
    public void DeactivateComponent_UnknownId_Fails()
    {
        Assert.Equal(ComponentErrors.NotFound, NewAsset().DeactivateComponent(999, Now).Error);
    }

    [Fact]
    public void LastOwner_CannotBeRemovedOrDemoted()
    {
        var group = AssetGroup.Create("Grup", null, "owner-1", Now).Value;

        Assert.Equal(AssetGroupErrors.LastOwner, group.RemoveMember("owner-1").Error);
        Assert.Equal(AssetGroupErrors.LastOwner, group.ChangeMemberRole("owner-1", AssetGroupRole.Viewer).Error);

        Assert.True(group.AddMember("owner-2", AssetGroupRole.Owner, Now).IsSuccess);
        Assert.True(group.ChangeMemberRole("owner-1", AssetGroupRole.Viewer).IsSuccess);
        Assert.Equal(AssetGroupErrors.LastOwner, group.RemoveMember("owner-2").Error);
    }

    [Fact]
    public void AddMember_Twice_Fails()
    {
        var group = AssetGroup.Create("Grup", null, "owner-1", Now).Value;

        Assert.True(group.AddMember("user-2", AssetGroupRole.Viewer, Now).IsSuccess);
        Assert.Equal(AssetGroupErrors.MemberAlreadyExists, group.AddMember("user-2", AssetGroupRole.Editor, Now).Error);
    }

    [Theory]
    [InlineData(@"C:\fakepath\package-lock.json", "package-lock.json")]
    [InlineData("../../etc/passwd.json", "passwd.json")]
    [InlineData("", "dosya.json")]
    public void UploadFileName_IsSanitized(string input, string expected)
    {
        Assert.Equal(expected, InventoryUpload.SanitizeFileName(input));
    }

    [Fact]
    public void UploadFileName_ControlCharacters_AreRemoved()
    {
        Assert.Equal("ab.json", InventoryUpload.SanitizeFileName("a\u0000b<>\r\n.json"));
    }

    private static Asset NewAsset() =>
        Asset.Create(1, "web-api", AssetEnvironment.Production, AssetCriticality.High, true, Now).Value;

    private static InventoryUpload NewUpload() =>
        InventoryUpload.Create("package-lock.json", UploadFormat.NpmLock, new byte[32], 100, "user-1", Now);

    private static PackageCoordinate Npm(string name, string version) => PackageCoordinate.Create("npm", name, version).Value;

    private static PackageCoordinate NuGet(string name, string version) => PackageCoordinate.Create("NuGet", name, version).Value;
}
