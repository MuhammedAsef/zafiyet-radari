using System.Data;
using Microsoft.Data.SqlClient;
using ZafiyetRadari.Application.Matching;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Matching;
using ZafiyetRadari.Domain.Versioning;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Matching;

/// <summary>
/// Eşleştirme okumaları: sabit SQL + SqlParameter. Aday sorgusu (Ecosystem, PackageName) indeksini kullanır;
/// 234 bin zafiyet olsa da bir varlık için sadece bileşenleriyle aynı adlı paketler okunur.
/// </summary>
internal sealed class MatchingDataSource(ISqlConnectionFactory connectionFactory) : IMatchingDataSource
{
    private const string AllAssetsSql = "SELECT Id FROM inventory.Assets ORDER BY Id;";

    private const string AssetContextSql = """
        SELECT Criticality, Environment, IsInternetFacing
        FROM inventory.Assets
        WHERE Id = @AssetId;
        """;

    // Paket adı karşılaştırması veritabanı collation'ı ile (büyük/küçük harf duyarsız) yapılır: NuGet kimlikleri duyarsızdır,
    // npm'de eski büyük harfli paketler de yakalanır. Geri çekilmiş (withdrawn) kayıtlar eşleşmez.
    private const string CandidatesSql = """
        SELECT c.Id, c.Ecosystem, c.Version,
               v.Id, v.ExternalId, v.CvssScore, v.EpssScore, v.IsKev, v.KevRansomware,
               r.Introduced, r.Fixed, r.LastAffected
        FROM inventory.Components AS c
        JOIN vuln.AffectedPackages AS ap ON ap.Ecosystem = c.Ecosystem AND ap.PackageName = c.Name
        JOIN vuln.Vulnerabilities AS v ON v.Id = ap.VulnerabilityId
        LEFT JOIN vuln.AffectedRanges AS r ON r.AffectedPackageId = ap.Id
        WHERE c.AssetId = @AssetId AND c.IsActive = 1 AND v.WithdrawnAt IS NULL;
        """;

    public async Task<IReadOnlyList<int>> GetAllAssetIdsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AllAssetsSql;

        var ids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }

    public async Task<AssetRiskContext?> GetAssetContextAsync(int assetId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AssetContextSql;
        command.WithParameter("@AssetId", SqlDbType.Int, assetId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AssetRiskContext(
            assetId,
            (AssetCriticality)reader.GetByte(0),
            (AssetEnvironment)reader.GetByte(1),
            reader.GetBoolean(2));
    }

    public async Task<AssetMatchData> GetMatchDataAsync(int assetId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CandidatesSql;
        command.CommandTimeout = 120;
        command.WithParameter("@AssetId", SqlDbType.Int, assetId);

        var candidates = new List<MatchCandidate>();
        var riskData = new Dictionary<int, VulnerabilityRiskData>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var vulnerabilityId = reader.GetInt32(3);

            if (!riskData.ContainsKey(vulnerabilityId))
            {
                riskData[vulnerabilityId] = new VulnerabilityRiskData(
                    vulnerabilityId,
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    reader.GetBoolean(7),
                    !reader.IsDBNull(8) && reader.GetBoolean(8));
            }

            // LEFT JOIN: aralığı olmayan paket kaydı boş liste olarak gelir (Domain bunu "belirsiz" sayar).
            var hasRange = !(reader.IsDBNull(9) && reader.IsDBNull(10) && reader.IsDBNull(11));
            var ranges = hasRange
                ? new[] { new VersionRangeSpec(GetNullableString(reader, 9), GetNullableString(reader, 10), GetNullableString(reader, 11)) }
                : Array.Empty<VersionRangeSpec>();

            candidates.Add(new MatchCandidate(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), vulnerabilityId, ranges));
        }

        return new AssetMatchData(candidates, riskData);
    }

    private static string? GetNullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
