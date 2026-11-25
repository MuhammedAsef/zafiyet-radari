using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Abstractions.Authorization;

/// <summary>Kullanıcının bir varlık grubundaki rolünü okur. IDOR kontrolünün veri kaynağı.</summary>
public interface IAssetGroupAccessReader
{
    /// <returns>Üye değilse null.</returns>
    Task<AssetGroupRole?> GetRoleAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default);
}
