using System.Text;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Entities;

public partial class InventoryUpload
{
    public const int MaxFileNameLength = 255;
    public const int Sha256Length = 32;

    public UploadFormat FormatKind => (UploadFormat)Format;

    /// <param name="originalFileName">Sadece gösterim için. Yol bilgisi ve kontrol karakterleri atılır; dosya diske yazılmaz.</param>
    public static InventoryUpload Create(
        string originalFileName,
        UploadFormat format,
        byte[] sha256,
        int sizeBytes,
        string uploadedBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sha256);
        if (sha256.Length != Sha256Length)
        {
            throw new ArgumentException("SHA-256 özeti 32 bayt olmalı.", nameof(sha256));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);

        return new InventoryUpload
        {
            OriginalFileName = SanitizeFileName(originalFileName),
            Format = (byte)format,
            Sha256 = sha256,
            SizeBytes = sizeBytes,
            UploadedBy = uploadedBy,
            UploadedAt = now
        };
    }

    /// <summary>"C:\fakepath\..\package-lock.json" → "package-lock.json". Hem / hem \ ayırıcı sayılır (tarayıcıya göre değişir).</summary>
    public static string SanitizeFileName(string? fileName)
    {
        var name = fileName ?? string.Empty;
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
        {
            name = name[(lastSeparator + 1)..];
        }

        var builder = new StringBuilder(Math.Min(name.Length, MaxFileNameLength));
        foreach (var c in name)
        {
            if (builder.Length == MaxFileNameLength)
            {
                break;
            }

            if (!char.IsControl(c) && c is not ('<' or '>' or '"' or ':' or '|' or '?' or '*'))
            {
                builder.Append(c);
            }
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? "dosya.json" : result;
    }
}
