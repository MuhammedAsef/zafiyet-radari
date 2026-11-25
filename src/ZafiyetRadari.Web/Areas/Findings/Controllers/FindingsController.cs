using Microsoft.AspNetCore.Mvc;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Findings;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Web.Controllers;
using ZafiyetRadari.Web.Reporting;

namespace ZafiyetRadari.Web.Areas.Findings.Controllers;

[Area("Findings")]
public sealed class FindingsController(IFindingService findings, IAssetGroupService groups, IAppLinks links) : AppControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(
        FindingPriority? priority,
        FindingStatus? status,
        bool closed = false,
        int? groupId = null,
        int? assetId = null,
        string? search = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var filter = new FindingFilter(priority, status, closed, groupId, assetId, search, page);

        var result = await findings.ListAsync(filter, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? Forbid();
        }

        var myGroups = await groups.ListMyGroupsAsync(cancellationToken);

        return View(new FindingIndexViewModel
        {
            Findings = result.Value,
            Filter = filter,
            Groups = myGroups.IsSuccess ? myGroups.Value : []
        });
    }

    /// <summary>Listedeki filtrelerle CSV. GET: tarayıcı indirmesi; veri değiştirmez, audit'e yazılır.</summary>
    [HttpGet]
    public async Task<IActionResult> Export(
        FindingPriority? priority,
        FindingStatus? status,
        bool closed = false,
        int? groupId = null,
        int? assetId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await findings.ExportAsync(new FindingFilter(priority, status, closed, groupId, assetId, search), cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? Forbid();
        }

        var csv = new CsvBuilder()
            .AddRow("Öncelik", "Risk", "Durum", "Zafiyet", "CVE", "KEV", "Ekosistem", "Paket", "Sürüm", "Varlık", "Grup", "İlk görülme", "Özet", "Bağlantı");

        foreach (var item in result.Value)
        {
            csv.AddRow(
                FindingLabels.Of(item.Priority),
                item.RiskScore,
                FindingLabels.Of(item.Status),
                item.ExternalId,
                item.CveId,
                item.IsKev ? "Evet" : "Hayır",
                item.Ecosystem,
                item.PackageName,
                new CsvText(item.Version),
                item.AssetName,
                item.AssetGroupName,
                item.FirstSeenAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                item.Summary,
                links.FindingDetails(item.Id)); // taban adres ayardan; Host başlığından türetilmez
        }

        var fileName = $"bulgular-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(csv.ToBytes(), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var result = await findings.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        return View(new FindingDetailsViewModel { Finding = result.Value });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(long id, [Bind(Prefix = "Form")] ChangeStatusFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Form geçersiz: açıklama en fazla 1000 karakter olabilir.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await findings.ChangeStatusAsync(
            new ChangeFindingStatusCommand(id, model.NewStatus, model.Comment, model.RiskAcceptedUntil), cancellationToken);

        return RedirectWithResult(result, $"Durum \"{FindingLabels.Of(model.NewStatus)}\" olarak güncellendi.", nameof(Details), new { id });
    }
}
