namespace ZafiyetRadari.Web.Logging;

/// <summary>
/// İstekten gelen değerleri (yol, sorgu) loga yazmadan önce temizler: log forging önlemi.
/// Saldırgan "%0D%0A" ile sahte bir log satırı uyduramasın; kontrol karakterleri atılır, uzunluk sınırlanır.
/// Production'da log JSON olsa da geliştirme konsolu ve dışa aktarılan düz metin loglar da korunur.
/// </summary>
internal static class LogSanitizer
{
    private const int MaxLength = 300;

    public static string Path(HttpContext context) => Clean(context.Request.Path.Value);

    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        cleaned = new string(cleaned.Where(c => !char.IsControl(c)).ToArray());
        return cleaned.Length > MaxLength ? cleaned[..MaxLength] + "…" : cleaned;
    }
}
