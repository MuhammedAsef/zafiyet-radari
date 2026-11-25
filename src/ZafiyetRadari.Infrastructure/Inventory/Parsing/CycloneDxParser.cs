using System.Text.Json;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Infrastructure.Inventory.Parsing;

/// <summary>
/// CycloneDX JSON SBOM (1.2+). Paket kimliği purl'den alınır; purl'ü olmayan veya npm/NuGet dışı bileşenler atlanır.
/// İç içe "components" dizileri de dolaşılır (yığın ile, özyineleme yok).
/// Kapsam: SBOM'da görülen ekosistemler.
/// </summary>
internal sealed class CycloneDxParser : IInventoryFormatParser
{
    public UploadFormat Format => UploadFormat.CycloneDx;

    public bool CanParse(JsonElement root) =>
        root.TryGetProperty("bomFormat", out var format)
        && format.ValueKind == JsonValueKind.String
        && format.ValueEquals("CycloneDX");

    public Result<ParsedInventory> Parse(JsonElement root)
    {
        var collector = new PackageCollector();

        if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            return collector.Build(Format);
        }

        var pending = new Stack<JsonElement>();
        pending.Push(components);

        while (pending.Count > 0)
        {
            foreach (var component in pending.Pop().EnumerateArray())
            {
                if (component.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (component.TryGetProperty("components", out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    pending.Push(nested);
                }

                var purl = component.TryGetProperty("purl", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                if (!PurlParser.TryParse(purl, out var ecosystem, out var name, out var version))
                {
                    collector.Ignore();
                    continue;
                }

                if (!collector.Add(ecosystem, name, version))
                {
                    return collector.Build(Format);
                }
            }
        }

        return collector.Build(Format);
    }
}

/// <summary>
/// purl (package-url) ayrıştırma: pkg:type/namespace/name@version?qualifiers#subpath. Sadece npm ve nuget tipleri.
/// Bileşenler yüzde-kodlamalıdır; "@" npm kapsamında %40 olarak gelir.
/// </summary>
internal static class PurlParser
{
    public const int MaxPurlLength = 1000;

    public static bool TryParse(string? purl, out string ecosystem, out string name, out string version)
    {
        ecosystem = name = version = string.Empty;

        if (string.IsNullOrEmpty(purl) || purl.Length > MaxPurlLength || !purl.StartsWith("pkg:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var body = purl[4..];

        var cut = body.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            body = body[..cut];
        }

        var at = body.LastIndexOf('@');
        var slash = body.IndexOf('/');
        if (slash <= 0 || at <= slash + 1 || at == body.Length - 1)
        {
            return false; // tip yok, ad yok veya sürüm yok
        }

        var type = body[..slash].ToLowerInvariant();
        var path = body[(slash + 1)..at];
        version = Uri.UnescapeDataString(body[(at + 1)..]);

        switch (type)
        {
            case "npm":
                ecosystem = Ecosystems.Npm;
                var segments = path.Split('/');
                name = segments.Length switch
                {
                    1 => Uri.UnescapeDataString(segments[0]),
                    2 => $"{Uri.UnescapeDataString(segments[0])}/{Uri.UnescapeDataString(segments[1])}",
                    _ => string.Empty
                };
                return name.Length > 0;

            case "nuget":
                ecosystem = Ecosystems.NuGet;
                name = path.Contains('/', StringComparison.Ordinal) ? string.Empty : Uri.UnescapeDataString(path);
                return name.Length > 0;

            default:
                return false;
        }
    }
}
