using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Matching;

namespace ZafiyetRadari.Application.Matching;

/// <summary>Varlığın risk bağlamı.</summary>
public sealed record AssetRiskContext(int AssetId, AssetCriticality Criticality, AssetEnvironment Environment, bool IsInternetFacing);

/// <summary>Zafiyetin risk girdileri.</summary>
public sealed record VulnerabilityRiskData(
    int VulnerabilityId,
    string ExternalId,
    decimal? CvssScore,
    decimal? EpssScore,
    bool IsKev,
    bool KevRansomware)
{
    /// <summary>OSV'de kötü amaçlı paket kayıtları MAL- önekiyle gelir.</summary>
    public bool IsMaliciousPackage => ExternalId.StartsWith("MAL-", StringComparison.Ordinal);
}

/// <summary>Eşleştirme için okuma tarafı (Infrastructure: parametreli SQL).</summary>
public interface IMatchingDataSource
{
    Task<IReadOnlyList<int>> GetAllAssetIdsAsync(CancellationToken cancellationToken);

    Task<AssetRiskContext?> GetAssetContextAsync(int assetId, CancellationToken cancellationToken);

    /// <summary>
    /// Varlığın aktif bileşenleriyle aynı ekosistem + adda olan, geri çekilmemiş zafiyetler, aralıkları ve risk verileri.
    /// Tek sorguda gelir: aday ile risk verisi arasında tutarsızlık olmaz.
    /// </summary>
    Task<AssetMatchData> GetMatchDataAsync(int assetId, CancellationToken cancellationToken);
}

public sealed record AssetMatchData(
    IReadOnlyList<MatchCandidate> Candidates,
    IReadOnlyDictionary<int, VulnerabilityRiskData> RiskData);

/// <summary>Yazma tarafı: bulgular (aggregate) yüklenir, IUnitOfWork ile kaydedilir.</summary>
public interface IFindingRepository
{
    /// <summary>Varlığın tüm bileşenlerine (aktif + pasif) ait bulgular, takipli.</summary>
    Task<List<Finding>> GetForAssetAsync(int assetId, CancellationToken cancellationToken);

    void AddRange(IEnumerable<Finding> findings);
}

public sealed record MatchRunSummary(int AssetId, int Matches, int Created, int Updated, int Reopened, int Resolved)
{
    public static MatchRunSummary Empty(int assetId) => new(assetId, 0, 0, 0, 0, 0);
}
