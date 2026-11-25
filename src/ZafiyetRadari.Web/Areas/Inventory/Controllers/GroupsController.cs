using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Web.Areas.Inventory.Models;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.Web.Areas.Inventory.Controllers;

public sealed class GroupsController(IAssetGroupService groups, INotificationChannelService notifications) : InventoryControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await groups.ListMyGroupsAsync(cancellationToken);
        return result.IsSuccess ? View(result.Value) : AccessFailure(result.Error) ?? Forbid();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var result = await groups.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        var settings = await notifications.GetSettingsAsync(id, cancellationToken);

        return View(new GroupDetailsViewModel
        {
            Group = result.Value,
            Notifications = settings.IsSuccess ? settings.Value : null
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new GroupFormModel());

    [HttpPost]
    public async Task<IActionResult> Create(GroupFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await groups.CreateAsync(new CreateAssetGroupCommand(model.Name, model.Description), cancellationToken);
        if (result.IsFailure)
        {
            AddError(result.Error);
            return View("Form", model);
        }

        FlashSuccess("Grup oluşturuldu. Sahibi sensin.");
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await groups.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        if (result.Value.MyRole != AssetGroupRole.Owner)
        {
            return Forbid();
        }

        return View("Form", new GroupFormModel { Id = id, Name = result.Value.Name, Description = result.Value.Description });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, GroupFormModel model, CancellationToken cancellationToken)
    {
        model.Id = id; // kimlik route'tan gelir; form alanına güvenilmez
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var result = await groups.UpdateAsync(new UpdateAssetGroupCommand(id, model.Name, model.Description), cancellationToken);
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

        FlashSuccess("Grup güncellendi.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await groups.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        return result.Value.MyRole == AssetGroupRole.Owner ? (IActionResult)View(result.Value) : Forbid();
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var result = await groups.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return AccessFailure(result.Error) ?? NotFound();
        }

        FlashSuccess("Grup ve içindeki tüm varlıklar silindi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(int id, [Bind(Prefix = "NewMember")] AddMemberFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Geçerli bir e-posta ve rol seç.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await groups.AddMemberAsync(new AddGroupMemberCommand(id, model.Email, model.Role), cancellationToken);
        return RedirectWithResult(result, $"{model.Email} gruba eklendi.", nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeRole(int id, string userId, AssetGroupRole role, CancellationToken cancellationToken)
    {
        var result = await groups.ChangeMemberRoleAsync(new ChangeGroupMemberRoleCommand(id, userId ?? string.Empty, role), cancellationToken);
        return RedirectWithResult(result, "Üyenin rolü güncellendi.", nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveMember(int id, string userId, CancellationToken cancellationToken)
    {
        var result = await groups.RemoveMemberAsync(id, userId ?? string.Empty, cancellationToken);
        return RedirectWithResult(result, "Üye gruptan çıkarıldı.", nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AddChannel(int id, [Bind(Prefix = "NewChannel")] AddChannelFormModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            FlashError("Kanal adı ve alıcı zorunlu.");
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await notifications.CreateEmailChannelAsync(
            new CreateEmailChannelCommand(id, model.Name, model.RecipientUserId, model.MinPriority, model.OnlyKev), cancellationToken);
        return RedirectWithResult(result, "Bildirim kanalı eklendi.", nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> ToggleChannel(int id, int channelId, bool enabled, CancellationToken cancellationToken)
    {
        var result = await notifications.SetEnabledAsync(id, channelId, enabled, cancellationToken);
        return RedirectWithResult(result, enabled ? "Kanal açıldı." : "Kanal kapatıldı.", nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteChannel(int id, int channelId, CancellationToken cancellationToken)
    {
        var result = await notifications.DeleteAsync(id, channelId, cancellationToken);
        return RedirectWithResult(result, "Kanal silindi.", nameof(Details), new { id });
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> TestChannel(int id, int channelId, CancellationToken cancellationToken)
    {
        var result = await notifications.SendTestAsync(id, channelId, cancellationToken);
        return RedirectWithResult(result, "Test maili gönderildi.", nameof(Details), new { id });
    }
}
