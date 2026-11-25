using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Inventory;

/// <summary>Yazma tarafı: aggregate'ler (AssetGroup, Asset) yüklenir, değiştirilir, IUnitOfWork ile kaydedilir.</summary>
public interface IInventoryRepository
{
    void AddGroup(AssetGroup group);

    Task<AssetGroup?> GetGroupWithMembersAsync(int assetGroupId, CancellationToken cancellationToken);

    Task DeleteGroupAsync(int assetGroupId, CancellationToken cancellationToken);

    void AddAsset(Asset asset);

    Task<Asset?> GetAssetAsync(int assetId, CancellationToken cancellationToken);

    /// <summary>Varlığı tüm bileşenleriyle (aktif + pasif) yükler.</summary>
    Task<Asset?> GetAssetWithComponentsAsync(int assetId, CancellationToken cancellationToken);

    /// <summary>Yetki kontrolü için: varlığın grubu. Varlık yoksa null.</summary>
    Task<int?> GetAssetGroupIdAsync(int assetId, CancellationToken cancellationToken);

    Task<bool> AssetNameExistsAsync(int assetGroupId, string name, int? excludingAssetId, CancellationToken cancellationToken);

    /// <summary>Varlığı siler; bileşenler, yüklemeler ve bulgular veritabanında cascade ile silinir.</summary>
    Task DeleteAssetAsync(int assetId, CancellationToken cancellationToken);

    Task<bool> UploadExistsAsync(int assetId, byte[] sha256, CancellationToken cancellationToken);
}

/// <summary>
/// Okuma tarafı: ekranlar için projeksiyonlar. Her sorgu kullanıcının üyeliğiyle de filtrelenir
/// (servisteki yetki kontrolüne ek, derinlemesine savunma).
/// </summary>
public interface IInventoryQueries
{
    Task<IReadOnlyList<AssetGroupListItem>> ListGroupsAsync(string userId, CancellationToken cancellationToken);

    Task<AssetGroupDetails?> GetGroupAsync(int assetGroupId, string userId, CancellationToken cancellationToken);

    Task<AssetDetails?> GetAssetAsync(int assetId, string userId, CancellationToken cancellationToken);

    Task<PagedResult<ComponentListItem>> ListComponentsAsync(
        int assetId, string userId, ComponentFilter filter, CancellationToken cancellationToken);
}

/// <summary>Ham paket girdisi; henüz doğrulanmamış (Application'da PackageCoordinate'e çevrilir).</summary>
public sealed record RawPackage(string Ecosystem, string Name, string Version);

/// <param name="CoveredEcosystems">
/// Dosyanın "tam listesini verdiği" ekosistemler. Bu ekosistemlerde dosyada olmayan (yüklemeden gelmiş) bileşenler pasife alınır.
/// </param>
/// <param name="IgnoredCount">Desteklenmeyen ekosistem, yerel/workspace bağlantısı gibi sebeplerle atlanan girdi sayısı.</param>
public sealed record ParsedInventory(
    UploadFormat Format,
    IReadOnlyList<RawPackage> Packages,
    IReadOnlyCollection<string> CoveredEcosystems,
    int IgnoredCount);

/// <summary>İçeriğe bakarak biçimi tanır (dosya adına güvenilmez) ve paketleri çıkarır.</summary>
public interface IInventoryFileParser
{
    Result<ParsedInventory> Parse(ReadOnlyMemory<byte> content);
}
