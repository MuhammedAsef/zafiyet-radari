using System.Data;
using Microsoft.Data.SqlClient;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Ingestion.Osv;

/// <summary>
/// OSV kayıtlarını toplu yazar: oturuma özel temp tablolara SqlBulkCopy → tek transaction'da MERGE.
/// Temp tablolar (#...) tempdb'de, sadece bu bağlantıya görünür; ek şema veya yetki gerektirmez.
/// Sadece kaynakta değişmiş (ModifiedAt daha yeni) kayıtların alt tabloları yeniden yazılır.
/// </summary>
internal sealed class OsvBatchWriter : IAsyncDisposable
{
    private const string CreateStagingSql = """
        CREATE TABLE #v (ExternalId varchar(64) NOT NULL PRIMARY KEY, Summary nvarchar(500) NULL, Details nvarchar(max) NULL,
                         PublishedAt datetimeoffset(3) NULL, ModifiedAt datetimeoffset(3) NULL, WithdrawnAt datetimeoffset(3) NULL,
                         CvssScore decimal(3,1) NULL, CvssVector varchar(200) NULL);
        CREATE TABLE #a (ExternalId varchar(64) NOT NULL, Alias varchar(64) NOT NULL);
        CREATE TABLE #p (PkgKey int NOT NULL PRIMARY KEY, ExternalId varchar(64) NOT NULL, Ecosystem varchar(20) NOT NULL,
                         PackageName nvarchar(400) NOT NULL, Purl nvarchar(1000) NULL);
        CREATE TABLE #r (PkgKey int NOT NULL, RangeType tinyint NOT NULL, Introduced nvarchar(100) NULL,
                         Fixed nvarchar(100) NULL, LastAffected nvarchar(100) NULL);
        CREATE TABLE #f (ExternalId varchar(64) NOT NULL, Type varchar(30) NOT NULL, Url nvarchar(2000) NOT NULL);
        """;

    private const string ClearStagingSql = "TRUNCATE TABLE #v; TRUNCATE TABLE #a; TRUNCATE TABLE #p; TRUNCATE TABLE #r; TRUNCATE TABLE #f;";

    private const string UpsertSql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        BEGIN TRANSACTION;

        DECLARE @Touched TABLE (Id int NOT NULL PRIMARY KEY, ExternalId varchar(64) NOT NULL);

        MERGE vuln.Vulnerabilities WITH (HOLDLOCK) AS target
        USING #v AS source ON target.ExternalId = source.ExternalId
        WHEN MATCHED AND (target.ModifiedAt IS NULL OR source.ModifiedAt IS NULL OR source.ModifiedAt > target.ModifiedAt) THEN
            UPDATE SET Summary = source.Summary, Details = source.Details, PublishedAt = source.PublishedAt,
                       ModifiedAt = source.ModifiedAt, WithdrawnAt = source.WithdrawnAt,
                       CvssScore = source.CvssScore, CvssVector = source.CvssVector, SyncedAt = SYSDATETIMEOFFSET()
        WHEN NOT MATCHED THEN
            INSERT (ExternalId, Source, Summary, Details, PublishedAt, ModifiedAt, WithdrawnAt, CvssScore, CvssVector)
            VALUES (source.ExternalId, 'OSV', source.Summary, source.Details, source.PublishedAt, source.ModifiedAt,
                    source.WithdrawnAt, source.CvssScore, source.CvssVector)
        OUTPUT inserted.Id, inserted.ExternalId INTO @Touched (Id, ExternalId);

        -- Değişen kayıtların alt verisi baştan yazılır (AffectedRanges, AffectedPackages silinince cascade ile gider).
        DELETE a FROM vuln.VulnerabilityAliases a JOIN @Touched t ON t.Id = a.VulnerabilityId;
        DELETE r FROM vuln.VulnerabilityReferences r JOIN @Touched t ON t.Id = r.VulnerabilityId;
        DELETE p FROM vuln.AffectedPackages p JOIN @Touched t ON t.Id = p.VulnerabilityId;

        INSERT INTO vuln.VulnerabilityAliases (VulnerabilityId, Alias)
        SELECT DISTINCT t.Id, a.Alias FROM #a a JOIN @Touched t ON t.ExternalId = a.ExternalId;

        INSERT INTO vuln.VulnerabilityReferences (VulnerabilityId, Type, Url)
        SELECT t.Id, f.Type, f.Url FROM #f f JOIN @Touched t ON t.ExternalId = f.ExternalId;

        -- Yeni paket Id'lerini staging anahtarıyla eşlemek için MERGE ... OUTPUT (INSERT OUTPUT kaynak kolonunu göremez).
        DECLARE @PackageMap TABLE (PkgKey int NOT NULL PRIMARY KEY, AffectedPackageId bigint NOT NULL);

        MERGE vuln.AffectedPackages AS target
        USING (SELECT p.PkgKey, t.Id AS VulnerabilityId, p.Ecosystem, p.PackageName, p.Purl
               FROM #p p JOIN @Touched t ON t.ExternalId = p.ExternalId) AS source
        ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (VulnerabilityId, Ecosystem, PackageName, Purl)
            VALUES (source.VulnerabilityId, source.Ecosystem, source.PackageName, source.Purl)
        OUTPUT source.PkgKey, inserted.Id INTO @PackageMap (PkgKey, AffectedPackageId);

        INSERT INTO vuln.AffectedRanges (AffectedPackageId, RangeType, Introduced, Fixed, LastAffected)
        SELECT m.AffectedPackageId, r.RangeType, r.Introduced, r.Fixed, r.LastAffected
        FROM #r r JOIN @PackageMap m ON m.PkgKey = r.PkgKey;

        DECLARE @Count int = (SELECT COUNT(*) FROM @Touched);
        COMMIT TRANSACTION;
        SELECT @Count;
        """;

    private readonly SqlConnection _connection;
    private int _nextPackageKey;

    private OsvBatchWriter(SqlConnection connection)
    {
        _connection = connection;
    }

    public static async Task<OsvBatchWriter> CreateAsync(ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var writer = new OsvBatchWriter(connection);

        try
        {
            await writer.CreateStagingTablesAsync(cancellationToken);
            return writer;
        }
        catch
        {
            await writer.DisposeAsync();
            throw;
        }
    }

    /// <returns>Eklenen veya güncellenen zafiyet sayısı.</returns>
    public async Task<int> WriteAsync(IReadOnlyCollection<OsvMappedRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return 0;
        }

        await ClearStagingTablesAsync(cancellationToken);

        var (vulnerabilities, aliases, packages, ranges, references) = BuildTables(records);
        await BulkCopyAsync("#v", vulnerabilities, cancellationToken);
        await BulkCopyAsync("#a", aliases, cancellationToken);
        await BulkCopyAsync("#p", packages, cancellationToken);
        await BulkCopyAsync("#r", ranges, cancellationToken);
        await BulkCopyAsync("#f", references, cancellationToken);

        await using var command = _connection.CreateCommand();
        command.CommandText = UpsertSql;
        command.CommandTimeout = 300;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private (DataTable, DataTable, DataTable, DataTable, DataTable) BuildTables(IReadOnlyCollection<OsvMappedRecord> records)
    {
        var vulnerabilities = Table(
            ("ExternalId", typeof(string)), ("Summary", typeof(string)), ("Details", typeof(string)),
            ("PublishedAt", typeof(DateTimeOffset)), ("ModifiedAt", typeof(DateTimeOffset)), ("WithdrawnAt", typeof(DateTimeOffset)),
            ("CvssScore", typeof(decimal)), ("CvssVector", typeof(string)));
        var aliases = Table(("ExternalId", typeof(string)), ("Alias", typeof(string)));
        var packages = Table(
            ("PkgKey", typeof(int)), ("ExternalId", typeof(string)), ("Ecosystem", typeof(string)),
            ("PackageName", typeof(string)), ("Purl", typeof(string)));
        var ranges = Table(
            ("PkgKey", typeof(int)), ("RangeType", typeof(byte)), ("Introduced", typeof(string)),
            ("Fixed", typeof(string)), ("LastAffected", typeof(string)));
        var references = Table(("ExternalId", typeof(string)), ("Type", typeof(string)), ("Url", typeof(string)));

        foreach (var record in records)
        {
            var v = record.Vulnerability;
            vulnerabilities.Rows.Add(v.ExternalId, Db(v.Summary), Db(v.Details), Db(v.PublishedAt), Db(v.ModifiedAt),
                Db(v.WithdrawnAt), Db(v.CvssScore), Db(v.CvssVector));

            foreach (var alias in record.Aliases)
            {
                aliases.Rows.Add(v.ExternalId, alias);
            }

            foreach (var reference in record.References)
            {
                references.Rows.Add(v.ExternalId, reference.Type, reference.Url);
            }

            foreach (var package in record.Packages)
            {
                var key = ++_nextPackageKey;
                packages.Rows.Add(key, v.ExternalId, package.Ecosystem, package.PackageName, Db(package.Purl));

                foreach (var range in package.Ranges)
                {
                    ranges.Rows.Add(key, range.RangeType, Db(range.Introduced), Db(range.Fixed), Db(range.LastAffected));
                }
            }
        }

        return (vulnerabilities, aliases, packages, ranges, references);
    }

    private async Task BulkCopyAsync(string tableName, DataTable table, CancellationToken cancellationToken)
    {
        if (table.Rows.Count == 0)
        {
            return;
        }

        using var bulkCopy = new SqlBulkCopy(_connection) { DestinationTableName = tableName, BulkCopyTimeout = 300 };
        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }

    // SQL metinleri sabit; komut metni hiçbir zaman parametreden/girdiden oluşmaz (CA2100).
    private async Task CreateStagingTablesAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = CreateStagingSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ClearStagingTablesAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = ClearStagingSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DataTable Table(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private static object Db(object? value) => value ?? DBNull.Value;
}
