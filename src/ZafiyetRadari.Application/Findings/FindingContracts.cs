using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Findings;

/// <param name="Status">Belirli bir durum. Boşsa <paramref name="IncludeClosed"/> belirler: false = sadece aksiyon bekleyenler (Açık + İncelemede).</param>
public sealed record FindingFilter(
    FindingPriority? Priority = null,
    FindingStatus? Status = null,
    bool IncludeClosed = false,
    int? AssetGroupId = null,
    int? AssetId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 100;
}

public sealed record FindingListItem(
    long Id,
    FindingPriority Priority,
    decimal RiskScore,
    FindingStatus Status,
    MatchConfidenceLevel Confidence,
    string ExternalId,
    string? CveId,
    string? Summary,
    bool IsKev,
    string Ecosystem,
    string PackageName,
    string Version,
    int AssetId,
    string AssetName,
    string AssetGroupName,
    DateTimeOffset FirstSeenAt);

public sealed record FindingReference(string Type, string Url);

public sealed record FindingHistoryItem(
    FindingStatus? FromStatus,
    FindingStatus ToStatus,
    string? Comment,
    string ChangedBy,
    DateTimeOffset ChangedAt);

public sealed record FindingDetails
{
    public required long Id { get; init; }
    public required FindingStatus Status { get; init; }
    public required FindingPriority Priority { get; init; }
    public required decimal RiskScore { get; init; }
    public required MatchConfidenceLevel Confidence { get; init; }
    public required DateTimeOffset FirstSeenAt { get; init; }
    public required DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public DateOnly? RiskAcceptedUntil { get; init; }

    public required AssetGroupRole MyRole { get; init; }
    public required int AssetGroupId { get; init; }
    public required string AssetGroupName { get; init; }
    public required int AssetId { get; init; }
    public required string AssetName { get; init; }
    public required AssetEnvironment AssetEnvironment { get; init; }
    public required AssetCriticality AssetCriticality { get; init; }
    public required bool AssetIsInternetFacing { get; init; }

    public required string Ecosystem { get; init; }
    public required string PackageName { get; init; }
    public required string Version { get; init; }
    public required bool ComponentIsActive { get; init; }

    public required string ExternalId { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public string? Summary { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset? ModifiedAt { get; init; }
    public decimal? CvssScore { get; init; }
    public string? CvssVector { get; init; }
    public decimal? EpssScore { get; init; }
    public decimal? EpssPercentile { get; init; }
    public required bool IsKev { get; init; }
    public DateOnly? KevAddedAt { get; init; }
    public DateOnly? KevDueDate { get; init; }
    public bool KevRansomware { get; init; }

    public required IReadOnlyList<string> FixedVersions { get; init; }
    public string? RecommendedVersion { get; init; }
    public required IReadOnlyList<FindingReference> References { get; init; }
    public required IReadOnlyList<FindingHistoryItem> History { get; init; }

    /// <summary>"Neden bu öncelik?" Skor saklanmaz, güncel verilerle yeniden açıklanır.</summary>
    public IReadOnlyList<string> RiskFactors { get; init; } = [];

    public bool IsMaliciousPackage => ExternalId.StartsWith("MAL-", StringComparison.Ordinal);
}

public sealed record ChangeFindingStatusCommand(
    long FindingId,
    FindingStatus NewStatus,
    string? Comment,
    DateOnly? RiskAcceptedUntil);

public static class FindingLimits
{
    public const int MaxCommentLength = Finding.MaxCommentLength;
    public const int MaxRiskAcceptanceDays = Finding.MaxRiskAcceptanceDays;

    /// <summary>CSV dışa aktarma üst sınırı (bellek ve sorgu süresi).</summary>
    public const int MaxExportRows = 10_000;
}
