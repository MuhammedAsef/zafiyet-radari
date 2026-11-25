using FluentValidation;
using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Application.Findings;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Application.Matching;
using Microsoft.Extensions.DependencyInjection;
using ZafiyetRadari.Domain.Risk;
using ZafiyetRadari.Domain.Versioning;

namespace ZafiyetRadari.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(AssemblyMarker).Assembly, includeInternalTypes: true);

        // Domain çekirdeği: durumsuz, thread-safe stratejiler.
        // Yeni ekosistem = yeni IVersionComparer kaydı; tüketiciler IEnumerable<IVersionComparer> alır.
        services.AddSingleton<IVersionComparer, SemVerComparer>();
        services.AddSingleton<IRiskScorer, DefaultRiskScorer>();

        services.AddInventoryModule();
        services.AddScoped<IAssetMatchingService, AssetMatchingService>();

        services.AddFindingsModule();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
