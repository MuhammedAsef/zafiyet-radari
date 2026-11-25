using System.Diagnostics.CodeAnalysis;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Vulnerabilities;

namespace ZafiyetRadari.Infrastructure.Ingestion.Osv;

/// <summary>
/// OSV kaydını veritabanı satırlarına çevirir. Dış kaynak verisi güvenilmez kabul edilir:
/// her alan şema sınırlarına göre kesilir veya atılır, sadece http/https referans linkleri tutulur.
/// </summary>
internal static class OsvRecordMapper
{
    public const int MaxExternalIdLength = 64;
    public const int MaxSummaryLength = 500;
    public const int MaxDetailsLength = 20_000;
    public const int MaxPackageNameLength = 400;
    public const int MaxPurlLength = 1000;
    public const int MaxVersionLength = 100;
    public const int MaxUrlLength = 2000;
    public const int MaxAliases = 50;
    public const int MaxReferences = 50;
    public const int MaxRangesPerPackage = 200;

    public static bool TryMap(
        OsvRecord record,
        string ecosystem,
        bool includeMalicious,
        [NotNullWhen(true)] out OsvMappedRecord? mapped)
    {
        mapped = null;

        var id = record.Id?.Trim();
        if (!IsSafeIdentifier(id))
        {
            return false;
        }

        if (!includeMalicious && id.StartsWith("MAL-", StringComparison.Ordinal))
        {
            return false;
        }

        var packages = MapPackages(record.Affected, ecosystem);
        if (packages.Count == 0)
        {
            return false; // bu ekosistemi etkilemiyor
        }

        var (cvssScore, cvssVector) = MapSeverity(record.Severity);

        mapped = new OsvMappedRecord(
            new OsvVulnerabilityRow(
                id,
                Truncate(record.Summary, MaxSummaryLength),
                Truncate(record.Details, MaxDetailsLength),
                record.Published,
                record.Modified,
                record.Withdrawn,
                cvssScore,
                cvssVector),
            MapAliases(record.Aliases, id),
            packages,
            MapReferences(record.References));

        return true;
    }

    /// <summary>
    /// OSV "events" listesini (introduced, fixed, last_affected) aralık çiftlerine açar.
    /// Örnek: [introduced 0, fixed 1.2, introduced 2.0, last_affected 2.3] → [0, 1.2) ve [2.0, 2.3].
    /// "limit" olayları yok sayılır; introduced'sız fixed geçersiz kabul edilir.
    /// </summary>
    public static IEnumerable<(string? Introduced, string? Fixed, string? LastAffected)> FlattenEvents(IEnumerable<OsvEvent> events)
    {
        string? introduced = null;
        var open = false;

        foreach (var osvEvent in events)
        {
            if (osvEvent.Introduced is not null)
            {
                if (open)
                {
                    yield return (introduced, null, null);
                }

                introduced = osvEvent.Introduced;
                open = true;
            }
            else if (osvEvent.Fixed is not null && open)
            {
                yield return (introduced, osvEvent.Fixed, null);
                open = false;
            }
            else if (osvEvent.LastAffected is not null && open)
            {
                yield return (introduced, null, osvEvent.LastAffected);
                open = false;
            }
        }

        if (open)
        {
            yield return (introduced, null, null);
        }
    }

    /// <summary>OSV kimlikleri URL yoluna da girdiği için sadece güvenli karakterler.</summary>
    public static bool IsSafeIdentifier([NotNullWhen(true)] string? id) =>
        !string.IsNullOrEmpty(id)
        && id.Length <= MaxExternalIdLength
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static List<OsvPackageRow> MapPackages(List<OsvAffected>? affected, string ecosystem)
    {
        var packages = new List<OsvPackageRow>();

        foreach (var item in affected ?? [])
        {
            if (!string.Equals(item.Package?.Ecosystem, ecosystem, StringComparison.Ordinal))
            {
                continue;
            }

            var name = item.Package!.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaxPackageNameLength)
            {
                continue;
            }

            var purl = item.Package.Purl is { Length: > 0 and <= MaxPurlLength } p ? p : null;
            packages.Add(new OsvPackageRow(ecosystem, name, purl, MapRanges(item)));
        }

        return packages;
    }

    private static List<OsvRangeRow> MapRanges(OsvAffected affected)
    {
        var ranges = new List<OsvRangeRow>();

        foreach (var range in affected.Ranges ?? [])
        {
            var rangeType = range.Type switch
            {
                "SEMVER" => (byte)VersionRangeType.Semver,
                "ECOSYSTEM" => (byte)VersionRangeType.Ecosystem,
                _ => (byte)0 // GIT commit aralıkları paket sürümüyle karşılaştırılamaz
            };

            if (rangeType == 0)
            {
                continue;
            }

            foreach (var (introduced, fixedVersion, lastAffected) in FlattenEvents(range.Events ?? []))
            {
                if (IsValidVersion(introduced) && IsValidVersion(fixedVersion) && IsValidVersion(lastAffected))
                {
                    ranges.Add(new OsvRangeRow(rangeType, introduced, fixedVersion, lastAffected));
                }
            }
        }

        // Aralık yoksa ama tek tek etkilenen sürümler verildiyse: her biri [v, v] tam eşleşme aralığı olur.
        if (ranges.Count == 0)
        {
            foreach (var version in affected.Versions ?? [])
            {
                if (!string.IsNullOrWhiteSpace(version) && IsValidVersion(version))
                {
                    ranges.Add(new OsvRangeRow((byte)VersionRangeType.Ecosystem, version, null, version));
                }
            }
        }

        return ranges.Count <= MaxRangesPerPackage ? ranges : ranges.GetRange(0, MaxRangesPerPackage);
    }

    private static (decimal? Score, string? Vector) MapSeverity(List<OsvSeverity>? severities)
    {
        var v3 = severities?.FirstOrDefault(s => s.Type == "CVSS_V3" && s.Score is not null);
        if (v3 is not null && CvssV3Calculator.TryCalculateBaseScore(v3.Score, out var score))
        {
            return (score, v3.Score!.Trim());
        }

        // CVSS v4 puanı tablo tabanlı hesaplanır, şimdilik sadece vektörü saklıyoruz.
        var v4 = severities?.FirstOrDefault(s => s.Type == "CVSS_V4" && s.Score is { Length: <= CvssV3Calculator.MaxVectorLength });
        return (null, v4?.Score?.Trim());
    }

    private static List<string> MapAliases(List<string>? aliases, string id) =>
        (aliases ?? [])
            .Select(alias => alias?.Trim())
            .Where(alias => IsSafeIdentifier(alias) && alias != id)
            .Select(alias => alias!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxAliases)
            .ToList();

    private static List<OsvReferenceRow> MapReferences(List<OsvReference>? references) =>
        (references ?? [])
            .Where(reference => IsSafeHttpUrl(reference.Url))
            .Select(reference => new OsvReferenceRow(
                NormalizeReferenceType(reference.Type),
                reference.Url!.Trim()))
            .DistinctBy(reference => reference.Url, StringComparer.Ordinal)
            .Take(MaxReferences)
            .ToList();

    /// <summary>javascript:, data:, file: gibi şemalar arayüzde link olarak gösterilirse XSS olur; sadece http/https.</summary>
    private static bool IsSafeHttpUrl(string? url) =>
        url is { Length: > 0 and <= MaxUrlLength }
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string NormalizeReferenceType(string? type)
    {
        var value = type?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(value) || value.Length > 30 || !value.All(c => char.IsAsciiLetterUpper(c) || c == '_')
            ? "WEB"
            : value;
    }

    private static bool IsValidVersion(string? version) => version is null || version.Length <= MaxVersionLength;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
