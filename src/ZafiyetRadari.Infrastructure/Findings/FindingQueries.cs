using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Findings;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Inventory;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Findings;

/// <summary>
/// Bulgu ekranı sorguları (EF projeksiyon, takipsiz). Üyelik filtresi her sorgunun ilk koşuludur:
/// başka grubun bulgusu kimlik tahmin edilerek de okunamaz.
/// </summary>
internal sealed class FindingQueries(ZafiyetRadariDbContext dbContext, IUserDirectory userDirectory) : IFindingQueries
{
    private const string CvePrefix = "CVE-";

    public async Task<PagedResult<FindingListItem>> ListAsync(string userId, FindingFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.Findings
            .AsNoTracking()
            .Where(f => f.Component.Asset.AssetGroup.AssetGroupMembers.Any(m => m.UserId == userId));

        if (filter.AssetGroupId is { } groupId)
        {
            query = query.Where(f => f.Component.Asset.AssetGroupId == groupId);
        }

        if (filter.AssetId is { } assetId)
        {
            query = query.Where(f => f.Component.AssetId == assetId);
        }

        if (filter.Priority is { } priority)
        {
            var value = (byte)priority;
            query = query.Where(f => f.Priority == value);
        }

        if (filter.Status is { } status)
        {
            var value = (byte)status;
            query = query.Where(f => f.Status == value);
        }
        else if (!filter.IncludeClosed)
        {
            const byte open = (byte)FindingStatus.Open, inReview = (byte)FindingStatus.InReview;
            query = query.Where(f => f.Status == open || f.Status == inReview);
        }

        if (filter.Search is { Length: > 0 } search)
        {
            // Parametreli LIKE; joker karakterler kaçışlanır.
            var pattern = "%" + InventoryQueries.EscapeLike(search) + "%";
            query = query.Where(f =>
                EF.Functions.Like(f.Component.Name, pattern, "\\")
                || EF.Functions.Like(f.Vulnerability.ExternalId, pattern, "\\")
                || f.Vulnerability.VulnerabilityAliases.Any(a => EF.Functions.Like(a.Alias, pattern, "\\")));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(f => f.Priority).ThenByDescending(f => f.RiskScore).ThenBy(f => f.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(f => new
            {
                f.Id,
                f.Priority,
                f.RiskScore,
                f.Status,
                f.MatchConfidence,
                f.Vulnerability.ExternalId,
                Cve = f.Vulnerability.VulnerabilityAliases
                    .Where(a => a.Alias.StartsWith(CvePrefix))
                    .OrderBy(a => a.Alias)
                    .Select(a => a.Alias)
                    .FirstOrDefault(),
                f.Vulnerability.Summary,
                f.Vulnerability.IsKev,
                f.Component.Ecosystem,
                f.Component.Name,
                f.Component.Version,
                f.Component.AssetId,
                AssetName = f.Component.Asset.Name,
                GroupName = f.Component.Asset.AssetGroup.Name,
                f.FirstSeenAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new FindingListItem(
                r.Id,
                (FindingPriority)r.Priority,
                r.RiskScore,
                (FindingStatus)r.Status,
                (MatchConfidenceLevel)r.MatchConfidence,
                r.ExternalId,
                r.ExternalId.StartsWith(CvePrefix, StringComparison.Ordinal) ? r.ExternalId : r.Cve,
                r.Summary,
                r.IsKev,
                r.Ecosystem,
                r.Name,
                r.Version,
                r.AssetId,
                r.AssetName,
                r.GroupName,
                r.FirstSeenAt))
            .ToList();

        return new PagedResult<FindingListItem>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<FindingDetails?> GetAsync(long findingId, string userId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Findings
            .AsNoTracking()
            .Where(f => f.Id == findingId && f.Component.Asset.AssetGroup.AssetGroupMembers.Any(m => m.UserId == userId))
            .Select(f => new
            {
                Finding = new { f.Id, f.Status, f.Priority, f.RiskScore, f.MatchConfidence, f.FirstSeenAt, f.LastSeenAt, f.ResolvedAt, f.RiskAcceptedUntil },
                MyRole = f.Component.Asset.AssetGroup.AssetGroupMembers.Where(m => m.UserId == userId).Select(m => m.Role).First(),
                Group = new { f.Component.Asset.AssetGroup.Id, f.Component.Asset.AssetGroup.Name },
                Asset = new { f.Component.Asset.Id, f.Component.Asset.Name, f.Component.Asset.Environment, f.Component.Asset.Criticality, f.Component.Asset.IsInternetFacing },
                Component = new { f.Component.Ecosystem, f.Component.Name, f.Component.Version, f.Component.IsActive },
                Vulnerability = new
                {
                    f.Vulnerability.ExternalId,
                    f.Vulnerability.Summary,
                    f.Vulnerability.Details,
                    f.Vulnerability.PublishedAt,
                    f.Vulnerability.ModifiedAt,
                    f.Vulnerability.CvssScore,
                    f.Vulnerability.CvssVector,
                    f.Vulnerability.EpssScore,
                    f.Vulnerability.EpssPercentile,
                    f.Vulnerability.IsKev,
                    f.Vulnerability.KevAddedAt,
                    f.Vulnerability.KevDueDate,
                    f.Vulnerability.KevRansomware
                },
                Aliases = f.Vulnerability.VulnerabilityAliases.OrderBy(a => a.Alias).Select(a => a.Alias).ToList(),
                FixedVersions = f.Vulnerability.AffectedPackages
                    .Where(p => p.Ecosystem == f.Component.Ecosystem && p.PackageName == f.Component.Name)
                    .SelectMany(p => p.AffectedRanges)
                    .Where(r => r.Fixed != null)
                    .Select(r => r.Fixed!)
                    .ToList(),
                References = f.Vulnerability.VulnerabilityReferences.OrderBy(r => r.Id).Select(r => new { r.Type, r.Url }).ToList(),
                History = f.FindingHistories.OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
                    .Select(h => new { h.FromStatus, h.ToStatus, h.Comment, h.ChangedByUserId, h.ChangedAt })
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var userIds = row.History.Where(h => h.ChangedByUserId is not null).Select(h => h.ChangedByUserId!).Distinct().ToList();
        var names = await userDirectory.GetDisplayNamesAsync(userIds, cancellationToken);

        return new FindingDetails
        {
            Id = row.Finding.Id,
            Status = (FindingStatus)row.Finding.Status,
            Priority = (FindingPriority)row.Finding.Priority,
            RiskScore = row.Finding.RiskScore,
            Confidence = (MatchConfidenceLevel)row.Finding.MatchConfidence,
            FirstSeenAt = row.Finding.FirstSeenAt,
            LastSeenAt = row.Finding.LastSeenAt,
            ResolvedAt = row.Finding.ResolvedAt,
            RiskAcceptedUntil = row.Finding.RiskAcceptedUntil,
            MyRole = (AssetGroupRole)row.MyRole,
            AssetGroupId = row.Group.Id,
            AssetGroupName = row.Group.Name,
            AssetId = row.Asset.Id,
            AssetName = row.Asset.Name,
            AssetEnvironment = (AssetEnvironment)row.Asset.Environment,
            AssetCriticality = (AssetCriticality)row.Asset.Criticality,
            AssetIsInternetFacing = row.Asset.IsInternetFacing,
            Ecosystem = row.Component.Ecosystem,
            PackageName = row.Component.Name,
            Version = row.Component.Version,
            ComponentIsActive = row.Component.IsActive,
            ExternalId = row.Vulnerability.ExternalId,
            Aliases = row.Aliases,
            Summary = row.Vulnerability.Summary,
            Details = row.Vulnerability.Details,
            PublishedAt = row.Vulnerability.PublishedAt,
            ModifiedAt = row.Vulnerability.ModifiedAt,
            CvssScore = row.Vulnerability.CvssScore,
            CvssVector = row.Vulnerability.CvssVector,
            EpssScore = row.Vulnerability.EpssScore,
            EpssPercentile = row.Vulnerability.EpssPercentile,
            IsKev = row.Vulnerability.IsKev,
            KevAddedAt = row.Vulnerability.KevAddedAt,
            KevDueDate = row.Vulnerability.KevDueDate,
            KevRansomware = row.Vulnerability.KevRansomware == true,
            FixedVersions = row.FixedVersions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            References = row.References.Select(r => new FindingReference(r.Type, r.Url)).ToList(),
            History = row.History
                .Select(h => new FindingHistoryItem(
                    h.FromStatus is null ? null : (FindingStatus)h.FromStatus.Value,
                    (FindingStatus)h.ToStatus,
                    h.Comment,
                    h.ChangedByUserId is null ? "Sistem" : names.GetValueOrDefault(h.ChangedByUserId, "(silinmiş kullanıcı)"),
                    h.ChangedAt))
                .ToList()
        };
    }

    public Task<int?> GetAssetGroupIdAsync(long findingId, CancellationToken cancellationToken) =>
        dbContext.Findings
            .AsNoTracking()
            .Where(f => f.Id == findingId)
            .Select(f => (int?)f.Component.Asset.AssetGroupId)
            .FirstOrDefaultAsync(cancellationToken);
}

internal sealed class FindingStore(ZafiyetRadariDbContext dbContext) : IFindingStore
{
    public Task<Finding?> GetAsync(long findingId, CancellationToken cancellationToken) =>
        dbContext.Findings.FirstOrDefaultAsync(f => f.Id == findingId, cancellationToken);
}
