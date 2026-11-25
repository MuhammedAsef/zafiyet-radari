using System.Data;
using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Ingestion.Epss;

/// <summary>
/// FIRST EPSS günlük skor dosyası (gzip CSV). Aynı günün skorları zaten işlendiyse hiçbir şey yapılmaz.
/// Sadece bizim veritabanımızdaki zafiyetlere (CVE veya alias üzerinden) karşılık gelen skorlar yazılır.
/// </summary>
internal sealed class EpssFeed(
    FeedDownloader downloader,
    ISqlConnectionFactory connectionFactory,
    IOptions<IngestionOptions> options) : IVulnerabilityFeed
{
    private const long MaxCompressedBytes = 128L * 1024 * 1024;
    private const long MaxDecompressedBytes = 512L * 1024 * 1024; // gzip bombasına karşı

    // Dosyada yüz binlerce CVE var. Bozuk/eksik dosya ile güncelleme yapılmaz.
    private const int MinimumExpectedRows = 10_000;

    private const string ApplySql = """
        SET NOCOUNT ON;

        WITH CveMap AS
        (
            SELECT Id AS VulnerabilityId, ExternalId AS Cve FROM vuln.Vulnerabilities WHERE ExternalId LIKE 'CVE-%'
            UNION
            SELECT VulnerabilityId, Alias FROM vuln.VulnerabilityAliases WHERE Alias LIKE 'CVE-%'
        )
        UPDATE v SET EpssScore = x.Score, EpssPercentile = x.Percentile, EpssUpdatedAt = @ScoreDate
        FROM vuln.Vulnerabilities v
        JOIN (
            SELECT m.VulnerabilityId, MAX(e.Score) AS Score, MAX(e.Percentile) AS Percentile
            FROM CveMap m
            JOIN #Epss e ON e.Cve = m.Cve
            GROUP BY m.VulnerabilityId
        ) x ON x.VulnerabilityId = v.Id;

        SELECT @@ROWCOUNT;
        """;

    public string Source => FeedSources.Epss;

    public async Task<FeedSyncResult> SyncAsync(string? cursor, CancellationToken cancellationToken)
    {
        await using var compressed = await downloader.DownloadToMemoryAsync(new Uri(options.Value.EpssUrl), MaxCompressedBytes, cancellationToken)
            ?? throw new InvalidDataException("EPSS dosyası bulunamadı.");

        await using var decompressed = new LimitedReadStream(new GZipStream(compressed, CompressionMode.Decompress), MaxDecompressedBytes);
        using var reader = new StreamReader(decompressed);
        var dataset = EpssCsvParser.Parse(reader);

        var scoreDate = dataset.ScoreDate
            ?? throw new InvalidDataException("EPSS dosyasında skor tarihi yok.");

        var dateCursor = scoreDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (dateCursor == cursor)
        {
            return new FeedSyncResult(0, cursor); // bugünün skorları zaten işlendi
        }

        if (dataset.Rows.Count < MinimumExpectedRows)
        {
            throw new InvalidDataException($"EPSS dosyası beklenenden küçük ({dataset.Rows.Count} satır); güncelleme yapılmadı.");
        }

        var updated = await ApplyAsync(dataset.Rows, scoreDate, cancellationToken);
        return new FeedSyncResult(updated, dateCursor);
    }

    private async Task<int> ApplyAsync(IReadOnlyCollection<EpssRow> rows, DateOnly scoreDate, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE #Epss (Cve varchar(64) NOT NULL PRIMARY KEY, Score decimal(6,5) NOT NULL, Percentile decimal(6,5) NOT NULL);";
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var table = new DataTable();
        table.Columns.Add("Cve", typeof(string));
        table.Columns.Add("Score", typeof(decimal));
        table.Columns.Add("Percentile", typeof(decimal));

        foreach (var row in rows)
        {
            table.Rows.Add(row.Cve, row.Score, row.Percentile);
        }

        using (var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = "#Epss", BulkCopyTimeout = 300 })
        {
            foreach (DataColumn column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(table, cancellationToken);
        }

        await using var apply = connection.CreateCommand();
        apply.CommandText = ApplySql;
        apply.CommandTimeout = 300;
        apply.WithParameter("@ScoreDate", SqlDbType.Date, scoreDate.ToDateTime(TimeOnly.MinValue));
        return Convert.ToInt32(await apply.ExecuteScalarAsync(cancellationToken));
    }
}

internal sealed record EpssRow(string Cve, decimal Score, decimal Percentile);

internal sealed record EpssDataset(DateOnly? ScoreDate, IReadOnlyList<EpssRow> Rows);

/// <summary>
/// Biçim:
///   #model_version:v2025.03.14,score_date:2026-11-21T12:55:00Z
///   cve,epss,percentile
///   CVE-1999-0001,0.01208,0.78535
/// Hatalı satırlar sessizce atlanır, aynı CVE tekrar ederse sonuncusu kalır.
/// </summary>
internal static class EpssCsvParser
{
    public static EpssDataset Parse(TextReader reader)
    {
        DateOnly? scoreDate = null;
        var rows = new Dictionary<string, EpssRow>(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('#'))
            {
                scoreDate ??= ParseScoreDate(line);
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 3)
            {
                continue;
            }

            var cve = parts[0].Trim();
            if (!CveId.IsValid(cve) || !TryParseProbability(parts[1], out var score) || !TryParseProbability(parts[2], out var percentile))
            {
                continue; // başlık satırı dahil
            }

            rows[cve] = new EpssRow(cve, score, percentile);
        }

        return new EpssDataset(scoreDate, [.. rows.Values]);
    }

    private static bool TryParseProbability(string text, out decimal value)
    {
        if (decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0m and <= 1m)
        {
            value = decimal.Round(parsed, 5);
            return true;
        }

        value = 0m;
        return false;
    }

    private static DateOnly? ParseScoreDate(string line)
    {
        const string marker = "score_date:";
        var index = line.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var value = line[(index + marker.Length)..];
        var comma = value.IndexOf(',');
        if (comma >= 0)
        {
            value = value[..comma];
        }

        // Örnekler: 2026-11-21T12:55:00Z, 2026-11-21T00:00:00+0000 → sadece tarih kısmı kullanılır.
        value = value.Trim();
        return value.Length >= 10
            && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
