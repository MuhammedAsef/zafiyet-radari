using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZafiyetRadari.Infrastructure.Ingestion.Osv;

// OSV şeması (https://ossf.github.io/osv-schema/). Sadece kullandığımız alanlar.

internal sealed class OsvRecord
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("modified")] public DateTimeOffset? Modified { get; set; }
    [JsonPropertyName("published")] public DateTimeOffset? Published { get; set; }
    [JsonPropertyName("withdrawn")] public DateTimeOffset? Withdrawn { get; set; }
    [JsonPropertyName("aliases")] public List<string>? Aliases { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("details")] public string? Details { get; set; }
    [JsonPropertyName("severity")] public List<OsvSeverity>? Severity { get; set; }
    [JsonPropertyName("affected")] public List<OsvAffected>? Affected { get; set; }
    [JsonPropertyName("references")] public List<OsvReference>? References { get; set; }
}

internal sealed class OsvSeverity
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("score")] public string? Score { get; set; }
}

internal sealed class OsvAffected
{
    [JsonPropertyName("package")] public OsvPackage? Package { get; set; }
    [JsonPropertyName("ranges")] public List<OsvRange>? Ranges { get; set; }
    [JsonPropertyName("versions")] public List<string>? Versions { get; set; }
}

internal sealed class OsvPackage
{
    [JsonPropertyName("ecosystem")] public string? Ecosystem { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("purl")] public string? Purl { get; set; }
}

internal sealed class OsvRange
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("events")] public List<OsvEvent>? Events { get; set; }
}

internal sealed class OsvEvent
{
    [JsonPropertyName("introduced")] public string? Introduced { get; set; }
    [JsonPropertyName("fixed")] public string? Fixed { get; set; }
    [JsonPropertyName("last_affected")] public string? LastAffected { get; set; }
    [JsonPropertyName("limit")] public string? Limit { get; set; }
}

internal sealed class OsvReference
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}

internal static class OsvJson
{
    /// <summary>Güvenilmez girdi: iç içe derinlik sınırlı, yorum/sondaki virgül kabul edilmez.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 32,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };
}

// Veritabanına yazılacak, doğrulanmış ve sınırlandırılmış satırlar.

internal sealed record OsvMappedRecord(
    OsvVulnerabilityRow Vulnerability,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<OsvPackageRow> Packages,
    IReadOnlyList<OsvReferenceRow> References);

internal sealed record OsvVulnerabilityRow(
    string ExternalId,
    string? Summary,
    string? Details,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ModifiedAt,
    DateTimeOffset? WithdrawnAt,
    decimal? CvssScore,
    string? CvssVector);

internal sealed record OsvPackageRow(string Ecosystem, string PackageName, string? Purl, IReadOnlyList<OsvRangeRow> Ranges);

internal sealed record OsvRangeRow(byte RangeType, string? Introduced, string? Fixed, string? LastAffected);

internal sealed record OsvReferenceRow(string Type, string Url);
