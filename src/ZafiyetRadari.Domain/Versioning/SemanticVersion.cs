using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ZafiyetRadari.Domain.Versioning;

/// <summary>
/// SemVer 2.0 sürümü (npm ve NuGet için). Karşılaştırma kuralları semver.org §11'e göredir:
/// 1.0.0-alpha &lt; 1.0.0-alpha.1 &lt; 1.0.0-alpha.beta &lt; 1.0.0-beta &lt; 1.0.0-beta.2 &lt; 1.0.0-beta.11 &lt; 1.0.0-rc.1 &lt; 1.0.0
///
/// Ekosistem gerçekleri için bilinçli esneklikler: "v" ve "=" öneki, eksik minor/patch ("1.2" = 1.2.0),
/// NuGet'in 4. bileşeni (1.2.3.4). Build metadata (+...) karşılaştırmaya katılmaz.
///
/// Güvenlik: girdi dış kaynaktan (SBOM, OSV) geldiği için regex kullanılmaz (ReDoS yok), uzunluk sınırlıdır
/// ve hatalı girdi exception değil false döner.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    public const int MaxLength = 256;

    private readonly string[] _preRelease;

    private SemanticVersion(int major, int minor, int patch, int revision, string[] preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Revision = revision;
        _preRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>NuGet'in 4. bileşeni; SemVer sürümlerinde 0.</summary>
    public int Revision { get; }

    public IReadOnlyList<string> PreRelease => _preRelease;

    public bool IsPreRelease => _preRelease.Length > 0;

    public static SemanticVersion Parse(string input) =>
        TryParse(input, out var version)
            ? version
            : throw new FormatException($"Geçersiz sürüm: '{input}'.");

    public static bool TryParse(string? input, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxLength)
        {
            return false;
        }

        var text = input.Trim();
        if (text[0] is 'v' or 'V' or '=')
        {
            text = text[1..];
        }

        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            if (!AreValidIdentifiers(text[(plus + 1)..]))
            {
                return false;
            }

            text = text[..plus];
        }

        string[] preRelease = [];
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            var preReleaseText = text[(dash + 1)..];
            if (!AreValidIdentifiers(preReleaseText))
            {
                return false;
            }

            preRelease = preReleaseText.Split('.');
            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!TryParseNumber(parts[i], out numbers[i]))
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], numbers[3], preRelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        result = Revision.CompareTo(other.Revision);
        if (result != 0) return result;

        // Pre-release'i olmayan sürüm, aynı çekirdekli pre-release sürümden büyüktür: 1.0.0-rc.1 < 1.0.0
        if (!IsPreRelease)
        {
            return other.IsPreRelease ? 1 : 0;
        }

        if (!other.IsPreRelease)
        {
            return -1;
        }

        var length = Math.Min(_preRelease.Length, other._preRelease.Length);
        for (var i = 0; i < length; i++)
        {
            result = CompareIdentifiers(_preRelease[i], other._preRelease[i]);
            if (result != 0)
            {
                return result;
            }
        }

        // Ortak kısım eşitse daha çok tanımlayıcısı olan büyüktür: alpha < alpha.1
        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);
        hash.Add(Revision);
        foreach (var identifier in _preRelease)
        {
            hash.Add(NormalizeNumeric(identifier), StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var core = Revision > 0 ? $"{Major}.{Minor}.{Patch}.{Revision}" : $"{Major}.{Minor}.{Patch}";
        return IsPreRelease ? $"{core}-{string.Join('.', _preRelease)}" : core;
    }

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) == 0;

    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) != 0;

    public static bool operator <(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) < 0;

    public static bool operator >(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) > 0;

    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) <= 0;

    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) => Compare(left, right) >= 0;

    private static int Compare(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? (right is null ? 0 : -1) : left.CompareTo(right);

    /// <summary>Sayısal tanımlayıcılar sayı olarak, diğerleri ASCII sırasıyla; sayısal olan her zaman küçüktür.</summary>
    private static int CompareIdentifiers(string left, string right)
    {
        var leftNumeric = IsNumeric(left);
        var rightNumeric = IsNumeric(right);

        if (leftNumeric && rightNumeric)
        {
            // int'e çevirmeden karşılaştır: çok uzun sayılar taşma yaratmasın.
            var a = NormalizeNumeric(left);
            var b = NormalizeNumeric(right);
            var byLength = a.Length.CompareTo(b.Length);
            return byLength != 0 ? byLength : Math.Sign(string.CompareOrdinal(a, b));
        }

        if (leftNumeric) return -1;
        if (rightNumeric) return 1;

        return Math.Sign(string.CompareOrdinal(left, right));
    }

    private static string NormalizeNumeric(string identifier)
    {
        if (!IsNumeric(identifier))
        {
            return identifier;
        }

        var trimmed = identifier.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static bool IsNumeric(string identifier)
    {
        foreach (var character in identifier)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return identifier.Length > 0;
    }

    private static bool TryParseNumber(string text, out int value)
    {
        value = 0;
        return text.Length > 0
            && IsNumeric(text)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Nokta ile ayrılmış, boş olmayan [0-9A-Za-z-] tanımlayıcılar.</summary>
    private static bool AreValidIdentifiers(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var identifier in text.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            foreach (var character in identifier)
            {
                if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
