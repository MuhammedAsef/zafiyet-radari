using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.Domain.Inventory;

/// <summary>
/// Doğrulanmış paket kimliği: ekosistem + ad + tam sürüm. Elle girişten de dosyadan da aynı kurallarla oluşur.
/// purl her zaman bizim tarafımızdan üretilir; dosyadaki purl'e güvenilmez.
/// </summary>
public sealed record PackageCoordinate
{
    public const int MaxNpmNameLength = 214;   // npm kuralı
    public const int MaxNuGetIdLength = 100;   // NuGet kuralı
    public const int MaxVersionLength = 100;   // inventory.Components.Version

    private PackageCoordinate(string ecosystem, string name, string version)
    {
        Ecosystem = ecosystem;
        Name = name;
        Version = version;
    }

    public string Ecosystem { get; }

    public string Name { get; }

    public string Version { get; }

    /// <summary>
    /// Tekilleştirme anahtarı. NuGet kimlikleri büyük/küçük harf duyarsızdır; veritabanındaki tekil indeks de
    /// varsayılan collation ile duyarsız olduğu için her iki ekosistemde de duyarsız karşılaştırılır.
    /// </summary>
    public string Key => $"{Ecosystem}|{Name}|{Version}".ToUpperInvariant();

    public string Purl => Ecosystem == Ecosystems.Npm
        ? $"pkg:npm/{EncodeNpmName(Name)}@{Uri.EscapeDataString(Version)}"
        : $"pkg:nuget/{Uri.EscapeDataString(Name)}@{Uri.EscapeDataString(Version)}";

    public static Result<PackageCoordinate> Create(string? ecosystem, string? name, string? version)
    {
        ecosystem = ecosystem?.Trim() ?? string.Empty;
        name = name?.Trim() ?? string.Empty;
        version = version?.Trim() ?? string.Empty;

        // Ekosistem adı kullanıcıdan farklı yazılmış gelebilir (nuget, NPM); kanonik ada çevrilir.
        var canonical = Ecosystems.All.FirstOrDefault(e => string.Equals(e, ecosystem, StringComparison.OrdinalIgnoreCase));
        if (canonical is null)
        {
            return ComponentErrors.UnsupportedEcosystem;
        }

        var validName = canonical == Ecosystems.Npm ? IsValidNpmName(name) : IsValidNuGetId(name);
        if (!validName)
        {
            return ComponentErrors.InvalidName;
        }

        if (version.Length > MaxVersionLength || !SemanticVersion.TryParse(version, out _))
        {
            return ComponentErrors.InvalidVersion;
        }

        return new PackageCoordinate(canonical, name, version);
    }

    /// <summary>Veritabanından okunan (daha önce doğrulanmış) değerler için. Dış girdi için kullanılmaz.</summary>
    internal static PackageCoordinate FromTrusted(string ecosystem, string name, string version) =>
        new(ecosystem, name, version);

    /// <summary>
    /// npm adı: "paket" veya "@kapsam/paket". Sadece URL-güvenli karakterler; boşluk, ters bölü, ".." yok.
    /// Eski paketlerde büyük harf bulunabildiği için büyük harf reddedilmez.
    /// </summary>
    public static bool IsValidNpmName(string name)
    {
        if (name.Length is 0 or > MaxNpmNameLength)
        {
            return false;
        }

        if (name[0] == '@')
        {
            var slash = name.IndexOf('/');
            return slash > 1
                && IsValidNpmSegment(name.AsSpan(1, slash - 1))
                && IsValidNpmSegment(name.AsSpan(slash + 1));
        }

        return IsValidNpmSegment(name);
    }

    /// <summary>NuGet kimliği: harf, rakam, nokta, tire, alt çizgi.</summary>
    public static bool IsValidNuGetId(string id) =>
        id.Length is > 0 and <= MaxNuGetIdLength
        && id[0] != '.' && id[^1] != '.'
        && !id.Contains("..", StringComparison.Ordinal)
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');

    private static bool IsValidNpmSegment(ReadOnlySpan<char> segment)
    {
        if (segment.IsEmpty || segment[0] is '.' or '_' || segment.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in segment)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~'))
            {
                return false;
            }
        }

        return true;
    }

    private static string EncodeNpmName(string name)
    {
        if (name[0] != '@')
        {
            return Uri.EscapeDataString(name);
        }

        var slash = name.IndexOf('/');
        return $"%40{Uri.EscapeDataString(name[1..slash])}/{Uri.EscapeDataString(name[(slash + 1)..])}";
    }
}

/// <summary>Bir envanter dosyasının varlığa uygulanmasının özeti.</summary>
public sealed record InventorySyncSummary(int Added, int Reactivated, int Unchanged, int Deactivated)
{
    public int Total => Added + Reactivated + Unchanged;
}
