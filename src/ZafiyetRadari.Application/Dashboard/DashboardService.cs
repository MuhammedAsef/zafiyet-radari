using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Dashboard;

public interface IDashboardService
{
    Task<Result<DashboardData>> GetAsync(int? assetGroupId, CancellationToken cancellationToken = default);
}

internal sealed class DashboardService(
    InventoryAccessGuard guard,
    IDashboardQueries queries,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<Result<DashboardData>> GetAsync(int? assetGroupId, CancellationToken cancellationToken = default)
    {
        string userId;

        if (assetGroupId is { } groupId)
        {
            // Belirli grup istendiyse üyelik kontrolü (audit dahil); sorgu ayrıca üyelikle filtreli.
            var access = await guard.RequireAsync(groupId, AssetGroupRole.Viewer, cancellationToken);
            if (access.IsFailure)
            {
                return access.Error;
            }

            userId = access.Value;
        }
        else
        {
            var user = guard.RequireUser();
            if (user.IsFailure)
            {
                return user.Error;
            }

            userId = user.Value;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var trendStart = today.AddDays(-(DashboardLimits.TrendDays - 1));

        var data = await queries.GetAsync(userId, assetGroupId, trendStart, cancellationToken);
        return data with { Trend = FillGaps(data.Trend, trendStart, today) };
    }

    /// <summary>Hiç olay olmayan günler sorgudan gelmez; grafikte boşluk olmasın diye sıfırla doldurulur.</summary>
    internal static IReadOnlyList<TrendPoint> FillGaps(IReadOnlyList<TrendPoint> points, DateOnly start, DateOnly end)
    {
        var byDay = points.ToDictionary(p => p.Day);
        var result = new List<TrendPoint>();

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            result.Add(byDay.TryGetValue(day, out var point) ? point : new TrendPoint(day, 0, 0));
        }

        return result;
    }
}
