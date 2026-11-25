namespace ZafiyetRadari.Infrastructure.Ingestion;

/// <summary>
/// Okunan bayt sayısı sınırı aşınca durdurur. Dış kaynaktan gelen her akış bununla sarılır:
/// sunucunun bildirdiği boyut yalan olabilir, sıkıştırılmış veri açılınca devleşebilir (zip/gzip bombası).
/// </summary>
internal sealed class LimitedReadStream(Stream inner, long maxBytes) : Stream
{
    private long _bytesRead;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Track(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Track(inner.Read(buffer));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Track(await inner.ReadAsync(buffer, cancellationToken));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        await base.DisposeAsync();
    }

    private int Track(int read)
    {
        _bytesRead += read;
        if (_bytesRead > maxBytes)
        {
            throw new InvalidDataException($"Veri boyut sınırı aşıldı ({maxBytes:N0} bayt).");
        }

        return read;
    }
}
