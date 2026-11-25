using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Ingestion.Kev;

/// <summary>
/// CISA Known Exploited Vulnerabilities kataloğu. Katalog sürümü değişmediyse hiçbir şey yapılmaz.
/// Eşleştirme CVE üzerinden: zafiyetin kendi kimliği veya alias'larından biri.
/// </summary>
internal sealed class KevFeed(
    FeedDownloader downloader,
    ISqlConnectionFactory connectionFactory,
    IOptions<IngestionOptions> options) : IVulnerabilityFeed
{
    private const long MaxCatalogBytes = 64L * 1024 * 1024;

    // Katalogda 1000'den fazla kayıt var. Bozuk/eksik yanıt tüm KEV işaretlerini silmesin diye alt sınır.
    private const int MinimumExpectedEntries = 100;

    private const string ApplySql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        WITH CveMap AS
        (
            SELECT Id AS VulnerabilityId, ExternalId AS Cve FROM vuln.Vulnerabilities WHERE ExternalId LIKE 'CVE-%'
            UNION
            SELECT VulnerabilityId, Alias FROM vuln.VulnerabilityAliases WHERE Alias LIKE 'CVE-%'
        )
        SELECT m.VulnerabilityId,
               MIN(k.DateAdded) AS DateAdded,
               MIN(k.DueDate) AS DueDate,
               CAST(MAX(CAST(k.Ransomware AS int)) AS bit) AS Ransomware
        INTO #KevMatch
        FROM CveMap m
        JOIN #Kev k ON k.Cve = m.Cve
        GROUP BY m.VulnerabilityId;

        -- Katalogdan çıkarılanların işareti kalkar.
        UPDATE v SET IsKev = 0, KevAddedAt = NULL, KevDueDate = NULL, KevRansomware = NULL
        FROM vuln.Vulnerabilities v
        WHERE v.IsKev = 1 AND NOT EXISTS (SELECT 1 FROM #KevMatch x WHERE x.VulnerabilityId = v.Id);

        UPDATE v SET IsKev = 1, KevAddedAt = x.DateAdded, KevDueDate = x.DueDate, KevRansomware = x.Ransomware
        FROM vuln.Vulnerabilities v
        JOIN #KevMatch x ON x.VulnerabilityId = v.Id;

        DECLARE @Count int = @@ROWCOUNT;
        DROP TABLE #KevMatch;
        COMMIT TRANSACTION;
        SELECT @Count;
        """;

    public string Source => FeedSources.Kev;

    public async Task<FeedSyncResult> SyncAsync(string? cursor, CancellationToken cancellationToken)
    {
        await using var body = await downloader.DownloadToMemoryAsync(new Uri(options.Value.KevUrl), MaxCatalogBytes, cancellationToken)
            ?? throw new InvalidDataException("KEV kataloğu bulunamadı.");

        var catalog = await JsonSerializer.DeserializeAsync<KevCatalog>(body, KevJson.Options, cancellationToken)
            ?? throw new InvalidDataException("KEV kataloğu boş.");

        var version = catalog.CatalogVersion?.Trim();
        if (version is not null && version == cursor)
        {
            return new FeedSyncResult(0, cursor); // değişiklik yok
        }

        var entries = Map(catalog);
        if (entries.Count < MinimumExpectedEntries)
        {
            throw new InvalidDataException($"KEV kataloğu beklenenden küçük ({entries.Count} kayıt); güncelleme yapılmadı.");
        }

        var updated = await ApplyAsync(entries, cancellationToken);
        return new FeedSyncResult(updated, version);
    }

    internal static List<KevRow> Map(KevCatalog catalog)
    {
        var rows = new Dictionary<string, KevRow>(StringComparer.Ordinal);

        foreach (var entry in catalog.Vulnerabilities ?? [])
        {
            var cve = entry.CveId?.Trim();
            if (cve is null || !CveId.IsValid(cve) || !TryParseDate(entry.DateAdded, out var added))
            {
                continue;
            }

            DateOnly? due = TryParseDate(entry.DueDate, out var dueDate) ? dueDate : null;
            var ransomware = string.Equals(entry.KnownRansomwareCampaignUse?.Trim(), "Known", StringComparison.OrdinalIgnoreCase);

            rows[cve] = new KevRow(cve, added, due, ransomware);
        }

        return [.. rows.Values];
    }

    private async Task<int> ApplyAsync(IReadOnlyCollection<KevRow> entries, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE #Kev (Cve varchar(64) NOT NULL PRIMARY KEY, DateAdded date NOT NULL, DueDate date NULL, Ransomware bit NOT NULL);";
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        var table = new DataTable();
        table.Columns.Add("Cve", typeof(string));
        table.Columns.Add("DateAdded", typeof(DateTime));
        table.Columns.Add("DueDate", typeof(DateTime));
        table.Columns.Add("Ransomware", typeof(bool));

        foreach (var entry in entries)
        {
            table.Rows.Add(
                entry.Cve,
                entry.DateAdded.ToDateTime(TimeOnly.MinValue),
                entry.DueDate is { } due ? (object)due.ToDateTime(TimeOnly.MinValue) : DBNull.Value,
                entry.Ransomware);
        }

        using (var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection) { DestinationTableName = "#Kev" })
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
        return Convert.ToInt32(await apply.ExecuteScalarAsync(cancellationToken));
    }

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}

internal sealed record KevRow(string Cve, DateOnly DateAdded, DateOnly? DueDate, bool Ransomware);

internal sealed class KevCatalog
{
    [JsonPropertyName("catalogVersion")] public string? CatalogVersion { get; set; }
    [JsonPropertyName("vulnerabilities")] public List<KevEntry>? Vulnerabilities { get; set; }
}

internal sealed class KevEntry
{
    [JsonPropertyName("cveID")] public string? CveId { get; set; }
    [JsonPropertyName("dateAdded")] public string? DateAdded { get; set; }
    [JsonPropertyName("dueDate")] public string? DueDate { get; set; }
    [JsonPropertyName("knownRansomwareCampaignUse")] public string? KnownRansomwareCampaignUse { get; set; }
}

internal static class KevJson
{
    public static readonly JsonSerializerOptions Options = new() { MaxDepth = 16 };
}
