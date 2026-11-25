using Microsoft.Extensions.DependencyInjection;
using ZafiyetRadari.Application.Notifications;

namespace ZafiyetRadari.Application.Findings;

public static class FindingModule
{
    public static IServiceCollection AddFindingsModule(this IServiceCollection services)
    {
        services.AddScoped<IFindingService, FindingService>();
        services.AddScoped<IFindingNotifier, FindingNotifier>();
        services.AddScoped<INotificationChannelService, NotificationChannelService>();
        return services;
    }
}
