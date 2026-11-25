using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Application.Findings;

/// <summary>Bulgu ekran sorguları. Her sorgu kullanıcının grup üyeliğiyle filtrelenir.</summary>
public interface IFindingQueries
{
    Task<PagedResult<FindingListItem>> ListAsync(string userId, FindingFilter filter, CancellationToken cancellationToken);

    /// <summary>Risk faktörleri ve önerilen sürüm hariç (serviste hesaplanır).</summary>
    Task<FindingDetails?> GetAsync(long findingId, string userId, CancellationToken cancellationToken);

    /// <summary>Yetki kontrolü için: bulgunun ait olduğu grup. Bulgu yoksa null.</summary>
    Task<int?> GetAssetGroupIdAsync(long findingId, CancellationToken cancellationToken);
}

/// <summary>Bulgu aggregate'ini değiştirmek için takipli yükleme.</summary>
public interface IFindingStore
{
    Task<Finding?> GetAsync(long findingId, CancellationToken cancellationToken);
}
