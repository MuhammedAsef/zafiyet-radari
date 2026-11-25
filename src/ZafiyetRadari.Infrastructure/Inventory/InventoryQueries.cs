using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Inventory;

/// <summary>
/// Ekran sorguları: sadece gereken kolonlar projekte edilir, takip yok.
/// Her sorgu kullanıcının üyeliğiyle filtrelenir; servis kontrolü atlansa bile başka grubun verisi dönmez.
/// </summary>
internal sealed class InventoryQueries(ZafiyetRadariDbContext dbContext, IUserDirectory userDirectory) : IInventoryQueries
{
    public async Task<IReadOnlyList<AssetGroupListItem>> ListGroupsAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.AssetGroupMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.AssetGroup.Name)
            .Select(member => new
            {
                member.AssetGroup.Id,
                member.AssetGroup.Name,
                member.AssetGroup.Description,
                member.Role,
                AssetCount = member.AssetGroup.Assets.Count,
                MemberCount = member.AssetGroup.AssetGroupMembers.Count
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new AssetGroupListItem(row.Id, row.Name, row.Description, (AssetGroupRole)row.Role, row.AssetCount, row.MemberCount))
            .ToList();
    }

    public async Task<AssetGroupDetails?> GetGroupAsync(int assetGroupId, string userId, CancellationToken cancellationToken)
    {
        var group = await dbContext.AssetGroups
            .AsNoTracking()
            .Where(g => g.Id == assetGroupId && g.AssetGroupMembers.Any(m => m.UserId == userId))
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                MyRole = g.AssetGroupMembers.Where(m => m.UserId == userId).Select(m => m.Role).First(),
                Assets = g.Assets
                    .OrderBy(a => a.Name)
                    .Select(a => new
                    {
                        a.Id,
                        a.Name,
                        a.Environment,
                        a.Criticality,
                        a.IsInternetFacing,
                        ActiveComponentCount = a.Components.Count(c => c.IsActive)
                    })
                    .ToList(),
                Members = g.AssetGroupMembers
                    .OrderBy(m => m.Role).ThenBy(m => m.AddedAt)
                    .Select(m => new { m.UserId, m.Role, m.AddedAt })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
        {
            return null;
        }

        var names = await userDirectory.GetDisplayNamesAsync(group.Members.Select(m => m.UserId).ToList(), cancellationToken);

        return new AssetGroupDetails(
            group.Id,
            group.Name,
            group.Description,
            (AssetGroupRole)group.MyRole,
            group.Assets
                .Select(a => new AssetListItem(
                    a.Id, a.Name, (AssetEnvironment)a.Environment, (AssetCriticality)a.Criticality, a.IsInternetFacing, a.ActiveComponentCount))
                .ToList(),
            group.Members
                .Select(m => new GroupMemberItem(
                    m.UserId, names.GetValueOrDefault(m.UserId, "(silinmiş kullanıcı)"), (AssetGroupRole)m.Role, m.AddedAt))
                .ToList());
    }

    public async Task<AssetDetails?> GetAssetAsync(int assetId, string userId, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets
            .AsNoTracking()
            .Where(a => a.Id == assetId && a.AssetGroup.AssetGroupMembers.Any(m => m.UserId == userId))
            .Select(a => new
            {
                a.Id,
                a.AssetGroupId,
                GroupName = a.AssetGroup.Name,
                MyRole = a.AssetGroup.AssetGroupMembers.Where(m => m.UserId == userId).Select(m => m.Role).First(),
                a.Name,
                a.Environment,
                a.Criticality,
                a.IsInternetFacing,
                a.CreatedAt,
                a.UpdatedAt,
                ActiveComponentCount = a.Components.Count(c => c.IsActive),
                Uploads = a.InventoryUploads
                    .OrderByDescending(u => u.UploadedAt)
                    .Take(InventoryLimits.RecentUploadCount)
                    .Select(u => new { u.Id, u.OriginalFileName, u.Format, u.Sha256, u.SizeBytes, u.ComponentCount, u.UploadedAt })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (asset is null)
        {
            return null;
        }

        return new AssetDetails(
            asset.Id,
            asset.AssetGroupId,
            asset.GroupName,
            (AssetGroupRole)asset.MyRole,
            asset.Name,
            (AssetEnvironment)asset.Environment,
            (AssetCriticality)asset.Criticality,
            asset.IsInternetFacing,
            asset.CreatedAt,
            asset.UpdatedAt,
            asset.ActiveComponentCount,
            asset.Uploads
                .Select(u => new UploadListItem(
                    u.Id, u.OriginalFileName, (UploadFormat)u.Format, Convert.ToHexString(u.Sha256), u.SizeBytes, u.ComponentCount, u.UploadedAt))
                .ToList());
    }

    public async Task<PagedResult<ComponentListItem>> ListComponentsAsync(
        int assetId, string userId, ComponentFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.Components
            .AsNoTracking()
            .Where(c => c.AssetId == assetId && c.Asset.AssetGroup.AssetGroupMembers.Any(m => m.UserId == userId));

        if (!filter.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        if (filter.Search is { Length: > 0 } search)
        {
            // EF bunu parametreli LIKE'a çevirir. Joker karakterler (% _ [) kaçışlanır: kullanıcı "%" yazarak tüm tabloyu taratamaz.
            var pattern = "%" + EscapeLike(search) + "%";
            query = query.Where(c => EF.Functions.Like(c.Name, pattern, "\\"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Ecosystem).ThenBy(c => c.Name).ThenBy(c => c.Version)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(c => new { c.Id, c.Ecosystem, c.Name, c.Version, c.Purl, c.Source, c.IsActive, c.CreatedAt })
            .ToListAsync(cancellationToken);

        return new PagedResult<ComponentListItem>(
            items
                .Select(c => new ComponentListItem(c.Id, c.Ecosystem, c.Name, c.Version, c.Purl, (ComponentSource)c.Source, c.IsActive, c.CreatedAt))
                .ToList(),
            filter.Page,
            filter.PageSize,
            total);
    }

    internal static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
