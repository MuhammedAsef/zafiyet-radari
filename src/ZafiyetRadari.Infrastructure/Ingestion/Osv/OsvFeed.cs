using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Ingestion.Osv;

internal sealed record OsvFeedSettings(string Ecosystem, string Source);

/// <summary>
/// Bir ekosistemin OSV verisini senkronize eder.
/// İlk çalıştırma: &lt;ekosistem&gt;/all.zip (tam döküm). Sonrası: modified_id.csv ile sadece değişen kayıtlar.
/// </summary>
internal sealed class OsvFeed(
    OsvFeedSettings settings,
    FeedDownloader downloader,
    ISqlConnectionFactory connectionFactory,
    IOptions<IngestionOptions> options,
    ILogger<OsvFeed> logger) : IVulnerabilityFeed
{
    private const long MaxRecordBytes = 5 * 1024 * 1024;
    private const long MaxIndexBytes = 256L * 1024 * 1024;
    private const int MaxZipEntries = 2_000_000;
    private const int ProgressLogInterval = 10_000;

    // Kaynak tarafında aynı saniyede yazılan kayıtlar kaçmasın diye küçük bir örtüşme penceresi.
    private static readonly TimeSpan Overlap = TimeSpan.FromHours(1);

    public string Source => settings.Source;

    private IngestionOptions Options => options.Value;

    public async Task<FeedSyncResult> SyncAsync(string? cursor, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.TryParse(cursor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var since))
        {
            var changedIds = await ReadChangedIdsAsync(since - Overlap, cancellationToken);
            if (changedIds is not null)
            {
                return await SyncChangedRecordsAsync(changedIds, since, cancellationToken);
            }

            logger.LogInformation("{Source}: değişiklik sayısı sınırı aştı, tam senkronizasyon yapılıyor.", Source);
        }

        return await FullSyncAsync(cancellationToken);
    }

    private async Task<FeedSyncResult> FullSyncAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{Source}: tam döküm indiriliyor.", Source);

        await using var file = await downloader.DownloadToTempFileAsync(EcosystemUri("all.zip"), cancellationToken);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);

        if (zip.Entries.Count > MaxZipEntries)
        {
            throw new InvalidDataException($"Zip çok fazla kayıt içeriyor ({zip.Entries.Count:N0}).");
        }

        await using var writer = await OsvBatchWriter.CreateAsync(connectionFactory, cancellationToken);
        var batch = new Batch(writer, Options.BatchSize);
        var seen = 0;

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || entry.Length > MaxRecordBytes)
            {
                continue;
            }

            // Bildirilen boyut yalan olabilir: açılan veri de sınırlanır (zip bombası).
            await using var stream = new LimitedReadStream(entry.Open(), MaxRecordBytes);
            await batch.AddAsync(await TryReadRecordAsync(stream, cancellationToken), Options, settings.Ecosystem, cancellationToken);

            if (++seen % ProgressLogInterval == 0)
            {
                logger.LogInformation("{Source}: {Seen:N0} kayıt okundu, {Written:N0} yazıldı.", Source, seen, batch.Written);
            }
        }

        await batch.FlushAsync(cancellationToken);
        return new FeedSyncResult(batch.Written, Format(batch.MaxModified));
    }

    private async Task<FeedSyncResult> SyncChangedRecordsAsync(
        IReadOnlyList<string> ids, DateTimeOffset previousCursor, CancellationToken cancellationToken)
    {
        logger.LogInformation("{Source}: {Count} değişen kayıt çekiliyor.", Source, ids.Count);

        await using var writer = await OsvBatchWriter.CreateAsync(connectionFactory, cancellationToken);
        var batch = new Batch(writer, Options.BatchSize);

        foreach (var id in ids)
        {
            if (!OsvRecordMapper.IsSafeIdentifier(id))
            {
                continue;
            }

            await using var body = await downloader.DownloadToMemoryAsync(EcosystemUri($"{id}.json"), MaxRecordBytes, cancellationToken);
            if (body is null)
            {
                continue; // dökümden kaldırılmış
            }

            await batch.AddAsync(await TryReadRecordAsync(body, cancellationToken), Options, settings.Ecosystem, cancellationToken);
        }

        await batch.FlushAsync(cancellationToken);

        var cursor = batch.MaxModified is { } latest && latest > previousCursor ? latest : previousCursor;
        return new FeedSyncResult(batch.Written, Format(cursor));
    }

    /// <returns>Eşikten sonra değişen kimlikler; sınırdan fazlaysa null (tam senkronizasyon gerekir).</returns>
    private async Task<IReadOnlyList<string>?> ReadChangedIdsAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        await using var body = await downloader.DownloadToMemoryAsync(EcosystemUri("modified_id.csv"), MaxIndexBytes, cancellationToken)
            ?? throw new InvalidDataException($"{Source}: modified_id.csv bulunamadı.");

        using var reader = new StreamReader(body);
        var ids = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!ModifiedIdCsv.TryParseLine(line, out var modified, out var id))
            {
                continue;
            }

            if (modified < threshold)
            {
                break; // dosya en yeniden en eskiye sıralı
            }

            ids.Add(id);
            if (ids.Count > Options.MaxIncrementalRecords)
            {
                return null;
            }
        }

        return ids;
    }

    private Uri EcosystemUri(string fileName) =>
        new($"{Options.OsvBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(settings.Ecosystem)}/{fileName}");

    private async Task<OsvRecord?> TryReadRecordAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<OsvRecord>(stream, OsvJson.Options, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            logger.LogDebug(ex, "{Source}: okunamayan kayıt atlandı.", Source);
            return null;
        }
    }

    private static string? Format(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Kayıtları biriktirir, dolunca yazar. Aynı kimlik iki kez gelirse sonuncusu kalır.</summary>
    private sealed class Batch(OsvBatchWriter writer, int size)
    {
        private readonly Dictionary<string, OsvMappedRecord> _pending = new(StringComparer.Ordinal);

        public int Written { get; private set; }

        public DateTimeOffset? MaxModified { get; private set; }

        public async Task AddAsync(OsvRecord? record, IngestionOptions options, string ecosystem, CancellationToken cancellationToken)
        {
            if (record is null || !OsvRecordMapper.TryMap(record, ecosystem, options.IncludeMaliciousPackages, out var mapped))
            {
                return;
            }

            if (mapped.Vulnerability.ModifiedAt is { } modified && (MaxModified is null || modified > MaxModified))
            {
                MaxModified = modified;
            }

            _pending[mapped.Vulnerability.ExternalId] = mapped;
            if (_pending.Count >= size)
            {
                await FlushAsync(cancellationToken);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            Written += await writer.WriteAsync(_pending.Values, cancellationToken);
            _pending.Clear();
        }
    }
}

/// <summary>OSV modified_id.csv satırı: "&lt;RFC3339 zaman&gt;,&lt;kimlik&gt;" (üst seviyede "&lt;ekosistem&gt;/&lt;kimlik&gt;").</summary>
internal static class ModifiedIdCsv
{
    public static bool TryParseLine(string line, out DateTimeOffset modified, out string id)
    {
        modified = default;
        id = string.Empty;

        var comma = line.IndexOf(',');
        if (comma <= 0)
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(line[..comma].Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out modified))
        {
            return false;
        }

        var value = line[(comma + 1)..].Trim();
        var slash = value.LastIndexOf('/');
        id = slash >= 0 ? value[(slash + 1)..] : value;
        return id.Length > 0;
    }
}
