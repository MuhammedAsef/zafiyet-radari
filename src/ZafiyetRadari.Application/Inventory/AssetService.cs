using FluentValidation;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Application.Inventory;

public interface IAssetService
{
    Task<Result<AssetDetails>> GetAsync(int assetId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ComponentListItem>>> ListComponentsAsync(int assetId, ComponentFilter filter, CancellationToken cancellationToken = default);

    Task<Result<int>> CreateAsync(CreateAssetCommand command, CancellationToken cancellationToken = default);

    Task<Result> UpdateAsync(UpdateAssetCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int assetId, CancellationToken cancellationToken = default);

    Task<Result<long>> AddComponentAsync(AddComponentCommand command, CancellationToken cancellationToken = default);

    Task<Result> DeactivateComponentAsync(int assetId, long componentId, CancellationToken cancellationToken = default);
}

internal sealed class AssetService(
    InventoryAccessGuard guard,
    IInventoryRepository repository,
    IInventoryQueries queries,
    IUnitOfWork unitOfWork,
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    IValidator<CreateAssetCommand> createValidator,
    IValidator<UpdateAssetCommand> updateValidator,
    IValidator<AddComponentCommand> addComponentValidator,
    IMatchingScheduler matchingScheduler) : IAssetService
{
    public async Task<Result<AssetDetails>> GetAsync(int assetId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAssetAccessAsync(assetId, AssetGroupRole.Viewer, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var details = await queries.GetAssetAsync(assetId, access.Value, cancellationToken);
        if (details is null)
        {
            return AssetErrors.NotFound;
        }

        return details;
    }

    public async Task<Result<PagedResult<ComponentListItem>>> ListComponentsAsync(
        int assetId, ComponentFilter filter, CancellationToken cancellationToken = default)
    {
        var access = await RequireAssetAccessAsync(assetId, AssetGroupRole.Viewer, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        // Sayfalama değerleri kullanıcıdan gelir; sınırlar burada zorlanır (büyük PageSize ile DoS yok).
        var safeFilter = filter with
        {
            Search = string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : filter.Search.Trim()[..Math.Min(filter.Search.Trim().Length, ComponentFilter.MaxSearchLength)],
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 1, ComponentFilter.MaxPageSize)
        };

        return await queries.ListComponentsAsync(assetId, access.Value, safeFilter, cancellationToken);
    }

    public async Task<Result<int>> CreateAsync(CreateAssetCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var access = await guard.RequireAsync(command.AssetGroupId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var created = Asset.Create(
            command.AssetGroupId, command.Name, command.Environment, command.Criticality, command.IsInternetFacing,
            timeProvider.GetUtcNow());
        if (created.IsFailure)
        {
            return created.Error;
        }

        if (await repository.AssetNameExistsAsync(command.AssetGroupId, created.Value.Name, excludingAssetId: null, cancellationToken))
        {
            return AssetErrors.NameTaken;
        }

        repository.AddAsset(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Value.Id;
    }

    public async Task<Result> UpdateAsync(UpdateAssetCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await updateValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await RequireAssetAccessAsync(command.AssetId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var asset = await repository.GetAssetAsync(command.AssetId, cancellationToken);
        if (asset is null)
        {
            return Result.Failure(AssetErrors.NotFound);
        }

        var result = asset.Update(command.Name, command.Environment, command.Criticality, command.IsInternetFacing, timeProvider.GetUtcNow());
        if (result.IsFailure)
        {
            return result;
        }

        if (await repository.AssetNameExistsAsync(asset.AssetGroupId, asset.Name, asset.Id, cancellationToken))
        {
            return Result.Failure(AssetErrors.NameTaken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Ortam/kritiklik/internete açıklık risk skorunu değiştirir.
        matchingScheduler.ScheduleAssetMatching(asset.Id);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int assetId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAssetAccessAsync(assetId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        await repository.DeleteAssetAsync(assetId, cancellationToken);

        var user = guard.CurrentUser;
        await auditTrail.AppendAsync(
            new AuditEvent(
                AuditEventTypes.AssetDeleted, user.UserId, user.UserName, user.IpAddress,
                TargetType: "Asset", TargetId: assetId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<long>> AddComponentAsync(AddComponentCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await addComponentValidator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var coordinate = PackageCoordinate.Create(command.Ecosystem, command.Name, command.Version);
        if (coordinate.IsFailure)
        {
            return coordinate.Error;
        }

        var access = await RequireAssetAccessAsync(command.AssetId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var asset = await repository.GetAssetWithComponentsAsync(command.AssetId, cancellationToken);
        if (asset is null)
        {
            return AssetErrors.NotFound;
        }

        var added = asset.AddManualComponent(coordinate.Value, timeProvider.GetUtcNow());
        if (added.IsFailure)
        {
            return added.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        matchingScheduler.ScheduleAssetMatching(command.AssetId);
        return added.Value.Id;
    }

    public async Task<Result> DeactivateComponentAsync(int assetId, long componentId, CancellationToken cancellationToken = default)
    {
        var access = await RequireAssetAccessAsync(assetId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var asset = await repository.GetAssetWithComponentsAsync(assetId, cancellationToken);
        if (asset is null)
        {
            return Result.Failure(AssetErrors.NotFound);
        }

        // Bileşen kimliği URL'den gelir; sadece bu varlığın bileşenleri arasında aranır (başka varlığın bileşeni kapatılamaz).
        var result = asset.DeactivateComponent(componentId, timeProvider.GetUtcNow());
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            matchingScheduler.ScheduleAssetMatching(assetId);
        }

        return result;
    }

    /// <summary>Varlık kimliği → grup → üyelik kontrolü. Varlık yoksa da "bulunamadı" (aynı yanıt, bilgi sızmaz).</summary>
    private async Task<Result<string>> RequireAssetAccessAsync(int assetId, AssetGroupRole minimumRole, CancellationToken cancellationToken)
    {
        var groupId = await repository.GetAssetGroupIdAsync(assetId, cancellationToken);
        if (groupId is null)
        {
            return AssetErrors.NotFound;
        }

        var access = await guard.RequireAsync(groupId.Value, minimumRole, cancellationToken);
        return access.IsFailure && access.Error == AssetGroupErrors.NotFound ? AssetErrors.NotFound : access;
    }
}
