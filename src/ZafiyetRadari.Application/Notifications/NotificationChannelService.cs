using FluentValidation;
using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Application.Notifications;

public interface INotificationChannelService
{
    /// <summary>Kanallar tüm üyelere görünür; alıcı seçenekleri sadece sahibe döner.</summary>
    Task<Result<GroupNotificationSettings>> GetSettingsAsync(int assetGroupId, CancellationToken cancellationToken = default);

    Task<Result> CreateEmailChannelAsync(CreateEmailChannelCommand command, CancellationToken cancellationToken = default);

    Task<Result> SetEnabledAsync(int assetGroupId, int channelId, bool enabled, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int assetGroupId, int channelId, CancellationToken cancellationToken = default);

    Task<Result> SendTestAsync(int assetGroupId, int channelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Kanal yönetimi sadece grup sahibine açık. Alıcı serbest metin değil: grubun e-postası doğrulanmış üyelerinden seçilir.
/// Böylece uygulama, rastgele adreslere mail gönderen bir araç olarak kötüye kullanılamaz.
/// </summary>
internal sealed class NotificationChannelService(
    InventoryAccessGuard guard,
    IInventoryQueries inventoryQueries,
    INotificationStore store,
    IUserDirectory userDirectory,
    IEmailSender emailSender,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<CreateEmailChannelCommand> validator,
    ILogger<NotificationChannelService> logger) : INotificationChannelService
{
    public async Task<Result<GroupNotificationSettings>> GetSettingsAsync(int assetGroupId, CancellationToken cancellationToken = default)
    {
        var access = await guard.RequireAsync(assetGroupId, AssetGroupRole.Viewer, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var channels = await store.ListChannelsAsync(assetGroupId, cancellationToken);

        var group = await inventoryQueries.GetGroupAsync(assetGroupId, access.Value, cancellationToken);
        IReadOnlyList<RecipientOption> recipients = [];

        if (group?.MyRole == AssetGroupRole.Owner)
        {
            var emails = await userDirectory.GetConfirmedEmailsAsync(group.Members.Select(m => m.UserId).ToList(), cancellationToken);
            recipients = emails.Select(e => new RecipientOption(e.Key, e.Value)).OrderBy(r => r.Email, StringComparer.OrdinalIgnoreCase).ToList();
        }

        return new GroupNotificationSettings(channels, recipients);
    }

    public async Task<Result> CreateEmailChannelAsync(CreateEmailChannelCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await guard.RequireAsync(command.AssetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        // Alıcı kimliği formdan gelir: grubun üyesi olduğu ve e-postasının doğrulandığı sunucuda tekrar kontrol edilir.
        var group = await inventoryQueries.GetGroupAsync(command.AssetGroupId, access.Value, cancellationToken);
        if (group is null || group.Members.All(m => m.UserId != command.RecipientUserId))
        {
            return Result.Failure(NotificationErrors.InvalidRecipient);
        }

        var emails = await userDirectory.GetConfirmedEmailsAsync([command.RecipientUserId], cancellationToken);
        if (!emails.TryGetValue(command.RecipientUserId, out var email))
        {
            return Result.Failure(NotificationErrors.InvalidRecipient);
        }

        var channel = NotificationChannel.CreateEmail(
            command.AssetGroupId, command.Name, email, command.MinPriority, command.OnlyKev, timeProvider.GetUtcNow());
        if (channel.IsFailure)
        {
            return Result.Failure(channel.Error);
        }

        store.AddChannel(channel.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetEnabledAsync(int assetGroupId, int channelId, bool enabled, CancellationToken cancellationToken = default)
    {
        var channel = await LoadOwnedChannelAsync(assetGroupId, channelId, cancellationToken);
        if (channel.IsFailure)
        {
            return Result.Failure(channel.Error);
        }

        channel.Value.SetEnabled(enabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int assetGroupId, int channelId, CancellationToken cancellationToken = default)
    {
        var channel = await LoadOwnedChannelAsync(assetGroupId, channelId, cancellationToken);
        if (channel.IsFailure)
        {
            return Result.Failure(channel.Error);
        }

        store.RemoveChannel(channel.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SendTestAsync(int assetGroupId, int channelId, CancellationToken cancellationToken = default)
    {
        var channel = await LoadOwnedChannelAsync(assetGroupId, channelId, cancellationToken);
        if (channel.IsFailure)
        {
            return Result.Failure(channel.Error);
        }

        var info = await store.GetDispatchInfoAsync(channelId, cancellationToken);
        if (info is null || !await store.IsValidRecipientAsync(assetGroupId, info.Target, cancellationToken))
        {
            return Result.Failure(NotificationErrors.InvalidRecipient);
        }

        try
        {
            await emailSender.SendAsync(FindingEmails.Test(info.Target, info.AssetGroupName, channel.Value.Name, links, assetGroupId), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Test maili gönderilemedi. Kanal {ChannelId}", channelId);
            return Result.Failure(NotificationErrors.SendFailed);
        }

        return Result.Success();
    }

    /// <summary>Kanal kimliği formdan gelir: hem sahiplik hem kanalın gerçekten bu gruba ait olduğu kontrol edilir (IDOR).</summary>
    private async Task<Result<NotificationChannel>> LoadOwnedChannelAsync(int assetGroupId, int channelId, CancellationToken cancellationToken)
    {
        var access = await guard.RequireAsync(assetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var channel = await store.GetChannelAsync(channelId, cancellationToken);
        if (channel is null || channel.AssetGroupId != assetGroupId)
        {
            return NotificationErrors.NotFound;
        }

        return channel;
    }
}

internal sealed class CreateEmailChannelCommandValidator : AbstractValidator<CreateEmailChannelCommand>
{
    public CreateEmailChannelCommandValidator()
    {
        RuleFor(x => x.AssetGroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kanal adı zorunlu.")
            .MaximumLength(NotificationLimits.MaxChannelNameLength).WithMessage($"Kanal adı en fazla {NotificationLimits.MaxChannelNameLength} karakter olabilir.");
        RuleFor(x => x.RecipientUserId).NotEmpty().WithMessage("Alıcı seç.").MaximumLength(450);
        RuleFor(x => x.MinPriority).IsInEnum().WithMessage("Geçersiz öncelik.");
    }
}
