using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Matching;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Matching;

internal sealed class FindingRepository(ZafiyetRadariDbContext dbContext) : IFindingRepository
{
    public Task<List<Finding>> GetForAssetAsync(int assetId, CancellationToken cancellationToken) =>
        dbContext.Findings
            .Where(finding => finding.Component.AssetId == assetId)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<Finding> findings) => dbContext.Findings.AddRange(findings);
}
