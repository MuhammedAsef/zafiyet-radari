using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Application.Inventory;

namespace ZafiyetRadari.Web.Models;

public sealed class DashboardViewModel
{
    public required DashboardData Data { get; init; }

    public required IReadOnlyList<AssetGroupListItem> Groups { get; init; }

    /// <summary>
    /// Grafik verisi. Sayfaya &lt;script type="application/json"&gt; olarak gömülür: tarayıcı bunu çalıştırmaz (CSP'ye takılmaz),
    /// dashboard.js okuyup çizer. Json.Serialize &lt; ve &gt; karakterlerini kaçışlar, script etiketinden çıkılamaz.
    /// </summary>
    public object ChartData => new
    {
        trend = new
        {
            labels = Data.Trend.Select(p => p.Day.ToString("dd.MM")).ToArray(),
            opened = Data.Trend.Select(p => p.Opened).ToArray(),
            resolved = Data.Trend.Select(p => p.Resolved).ToArray()
        },
        priorities = new
        {
            labels = new[] { "Hemen", "Bu hafta", "Planla", "İzle" },
            values = new[] { Data.Summary.NowCount, Data.Summary.ThisWeekCount, Data.Summary.PlanCount, Data.Summary.WatchCount }
        }
    };
}
