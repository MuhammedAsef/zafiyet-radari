using System.Data;
using Microsoft.Data.SqlClient;
using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Dashboard;

/// <summary>
/// Dashboard: tek komutta yedi sonuç kümesi (tek gidiş-dönüş). Sabit SQL + SqlParameter.
/// Kapsam önce @Groups tablo değişkenine alınır (kullanıcının üye olduğu gruplar, isteğe bağlı tek grup);
/// tüm sayımlar bu tabloyla JOIN edilir, başka grubun verisi hiçbir sayıya karışmaz.
/// Durum kodları: 1 Açık, 2 İncelemede, 3 Düzeltildi, 4 Risk kabul, 5 Yanlış alarm. Öncelik: 1 Hemen ... 4 İzle.
/// </summary>
internal sealed class DashboardQueries(ISqlConnectionFactory connectionFactory) : IDashboardQueries
{
    private const string Sql = """
        SET NOCOUNT ON;

        DECLARE @Groups TABLE (Id int NOT NULL PRIMARY KEY);
        INSERT INTO @Groups (Id)
        SELECT AssetGroupId FROM inventory.AssetGroupMembers
        WHERE UserId = @UserId AND (@AssetGroupId IS NULL OR AssetGroupId = @AssetGroupId);

        -- 1) Özet sayılar
        SELECT
            ISNULL(SUM(CASE WHEN f.Status IN (1, 2) AND f.Priority = 1 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status IN (1, 2) AND f.Priority = 2 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status IN (1, 2) AND f.Priority = 3 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status IN (1, 2) AND f.Priority = 4 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status = 2 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status IN (1, 2) AND v.IsKev = 1 THEN 1 ELSE 0 END), 0),
            ISNULL(SUM(CASE WHEN f.Status = 4 THEN 1 ELSE 0 END), 0),
            AVG(CASE WHEN f.Status = 3 AND f.ResolvedAt >= @MttrSince
                     THEN CAST(DATEDIFF(minute, f.FirstSeenAt, f.ResolvedAt) AS float) / 1440.0 END)
        FROM finding.Findings AS f
        JOIN inventory.Components AS c ON c.Id = f.ComponentId
        JOIN inventory.Assets AS a ON a.Id = c.AssetId
        JOIN @Groups AS g ON g.Id = a.AssetGroupId
        JOIN vuln.Vulnerabilities AS v ON v.Id = f.VulnerabilityId;

        -- 2) Envanter
        SELECT
            (SELECT COUNT(*) FROM inventory.Assets AS a JOIN @Groups AS g ON g.Id = a.AssetGroupId),
            (SELECT COUNT(*) FROM inventory.Components AS c
                JOIN inventory.Assets AS a ON a.Id = c.AssetId
                JOIN @Groups AS g ON g.Id = a.AssetGroupId
             WHERE c.IsActive = 1);

        -- 3) En riskli varlıklar
        SELECT TOP (@TopAssets) a.Id, a.Name, COUNT(*), SUM(CASE WHEN f.Priority = 1 THEN 1 ELSE 0 END), MAX(f.RiskScore)
        FROM finding.Findings AS f
        JOIN inventory.Components AS c ON c.Id = f.ComponentId
        JOIN inventory.Assets AS a ON a.Id = c.AssetId
        JOIN @Groups AS g ON g.Id = a.AssetGroupId
        WHERE f.Status IN (1, 2)
        GROUP BY a.Id, a.Name
        ORDER BY SUM(CASE WHEN f.Priority = 1 THEN 1 ELSE 0 END) DESC, MAX(f.RiskScore) DESC, COUNT(*) DESC;

        -- 4) En çok bulgu üreten paketler
        SELECT TOP (@TopPackages) c.Ecosystem, c.Name, COUNT(*), MAX(f.RiskScore)
        FROM finding.Findings AS f
        JOIN inventory.Components AS c ON c.Id = f.ComponentId
        JOIN inventory.Assets AS a ON a.Id = c.AssetId
        JOIN @Groups AS g ON g.Id = a.AssetGroupId
        WHERE f.Status IN (1, 2)
        GROUP BY c.Ecosystem, c.Name
        ORDER BY COUNT(*) DESC, MAX(f.RiskScore) DESC;

        -- 5) Günlük açılan (UTC günü)
        SELECT CAST(f.FirstSeenAt AS date), COUNT(*)
        FROM finding.Findings AS f
        JOIN inventory.Components AS c ON c.Id = f.ComponentId
        JOIN inventory.Assets AS a ON a.Id = c.AssetId
        JOIN @Groups AS g ON g.Id = a.AssetGroupId
        WHERE f.FirstSeenAt >= @TrendSince
        GROUP BY CAST(f.FirstSeenAt AS date);

        -- 6) Günlük kapanan (düzeltildi veya yanlış alarm)
        SELECT CAST(f.ResolvedAt AS date), COUNT(*)
        FROM finding.Findings AS f
        JOIN inventory.Components AS c ON c.Id = f.ComponentId
        JOIN inventory.Assets AS a ON a.Id = c.AssetId
        JOIN @Groups AS g ON g.Id = a.AssetGroupId
        WHERE f.ResolvedAt >= @TrendSince AND f.Status IN (3, 5)
        GROUP BY CAST(f.ResolvedAt AS date);

        -- 7) Veri kaynakları (gruptan bağımsız, sistem geneli)
        SELECT Source, LastAttemptAt, LastSuccessAt, ItemsProcessed, LastError
        FROM ops.FeedSyncState
        ORDER BY Source;
        """;

    public async Task<DashboardData> GetAsync(string userId, int? assetGroupId, DateOnly trendStart, CancellationToken cancellationToken)
    {
        var trendSince = new DateTimeOffset(trendStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Sql;
        command.CommandTimeout = 60;
        command.WithParameter("@UserId", SqlDbType.NVarChar, 450, userId);
        command.WithParameter("@AssetGroupId", SqlDbType.Int, assetGroupId);
        command.WithParameter("@MttrSince", SqlDbType.DateTimeOffset, DateTimeOffset.UtcNow.AddDays(-DashboardLimits.MttrWindowDays));
        command.WithParameter("@TrendSince", SqlDbType.DateTimeOffset, trendSince);
        command.WithParameter("@TopAssets", SqlDbType.Int, DashboardLimits.TopAssets);
        command.WithParameter("@TopPackages", SqlDbType.Int, DashboardLimits.TopPackages);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var counts = new int[7];
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = reader.GetInt32(i);
        }

        double? mttr = reader.IsDBNull(7) ? null : reader.GetDouble(7);

        await reader.NextResultAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var assetCount = reader.GetInt32(0);
        var componentCount = reader.GetInt32(1);

        await reader.NextResultAsync(cancellationToken);
        var assets = new List<RiskyAsset>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assets.Add(new RiskyAsset(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetDecimal(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        var packages = new List<RiskyPackage>();
        while (await reader.ReadAsync(cancellationToken))
        {
            packages.Add(new RiskyPackage(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetDecimal(3)));
        }

        var opened = await ReadDailyAsync(reader, cancellationToken);
        var resolved = await ReadDailyAsync(reader, cancellationToken);

        await reader.NextResultAsync(cancellationToken);
        var feeds = new List<FeedHealth>();
        while (await reader.ReadAsync(cancellationToken))
        {
            feeds.Add(new FeedHealth(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        var trend = opened.Keys.Union(resolved.Keys)
            .Select(day => new TrendPoint(day, opened.GetValueOrDefault(day), resolved.GetValueOrDefault(day)))
            .OrderBy(p => p.Day)
            .ToList();

        var summary = new DashboardSummary(
            counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], counts[6], mttr, assetCount, componentCount);

        return new DashboardData(summary, assets, packages, trend, feeds, assetGroupId);
    }

    private static async Task<Dictionary<DateOnly, int>> ReadDailyAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        await reader.NextResultAsync(cancellationToken);
        var result = new Dictionary<DateOnly, int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result[DateOnly.FromDateTime(reader.GetDateTime(0))] = reader.GetInt32(1);
        }

        return result;
    }
}
