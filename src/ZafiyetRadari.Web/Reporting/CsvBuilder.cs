using System.Globalization;
using System.Text;

namespace ZafiyetRadari.Web.Reporting;

/// <summary>Excel'in sayı/tarihe çevirmemesi gereken değer (sürüm numarası: 4.1.1 → 04.01.2001 olmasın).</summary>
public readonly record struct CsvText(string? Value);

/// <summary>
/// Excel uyumlu CSV. Türkçe Excel ayırıcı olarak noktalı virgül, ondalık için virgül bekler; UTF-8 BOM ile Türkçe karakterler bozulmaz.
///
/// CSV/formül enjeksiyonu (OWASP): paket adı veya zafiyet özeti dış kaynaktan gelir. "=", "+", "-", "@", tab veya CR ile
/// başlayan hücre Excel'de formül olarak çalışabilir (ör. =HYPERLINK(...)). Bu hücrelerin başına tek tırnak eklenir,
/// metin olarak kalırlar.
/// </summary>
public sealed class CsvBuilder
{
    private const char Separator = ';';
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly StringBuilder _builder = new();

    /// <param name="cells">string, decimal veya <see cref="CsvText"/>.</param>
    public CsvBuilder AddRow(params object?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
            {
                _builder.Append(Separator);
            }

            _builder.Append(cells[i] switch
            {
                CsvText text => AsText(text.Value),
                decimal number => number.ToString("0.00", Turkish),
                null => string.Empty,
                var other => Escape(Convert.ToString(other, CultureInfo.InvariantCulture))
            });
        }

        _builder.Append("\r\n");
        return this;
    }

    public byte[] ToBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_builder.ToString())).ToArray();

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        var needsQuotes = value.IndexOfAny([Separator, '"', '\r', '\n']) >= 0;
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }

    /// <summary>
    /// Excel'de metin olarak kalması gereken değer: ="4.1.1" biçiminde yazılır.
    /// Bu bir formül, bu yüzden sadece güvenli karakter kümesinde (harf, rakam, . - _ + ~) uygulanır;
    /// değer bu kümenin dışındaysa normal kaçışlamaya düşülür, dış veri formüle giremez.
    /// </summary>
    public static string AsText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var safe = value.Length <= 100 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+' or '~');
        return safe ? $"=\"{value}\"" : Escape(value);
    }
}
