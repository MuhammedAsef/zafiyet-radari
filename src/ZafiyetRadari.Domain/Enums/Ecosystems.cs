namespace ZafiyetRadari.Domain.Enums;

/// <summary>Desteklenen paket ekosistemleri. Değerler OSV'nin ekosistem adlarıdır (veritabanında CHECK ile sınırlı).</summary>
public static class Ecosystems
{
    public const string Npm = "npm";
    public const string NuGet = "NuGet";

    public static IReadOnlyList<string> All { get; } = [Npm, NuGet];

    public static bool IsSupported(string ecosystem) => All.Contains(ecosystem, StringComparer.Ordinal);
}
