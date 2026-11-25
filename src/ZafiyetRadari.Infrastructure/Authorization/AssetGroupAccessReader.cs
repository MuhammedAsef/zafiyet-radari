using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Authorization;

internal sealed class AssetGroupAccessReader(ZafiyetRadariDbContext dbContext) : IAssetGroupAccessReader
{
    public async Task<AssetGroupRole?> GetRoleAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.AssetGroupMembers
            .AsNoTracking()
            .Where(member => member.AssetGroupId == assetGroupId && member.UserId == userId)
            .Select(member => (byte?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return role is null ? null : (AssetGroupRole)role.Value;
    }
}
