namespace ZafiyetRadari.Infrastructure.Ingestion;

/// <summary>KEV ve EPSS satırlarındaki CVE kimliği doğrulaması (CVE-YYYY-NNNN...; varchar(64) kolonuna sığar).</summary>
internal static class CveId
{
    public static bool IsValid(string? value) =>
        value is { Length: >= 13 and <= 64 }
        && value.StartsWith("CVE-", StringComparison.Ordinal)
        && value.AsSpan(4).IndexOfAnyExcept("0123456789-") < 0;
}
