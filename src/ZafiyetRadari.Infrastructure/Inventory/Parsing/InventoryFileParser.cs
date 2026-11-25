using System.Text.Json;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Infrastructure.Inventory.Parsing;

/// <summary>Tek bir dosya biçiminin ayrıştırıcısı. Yeni biçim = yeni sınıf + DI kaydı (Open/Closed).</summary>
internal interface IInventoryFormatParser
{
    UploadFormat Format { get; }

    /// <summary>Kök nesneye bakarak bu biçim mi karar verir. Ucuz olmalı; içeriği dolaşmaz.</summary>
    bool CanParse(JsonElement root);

    Result<ParsedInventory> Parse(JsonElement root);
}

/// <summary>
/// Biçimi içerikten tanır (dosya adı/uzantı/Content-Type'a güvenilmez) ve ilgili ayrıştırıcıya yönlendirir.
/// Güvenilmez girdi: derinlik sınırlı, yorum ve sondaki virgül reddedilir, paket sayısı sınırlı.
/// </summary>
internal sealed class InventoryFileParser(IEnumerable<IInventoryFormatParser> parsers) : IInventoryFileParser
{
    // npm v1 lock dosyalarında node_modules iç içeliği derinleşebilir; 128 yeterli ve yığın taşmasına karşı güvenli.
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = 128,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    public Result<ParsedInventory> Parse(ReadOnlyMemory<byte> content)
    {
        if (content.Span.StartsWith(Utf8Bom))
        {
            content = content[Utf8Bom.Length..]; // Windows araçları (PowerShell, Notepad) BOM ekleyebilir
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, DocumentOptions);
        }
        catch (JsonException)
        {
            return UploadErrors.InvalidJson;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return UploadErrors.UnsupportedFormat;
            }

            var parser = parsers.FirstOrDefault(p => p.CanParse(root));
            return parser is null ? UploadErrors.UnsupportedFormat : parser.Parse(root);
        }
    }
}

/// <summary>Ayrıştırıcıların ortak toplayıcısı: paket sayısı sınırını tek yerde uygular.</summary>
internal sealed class PackageCollector(string ecosystemForCoverage)
{
    private readonly List<RawPackage> _packages = [];
    private readonly HashSet<string> _covered = new(StringComparer.Ordinal);

    public PackageCollector() : this(string.Empty)
    {
    }

    public int Ignored { get; private set; }

    public bool LimitExceeded { get; private set; }

    /// <returns>Sınır aşıldıysa false; ayrıştırıcı durmalıdır.</returns>
    public bool Add(string ecosystem, string? name, string? version)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
        {
            Ignored++;
            return true;
        }

        if (_packages.Count >= InventoryLimits.MaxComponentsPerUpload)
        {
            LimitExceeded = true;
            return false;
        }

        _packages.Add(new RawPackage(ecosystem, name, version));
        _covered.Add(ecosystem);
        return true;
    }

    public void Ignore() => Ignored++;

    public Result<ParsedInventory> Build(UploadFormat format)
    {
        if (LimitExceeded)
        {
            return UploadErrors.TooManyComponents(InventoryLimits.MaxComponentsPerUpload);
        }

        // Lock dosyası tek ekosistemi tam listeler; paket olmasa bile (boş proje) o ekosistemi kapsar.
        if (ecosystemForCoverage.Length > 0)
        {
            _covered.Add(ecosystemForCoverage);
        }

        return new ParsedInventory(format, _packages, _covered.ToList(), Ignored);
    }
}
