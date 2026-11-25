using System.Net;
using Microsoft.Extensions.Options;

namespace ZafiyetRadari.Infrastructure.Ingestion;

/// <summary>
/// Veri kaynaklarından güvenli indirme: sadece https, boyut sınırı (hem bildirilen hem gerçekten okunan),
/// büyük dosyalar belleğe değil kapanınca silinen geçici dosyaya.
/// </summary>
internal sealed class FeedDownloader(IHttpClientFactory httpClientFactory, IOptions<IngestionOptions> options)
{
    public const string HttpClientName = "vulnerability-feeds";

    private long MaxDownloadBytes => options.Value.MaxDownloadMegabytes * 1024L * 1024L;

    /// <summary>Büyük dosyalar (OSV all.zip) için. Dönen dosya kapatılınca diskten silinir.</summary>
    public async Task<FileStream> DownloadToTempFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(uri, MaxDownloadBytes, cancellationToken)
            ?? throw new HttpRequestException($"Kaynak bulunamadı: {uri}", null, HttpStatusCode.NotFound);

        var path = Path.Combine(Path.GetTempPath(), $"zr-feed-{Guid.NewGuid():N}.tmp");
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            await using var body = new LimitedReadStream(
                await response.Content.ReadAsStreamAsync(cancellationToken), MaxDownloadBytes);
            await body.CopyToAsync(file, cancellationToken);
            file.Position = 0;
            return file;
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    /// <summary>Küçük dosyalar için (KEV JSON, tekil OSV kaydı, CSV). Kaynak yoksa (404) null döner.</summary>
    public async Task<MemoryStream?> DownloadToMemoryAsync(Uri uri, long maxBytes, CancellationToken cancellationToken)
    {
        maxBytes = Math.Min(maxBytes, MaxDownloadBytes);

        using var response = await SendAsync(uri, maxBytes, cancellationToken);
        if (response is null)
        {
            return null;
        }

        var buffer = new MemoryStream();
        await using (var body = new LimitedReadStream(await response.Content.ReadAsStreamAsync(cancellationToken), maxBytes))
        {
            await body.CopyToAsync(buffer, cancellationToken);
        }

        buffer.Position = 0;
        return buffer;
    }

    private async Task<HttpResponseMessage?> SendAsync(Uri uri, long maxBytes, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Veri sadece https kaynaklardan çekilir.");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        try
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                return null;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > maxBytes)
            {
                throw new InvalidDataException($"Kaynak boyut sınırını aşıyor: {uri}");
            }

            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
