using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Inventory;

internal sealed class InventoryRepository(ZafiyetRadariDbContext dbContext) : IInventoryRepository
{
    public void AddGroup(AssetGroup group) => dbContext.AssetGroups.Add(group);

    public Task<AssetGroup?> GetGroupWithMembersAsync(int assetGroupId, CancellationToken cancellationToken) =>
        dbContext.AssetGroups
            .Include(group => group.AssetGroupMembers)
            .FirstOrDefaultAsync(group => group.Id == assetGroupId, cancellationToken);

    // Tek DELETE; varlıklar, bileşenler, yüklemeler, bulgular ve bildirim kanalları veritabanında cascade ile silinir.
    public Task DeleteGroupAsync(int assetGroupId, CancellationToken cancellationToken) =>
        dbContext.AssetGroups.Where(group => group.Id == assetGroupId).ExecuteDeleteAsync(cancellationToken);

    public void AddAsset(Asset asset) => dbContext.Assets.Add(asset);

    public Task<Asset?> GetAssetAsync(int assetId, CancellationToken cancellationToken) =>
        dbContext.Assets.FirstOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);

    public Task<Asset?> GetAssetWithComponentsAsync(int assetId, CancellationToken cancellationToken) =>
        dbContext.Assets
            .Include(asset => asset.Components)
            .AsSplitQuery()
            .FirstOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);

    public Task<int?> GetAssetGroupIdAsync(int assetId, CancellationToken cancellationToken) =>
        dbContext.Assets
            .AsNoTracking()
            .Where(asset => asset.Id == assetId)
            .Select(asset => (int?)asset.AssetGroupId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AssetNameExistsAsync(int assetGroupId, string name, int? excludingAssetId, CancellationToken cancellationToken) =>
        dbContext.Assets
            .AsNoTracking()
            .AnyAsync(
                asset => asset.AssetGroupId == assetGroupId
                    && asset.Name == name
                    && (excludingAssetId == null || asset.Id != excludingAssetId),
                cancellationToken);

    public Task DeleteAssetAsync(int assetId, CancellationToken cancellationToken) =>
        dbContext.Assets.Where(asset => asset.Id == assetId).ExecuteDeleteAsync(cancellationToken);

    public Task<bool> UploadExistsAsync(int assetId, byte[] sha256, CancellationToken cancellationToken) =>
        dbContext.InventoryUploads
            .AsNoTracking()
            .AnyAsync(upload => upload.AssetId == assetId && upload.Sha256 == sha256, cancellationToken);
}
