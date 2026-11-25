using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Web.Areas.Inventory.Models;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.Web.Areas.Inventory.Controllers;

public sealed class AssetsController(
    IAssetService assets,
    IAssetGroupService groups,
    IInventoryUploadService uploads) : InventoryControllerBase
{
    // Multipart başlıkları ve form alanları için dosya sınırının üstüne küçük pay.
    private const int MaxRequestBytes = InventoryLimits.MaxUploadBytes + (64 * 1024);

    [HttpGet]
    public async Task<IActionResult> Details(
        int id, string? search, bool inactive = false, int page = 1, CancellationToken cancellationToken = default)
    {
        var asset = await assets.GetAsync(id, cancellationToken);
        if (asset.IsFailure)
        {
            return AccessFailure(asset.Error) ?? NotFound();
        }

        var components = await assets.ListComponentsAsync(id, new ComponentFilter(search, inactive, page), cancellationToken);
        if (components.IsFailure)
        {
            return AccessFailure(components.Error) ?? NotFound();
        }

        return View(new AssetDetailsViewModel
        {
            Asset = asset.Value,
            Components = components.Value,
            Search = search,
            IncludeInactive = inactive
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int groupId, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(groupId, cancellationToken);
        if (group.IsFailure)
        {
            return AccessFailure(group.Error) ?? NotFound();
        }

        if (group.Value.MyRole == AssetGroupRole.Viewer)
        {
            return Forbid();
        }

        return View("Form", new AssetFormModel { AssetGroupId = groupId, AssetGroupName = group.Value.Name });
    }

    [HttpPost]
    public async Task<IActionResult> Create(int groupId, AssetFormModel model, CancellationToken cancellationToken)
    {
        model.AssetGroupId = groupId;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await assets.CreateAsync(
            new CreateAssetCommand(groupId, model.Name, model.Environment, model.Criticality, model.IsInternetFacing), cancellationToken);

        if (result.IsFailure)
        {
            var failure = AccessFailure(result.Error);
            if (failure is not null)
            {
                return failure;
            }

            AddError(result.Error);
            return View("Form", model);
        }

        FlashSuccess("Varlık oluşturuldu. Şimdi bileşenlerini ekleyebilir veya bir lock/SBOM dosyası yükleyebilirsin.");
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var asset = await assets.GetAsync(id, cancellationToken);
        if (asset.IsFailure)
        {
            return AccessFailure(asset.Error) ?? NotFound();
        }

        var value = asset.Value;
        if (value.MyRole == AssetGroupRole.Viewer)
        {
            return Forbid();
        }

        return View("Form", new AssetFormModel
        {
            Id = value.Id,
            AssetGroupId = value.AssetGroupId,
            AssetGroupName = value.AssetGroupName,
            Name = value.Name,
            Environment = value.Environment,
            Criticality = value.Criticality,
            IsInternetFacing = value.IsInternetFacing
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AssetFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await assets.UpdateAsync(
            new UpdateAssetCommand(id, model.Name, model.Environment, model.Criticality, model.IsInternetFacing), cancellationToken);

        if (result.IsFailure)
        {
            var failure = AccessFailure(result.Error);
            if (failure is not null)
            {
                return failure;
            }

            AddError(result.Error);
            return View("Form", model);
        }

        FlashSuccess("Varlık güncellendi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var asset = await assets.GetAsync(id, cancellationToken);
        if (asset.IsFailure)
        {
            return AccessFailure(asset.Error) ?? NotFound();
        }

        return asset.Value.MyRole == AssetGroupRole.Viewer ? Forbid() : (IActionResult)View(asset.Value);
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        // Grubu silmeden önce al: silindikten sonra yönlendirecek yer kalmaz.
        var asset = await assets.GetAsync(id, cancellationToken);
        if (asset.IsFailure)
        {
            return AccessFailure(asset.Error) ?? NotFound();
        }

        var result = await assets.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        FlashSuccess($"\"{asset.Value.Name}\" silindi.");
        return RedirectToAction(nameof(GroupsController.Details), "Groups", new { id = asset.Value.AssetGroupId });
    }

    [HttpPost]
    public async Task<IActionResult> AddComponent(int id, [Bind(Prefix = "NewComponent")] AddComponentFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Ekosistem, paket adı ve sürüm zorunlu.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await assets.AddComponentAsync(new AddComponentCommand(id, model.Ecosystem, model.Name, model.Version), cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? Flash(result.Error.Message, id);
        }

        FlashSuccess($"{model.Name}@{model.Version} eklendi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateComponent(int id, long componentId, CancellationToken cancellationToken)
    {
        var result = await assets.DeactivateComponentAsync(id, componentId, cancellationToken);
        return RedirectWithResult(result, "Bileşen pasife alındı. Geçmiş bulguları korunur.", nameof(Details), new { id });
    }

    /// <summary>
    /// Dosya yükleme. Sınırlar istek seviyesinde de uygulanır (servis ayrıca okuma sınırı koyar).
    /// MemoryBufferThreshold dosya sınırına eşit: ASP.NET dosyayı geçici diske yazmaz, bellekte tutar.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.FileUpload)]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes, MemoryBufferThreshold = MaxRequestBytes, ValueCountLimit = 16)]
    public async Task<IActionResult> Upload(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Flash("Yüklenecek bir dosya seç.", id);
        }

        if (file.Length > InventoryLimits.MaxUploadBytes)
        {
            return Flash($"Dosya en fazla {InventoryLimits.MaxUploadMegabytes} MB olabilir.", id);
        }

        await using var stream = file.OpenReadStream();
        var result = await uploads.UploadAsync(new UploadInventoryCommand(id, file.FileName, stream), cancellationToken);

        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? Flash(result.Error.Message, id);
        }

        var summary = result.Value;
        FlashSuccess(
            $"{InventoryLabels.Of(summary.Format)} işlendi: {summary.Added} yeni, {summary.Unchanged} değişmedi, " +
            $"{summary.Reactivated} yeniden aktif, {summary.Deactivated} pasife alındı" +
            (summary.Skipped > 0 ? $", {summary.Skipped} girdi atlandı (desteklenmeyen kaynak veya geçersiz ad/sürüm)." : "."));

        return RedirectToAction(nameof(Details), new { id });
    }

    private RedirectToActionResult Flash(string error, int assetId)
    {
        FlashError(error);
        return RedirectToAction(nameof(Details), new { id = assetId });
    }
}
