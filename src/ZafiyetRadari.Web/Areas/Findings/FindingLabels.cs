using Microsoft.AspNetCore.Mvc.Rendering;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Web.Areas.Findings;

public static class FindingLabels
{
    public static string Of(FindingPriority priority) => priority switch
    {
        FindingPriority.Now => "Hemen",
        FindingPriority.ThisWeek => "Bu hafta",
        FindingPriority.Plan => "Planla",
        FindingPriority.Watch => "İzle",
        _ => priority.ToString()
    };

    public static string Badge(FindingPriority priority) => priority switch
    {
        FindingPriority.Now => "text-bg-danger",
        FindingPriority.ThisWeek => "text-bg-warning",
        FindingPriority.Plan => "text-bg-info",
        _ => "text-bg-secondary"
    };

    public static string Of(FindingStatus status) => status switch
    {
        FindingStatus.Open => "Açık",
        FindingStatus.InReview => "İncelemede",
        FindingStatus.Fixed => "Düzeltildi",
        FindingStatus.RiskAccepted => "Risk kabul",
        FindingStatus.FalsePositive => "Yanlış alarm",
        _ => status.ToString()
    };

    public static string Badge(FindingStatus status) => status switch
    {
        FindingStatus.Open => "text-bg-danger",
        FindingStatus.InReview => "text-bg-warning",
        FindingStatus.Fixed => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string Of(MatchConfidenceLevel confidence) => confidence switch
    {
        MatchConfidenceLevel.Exact => "Kesin (paket + sürüm aralığı)",
        MatchConfidenceLevel.High => "Yüksek",
        MatchConfidenceLevel.Medium => "Orta",
        MatchConfidenceLevel.Low => "Düşük (sürüm aralığı belirsiz, doğrulayın)",
        _ => confidence.ToString()
    };

    public static IEnumerable<SelectListItem> Priorities(FindingPriority? selected) =>
        Enum.GetValues<FindingPriority>().Select(p => new SelectListItem(Of(p), p.ToString(), p == selected));

    public static IEnumerable<SelectListItem> Statuses(FindingStatus? selected) =>
        Enum.GetValues<FindingStatus>().Select(s => new SelectListItem(Of(s), s.ToString(), s == selected));

    /// <summary>Durum değiştirme formu: mevcut durum hariç.</summary>
    public static IEnumerable<SelectListItem> TargetStatuses(FindingStatus current) =>
        Enum.GetValues<FindingStatus>().Where(s => s != current).Select(s => new SelectListItem(Of(s), s.ToString()));

    /// <summary>Referans linkleri: sadece mutlak http/https adresler tıklanabilir (javascript:, data: vb. asla).</summary>
    public static bool IsSafeLink(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static string Percent(decimal? value) =>
        value is null ? "–" : $"%{value.Value * 100:0.##}";
}
