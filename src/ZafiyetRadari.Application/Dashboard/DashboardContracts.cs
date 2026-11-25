using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Dashboard;

public sealed record DashboardSummary(
    int NowCount,
    int ThisWeekCount,
    int PlanCount,
    int WatchCount,
    int InReviewCount,
    int KevOpenCount,
    int RiskAcceptedCount,
    double? MeanTimeToResolveDays,
    int AssetCount,
    int ActiveComponentCount)
{
    public int OpenCount => NowCount + ThisWeekCount + PlanCount + WatchCount;
}

public sealed record RiskyAsset(int AssetId, string Name, int OpenCount, int NowCount, decimal MaxRiskScore);

public sealed record RiskyPackage(string Ecosystem, string Name, int OpenCount, decimal MaxRiskScore);

public sealed record TrendPoint(DateOnly Day, int Opened, int Resolved);

public sealed record FeedHealth(string Source, DateTimeOffset? LastAttemptAt, DateTimeOffset? LastSuccessAt, int ItemsProcessed, string? LastError)
{
    /// <summary>Son deneme başarısızsa (hata var ve son başarı son denemeden eski).</summary>
    public bool IsFailing => LastError is not null && (LastSuccessAt is null || LastSuccessAt < LastAttemptAt);
}

public sealed record DashboardData(
    DashboardSummary Summary,
    IReadOnlyList<RiskyAsset> TopAssets,
    IReadOnlyList<RiskyPackage> TopPackages,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<FeedHealth> Feeds,
    int? AssetGroupId);

public static class DashboardLimits
{
    public const int TrendDays = 30;
    public const int MttrWindowDays = 90;
    public const int TopAssets = 5;
    public const int TopPackages = 8;
}

/// <summary>Dashboard sorguları: tek bağlantı, tek komut, birden çok sonuç kümesi. Hepsi kullanıcının gruplarıyla sınırlı.</summary>
public interface IDashboardQueries
{
    Task<DashboardData> GetAsync(string userId, int? assetGroupId, DateOnly trendStart, CancellationToken cancellationToken);
}
