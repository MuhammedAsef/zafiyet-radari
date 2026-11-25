using FluentValidation;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Application.Inventory;

public interface IAssetGroupService
{
    Task<Result<IReadOnlyList<AssetGroupListItem>>> ListMyGroupsAsync(CancellationToken cancellationToken = default);

    Task<Result<AssetGroupDetails>> GetAsync(int assetGroupId, CancellationToken cancellationToken = default);

    Task<Result<int>> CreateAsync(CreateAssetGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(UpdateAssetGroupCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int assetGroupId, CancellationToken cancellationToken = default);

    Task<Result> AddMemberAsync(AddGroupMemberCommand command, CancellationToken cancellationToken = default);

    Task<Result> ChangeMemberRoleAsync(ChangeGroupMemberRoleCommand command, CancellationToken cancellationToken = default);

    Task<Result> RemoveMemberAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default);
}

internal sealed class AssetGroupService(
    InventoryAccessGuard guard,
    IInventoryRepository repository,
    IInventoryQueries queries,
    IUserDirectory userDirectory,
    IUnitOfWork unitOfWork,
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    IValidator<CreateAssetGroupCommand> createValidator,
    IValidator<UpdateAssetGroupCommand> updateValidator,
    IValidator<AddGroupMemberCommand> addMemberValidator,
    IValidator<ChangeGroupMemberRoleCommand> changeRoleValidator) : IAssetGroupService
{
    public async Task<Result<IReadOnlyList<AssetGroupListItem>>> ListMyGroupsAsync(CancellationToken cancellationToken = default)
    {
        var user = guard.RequireUser();
        if (user.IsFailure)
        {
            return user.Error;
        }

        var groups = await queries.ListGroupsAsync(user.Value, cancellationToken);
        return Result.Success(groups);
    }

    public async Task<Result<AssetGroupDetails>> GetAsync(int assetGroupId, CancellationToken cancellationToken = default)
    {
        var access = await guard.RequireAsync(assetGroupId, AssetGroupRole.Viewer, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var details = await queries.GetGroupAsync(assetGroupId, access.Value, cancellationToken);
        if (details is null)
        {
            return AssetGroupErrors.NotFound;
        }

        return details;
    }

    public async Task<Result<int>> CreateAsync(CreateAssetGroupCommand command, CancellationToken cancellationToken = default)
    {
        var user = guard.RequireUser();
        if (user.IsFailure)
        {
            return user.Error;
        }

        var validation = await createValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var created = AssetGroup.Create(command.Name, command.Description, user.Value, timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        repository.AddGroup(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditEventTypes.GroupCreated, created.Value.Id, created.Value.Name, cancellationToken);
        return created.Value.Id;
    }

    public async Task<Result> UpdateAsync(UpdateAssetGroupCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await updateValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await guard.RequireAsync(command.AssetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var group = await repository.GetGroupWithMembersAsync(command.AssetGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(AssetGroupErrors.NotFound);
        }

        var result = group.Update(command.Name, command.Description);
        if (result.IsSuccess)
        {
            await SaveAsync(cancellationToken);
        }

        return result;
    }

    public async Task<Result> DeleteAsync(int assetGroupId, CancellationToken cancellationToken = default)
    {
        var access = await guard.RequireAsync(assetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        await repository.DeleteGroupAsync(assetGroupId, cancellationToken);
        await AuditAsync(AuditEventTypes.GroupDeleted, assetGroupId, null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> AddMemberAsync(AddGroupMemberCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await addMemberValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await guard.RequireAsync(command.AssetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var userId = await userDirectory.FindUserIdByEmailAsync(command.Email.Trim(), cancellationToken);
        if (userId is null)
        {
            return Result.Failure(AssetGroupErrors.UserNotFound);
        }

        var group = await repository.GetGroupWithMembersAsync(command.AssetGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(AssetGroupErrors.NotFound);
        }

        var result = group.AddMember(userId, command.Role, timeProvider.GetUtcNow());
        if (result.IsSuccess)
        {
            await SaveAsync(cancellationToken);
            await AuditAsync(AuditEventTypes.GroupMemberAdded, command.AssetGroupId, $"Üye {userId}, rol {command.Role}", cancellationToken);
        }

        return result;
    }

    public async Task<Result> ChangeMemberRoleAsync(ChangeGroupMemberRoleCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await changeRoleValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await guard.RequireAsync(command.AssetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var group = await repository.GetGroupWithMembersAsync(command.AssetGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(AssetGroupErrors.NotFound);
        }

        var result = group.ChangeMemberRole(command.UserId, command.Role);
        if (result.IsSuccess)
        {
            await SaveAsync(cancellationToken);
            await AuditAsync(AuditEventTypes.GroupMemberRoleChanged, command.AssetGroupId, $"Üye {command.UserId}, yeni rol {command.Role}", cancellationToken);
        }

        return result;
    }

    public async Task<Result> RemoveMemberAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default)
    {
        var access = await guard.RequireAsync(assetGroupId, AssetGroupRole.Owner, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var group = await repository.GetGroupWithMembersAsync(assetGroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(AssetGroupErrors.NotFound);
        }

        var result = group.RemoveMember(userId);
        if (result.IsSuccess)
        {
            await SaveAsync(cancellationToken);
            await AuditAsync(AuditEventTypes.GroupMemberRemoved, assetGroupId, $"Üye {userId}", cancellationToken);
        }

        return result;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task AuditAsync(string eventType, int assetGroupId, string? details, CancellationToken cancellationToken)
    {
        var user = guard.CurrentUser;
        return auditTrail.AppendAsync(
            new AuditEvent(
                eventType,
                user.UserId,
                user.UserName,
                user.IpAddress,
                TargetType: "AssetGroup",
                TargetId: assetGroupId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Details: details),
            cancellationToken);
    }
}
