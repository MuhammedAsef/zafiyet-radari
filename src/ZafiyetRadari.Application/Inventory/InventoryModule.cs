using Microsoft.Extensions.DependencyInjection;

namespace ZafiyetRadari.Application.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddScoped<InventoryAccessGuard>();
        services.AddScoped<IAssetGroupService, AssetGroupService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IInventoryUploadService, InventoryUploadService>();
        return services;
    }
}
