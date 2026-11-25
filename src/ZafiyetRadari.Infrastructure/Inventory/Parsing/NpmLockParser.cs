using System.Text.Json;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Infrastructure.Inventory.Parsing;

/// <summary>
/// package-lock.json / npm-shrinkwrap.json.
/// v2-v3: "packages" nesnesi (anahtar = node_modules yolu). v1: iç içe "dependencies" ağacı.
/// Registry dışı kaynaklar (file:, link:, git, http tarball) ve workspace bağlantıları atlanır: OSV'de karşılıkları yoktur.
/// </summary>
internal sealed class NpmLockParser : IInventoryFormatParser
{
    private const string NodeModules = "node_modules/";

    public UploadFormat Format => UploadFormat.NpmLock;

    public bool CanParse(JsonElement root) =>
        root.TryGetProperty("lockfileVersion", out var version)
        && version.ValueKind == JsonValueKind.Number
        && (HasObject(root, "packages") || HasObject(root, "dependencies"));

    public Result<ParsedInventory> Parse(JsonElement root)
    {
        var collector = new PackageCollector(Ecosystems.Npm);

        // v2 dosyaları geriye uyumluluk için "dependencies"i de içerir; ikisini birden okumak çift sayım olur.
        // Sınır aşılırsa ayrıştırma durur, Build hata döner.
        _ = HasObject(root, "packages")
            ? ParsePackages(root.GetProperty("packages"), collector)
            : ParseV1Dependencies(root.GetProperty("dependencies"), collector);

        return collector.Build(Format);
    }

    private static bool ParsePackages(JsonElement packages, PackageCollector collector)
    {
        foreach (var entry in packages.EnumerateObject())
        {
            if (entry.Name.Length == 0 || entry.Value.ValueKind != JsonValueKind.Object)
            {
                continue; // "" = kök proje
            }

            var index = entry.Name.LastIndexOf(NodeModules, StringComparison.Ordinal);
            if (index < 0 || IsTrue(entry.Value, "link"))
            {
                collector.Ignore(); // workspace klasörü veya sembolik bağlantı
                continue;
            }

            // Takma ad (npm:gercek-paket@1.0.0) kurulumlarında gerçek ad "name" alanındadır.
            var name = GetString(entry.Value, "name") ?? entry.Name[(index + NodeModules.Length)..];
            var version = GetString(entry.Value, "version");

            if (!IsRegistryVersion(version))
            {
                collector.Ignore();
                continue;
            }

            if (!collector.Add(Ecosystems.Npm, name, version))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParseV1Dependencies(JsonElement dependencies, PackageCollector collector)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(dependencies);

        while (pending.Count > 0)
        {
            foreach (var entry in pending.Pop().EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (HasObject(entry.Value, "dependencies"))
                {
                    pending.Push(entry.Value.GetProperty("dependencies"));
                }

                var name = entry.Name;
                var version = GetString(entry.Value, "version");

                // v1 takma ad biçimi: "version": "npm:gercek-paket@1.2.3"
                if (version is not null && version.StartsWith("npm:", StringComparison.Ordinal))
                {
                    var alias = version[4..];
                    var at = alias.LastIndexOf('@');
                    if (at <= 0)
                    {
                        collector.Ignore();
                        continue;
                    }

                    name = alias[..at];
                    version = alias[(at + 1)..];
                }

                if (!IsRegistryVersion(version))
                {
                    collector.Ignore();
                    continue;
                }

                if (!collector.Add(Ecosystems.Npm, name, version))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Registry'den gelen sürümler düz sürüm metnidir; diğerleri URL veya yerel yol içerir.</summary>
    private static bool IsRegistryVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version)
        && !version.Contains(':', StringComparison.Ordinal)  // file:, link:, git+https:, github:
        && !version.Contains('/', StringComparison.Ordinal); // kullanici/repo kısaltması

    private static bool HasObject(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Object;

    private static bool IsTrue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
