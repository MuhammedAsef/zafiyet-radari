using Microsoft.AspNetCore.Mvc.Rendering;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Web.Areas.Inventory;

/// <summary>Enum değerlerinin arayüz metinleri ve seçim listeleri.</summary>
public static class InventoryLabels
{
    public static string Of(AssetGroupRole role) => role switch
    {
        AssetGroupRole.Owner => "Sahip",
        AssetGroupRole.Editor => "Düzenleyici",
        AssetGroupRole.Viewer => "Görüntüleyici",
        _ => role.ToString()
    };

    public static string Of(AssetEnvironment environment) => environment switch
    {
        AssetEnvironment.Development => "Geliştirme",
        AssetEnvironment.Test => "Test",
        AssetEnvironment.Production => "Üretim",
        _ => environment.ToString()
    };

    public static string Of(AssetCriticality criticality) => criticality switch
    {
        AssetCriticality.Low => "Düşük",
        AssetCriticality.Medium => "Orta",
        AssetCriticality.High => "Yüksek",
        AssetCriticality.Critical => "Kritik",
        _ => criticality.ToString()
    };

    public static string Of(UploadFormat format) => format switch
    {
        UploadFormat.CycloneDx => "CycloneDX SBOM",
        UploadFormat.NpmLock => "package-lock.json",
        UploadFormat.NuGetLock => "packages.lock.json",
        _ => format.ToString()
    };

    public static string Of(ComponentSource source) => source switch
    {
        ComponentSource.Manual => "Elle",
        ComponentSource.Upload => "Dosya",
        _ => source.ToString()
    };

    public static string CriticalityBadge(AssetCriticality criticality) => criticality switch
    {
        AssetCriticality.Critical => "text-bg-danger",
        AssetCriticality.High => "text-bg-warning",
        AssetCriticality.Medium => "text-bg-info",
        _ => "text-bg-secondary"
    };

    public static IEnumerable<SelectListItem> Roles(AssetGroupRole? selected = null) =>
        Enum.GetValues<AssetGroupRole>().Select(r => new SelectListItem(Of(r), r.ToString(), r == selected));

    public static IEnumerable<SelectListItem> Environments() =>
        Enum.GetValues<AssetEnvironment>().Select(e => new SelectListItem(Of(e), e.ToString()));

    public static IEnumerable<SelectListItem> Criticalities() =>
        Enum.GetValues<AssetCriticality>().Select(c => new SelectListItem(Of(c), c.ToString()));

    public static IEnumerable<SelectListItem> EcosystemOptions() =>
        Ecosystems.All.Select(e => new SelectListItem(e, e));

    public static string FileSize(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB"
    };
}
