using System.Text.Json;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Infrastructure.Inventory.Parsing;

/// <summary>
/// packages.lock.json (RestorePackagesWithLockFile). Yapı: dependencies → hedef çerçeve (net10.0...) → paket → { type, resolved }.
/// Aynı paket birden çok hedef çerçevede tekrar eder; tekilleştirme Domain'de yapılır.
/// "Project" tipindeki girdiler (çözümdeki diğer projeler) paket değildir, atlanır.
/// </summary>
internal sealed class NuGetLockParser : IInventoryFormatParser
{
    public UploadFormat Format => UploadFormat.NuGetLock;

    public bool CanParse(JsonElement root) =>
        !root.TryGetProperty("lockfileVersion", out _)
        && root.TryGetProperty("version", out var version)
        && version.ValueKind == JsonValueKind.Number
        && root.TryGetProperty("dependencies", out var dependencies)
        && dependencies.ValueKind == JsonValueKind.Object;

    public Result<ParsedInventory> Parse(JsonElement root)
    {
        var collector = new PackageCollector(Ecosystems.NuGet);

        foreach (var framework in root.GetProperty("dependencies").EnumerateObject())
        {
            if (framework.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var package in framework.Value.EnumerateObject())
            {
                if (package.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = GetString(package.Value, "type");
                if (string.Equals(type, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    collector.Ignore();
                    continue;
                }

                if (!collector.Add(Ecosystems.NuGet, package.Name, GetString(package.Value, "resolved")))
                {
                    return collector.Build(Format);
                }
            }
        }

        return collector.Build(Format);
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
