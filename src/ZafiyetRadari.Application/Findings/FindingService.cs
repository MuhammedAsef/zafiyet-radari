using System.Globalization;
using FluentValidation;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Domain.Matching;
using ZafiyetRadari.Domain.Risk;

namespace ZafiyetRadari.Application.Findings;

public interface IFindingService
{
    Task<Result<PagedResult<FindingListItem>>> ListAsync(FindingFilter filter, CancellationToken cancellationToken = default);

    Task<Result<FindingDetails>> GetAsync(long findingId, CancellationToken cancellationToken = default);

    Task<Result> ChangeStatusAsync(ChangeFindingStatusCommand command, CancellationToken cancellationToken = default);

    /// <summary>Filtreye uyan bulgular, sayfalama olmadan (en fazla <see cref="FindingLimits.MaxExportRows"/>). Dışa aktarma audit'e yazılır.</summary>
    Task<Result<IReadOnlyList<FindingListItem>>> ExportAsync(FindingFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Bulgular, varlık grubunun yetki kurallarını kullanır (InventoryAccessGuard): görüntülemek için üyelik,
/// durum değiştirmek için Düzenleyici veya Sahip rolü. Risk kabulü ve yanlış alarm kararları audit log'a ayrıca yazılır.
/// </summary>
internal sealed class FindingService(
    InventoryAccessGuard guard,
    IFindingQueries queries,
    IFindingStore store,
    IUnitOfWork unitOfWork,
    IAuditTrail auditTrail,
    IRiskScorer riskScorer,
    TimeProvider timeProvider,
    IValidator<ChangeFindingStatusCommand> validator) : IFindingService
{
    public async Task<Result<PagedResult<FindingListItem>>> ListAsync(FindingFilter filter, CancellationToken cancellationToken = default)
    {
        var user = guard.RequireUser();
        if (user.IsFailure)
        {
            return user.Error;
        }

        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var safeFilter = filter with
        {
            Search = search is null ? null : search[..Math.Min(search.Length, FindingFilter.MaxSearchLength)],
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 1, FindingFilter.MaxPageSize)
        };

        return await queries.ListAsync(user.Value, safeFilter, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<FindingListItem>>> ExportAsync(FindingFilter filter, CancellationToken cancellationToken = default)
    {
        var user = guard.RequireUser();
        if (user.IsFailure)
        {
            return user.Error;
        }

        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var exportFilter = filter with
        {
            Search = search is null ? null : search[..Math.Min(search.Length, FindingFilter.MaxSearchLength)],
            Page = 1,
            PageSize = FindingLimits.MaxExportRows
        };

        var page = await queries.ListAsync(user.Value, exportFilter, cancellationToken);

        // Veri dışarı çıkıyor: kim, hangi filtreyle, kaç satır aldı kayıt altında.
        var current = guard.CurrentUser;
        await auditTrail.AppendAsync(
            new AuditEvent(
                AuditEventTypes.FindingsExported, current.UserId, current.UserName, current.IpAddress,
                TargetType: "Finding",
                Details: string.Create(CultureInfo.InvariantCulture,
                    $"{page.Items.Count} satır; grup={filter.AssetGroupId}, varlık={filter.AssetId}, öncelik={filter.Priority}, durum={filter.Status}, kapalılar={filter.IncludeClosed}")),
            cancellationToken);

        return Result.Success(page.Items);
    }

    public async Task<Result<FindingDetails>> GetAsync(long findingId, CancellationToken cancellationToken = default)
    {
        var access = await RequireFindingAccessAsync(findingId, AssetGroupRole.Viewer, cancellationToken);
        if (access.IsFailure)
        {
            return access.Error;
        }

        var details = await queries.GetAsync(findingId, access.Value, cancellationToken);
        if (details is null)
        {
            return FindingErrors.NotFound;
        }

        var risk = riskScorer.Assess(new RiskInput(
            details.CvssScore,
            details.EpssScore,
            details.IsKev,
            details.KevRansomware,
            details.AssetCriticality,
            details.AssetEnvironment,
            details.AssetIsInternetFacing,
            details.Confidence,
            details.IsMaliciousPackage));

        return details with
        {
            RiskFactors = risk.Factors,
            RecommendedVersion = FixVersionAdvisor.Recommend(details.Version, details.FixedVersions)
        };
    }

    public async Task<Result> ChangeStatusAsync(ChangeFindingStatusCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateToResultAsync(command, cancellationToken);
        if (validation.IsFailure)
        {
            return validation;
        }

        var access = await RequireFindingAccessAsync(command.FindingId, AssetGroupRole.Editor, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.Error);
        }

        var finding = await store.GetAsync(command.FindingId, cancellationToken);
        if (finding is null)
        {
            return Result.Failure(FindingErrors.NotFound);
        }

        var previous = finding.CurrentStatus;
        var result = finding.ChangeStatus(
            command.NewStatus, access.Value, command.Comment, command.RiskAcceptedUntil, timeProvider.GetUtcNow());

        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var eventType = command.NewStatus switch
        {
            FindingStatus.RiskAccepted => AuditEventTypes.FindingRiskAccepted,
            FindingStatus.FalsePositive => AuditEventTypes.FindingFalsePositive,
            _ => AuditEventTypes.FindingStatusChanged
        };

        var details = command.RiskAcceptedUntil is { } until && command.NewStatus == FindingStatus.RiskAccepted
            ? string.Create(CultureInfo.InvariantCulture, $"{previous} → {command.NewStatus}, bitiş {until:yyyy-MM-dd}: {command.Comment}")
            : $"{previous} → {command.NewStatus}: {command.Comment}";

        var user = guard.CurrentUser;
        await auditTrail.AppendAsync(
            new AuditEvent(
                eventType, user.UserId, user.UserName, user.IpAddress,
                TargetType: "Finding",
                TargetId: command.FindingId.ToString(CultureInfo.InvariantCulture),
                Details: details.Length > 500 ? details[..500] : details),
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result<string>> RequireFindingAccessAsync(long findingId, AssetGroupRole minimumRole, CancellationToken cancellationToken)
    {
        var groupId = await queries.GetAssetGroupIdAsync(findingId, cancellationToken);
        if (groupId is null)
        {
            return FindingErrors.NotFound;
        }

        var access = await guard.RequireAsync(groupId.Value, minimumRole, cancellationToken);
        return access.IsFailure && access.Error == AssetGroupErrors.NotFound ? FindingErrors.NotFound : access;
    }
}

internal sealed class ChangeFindingStatusCommandValidator : AbstractValidator<ChangeFindingStatusCommand>
{
    public ChangeFindingStatusCommandValidator()
    {
        RuleFor(x => x.FindingId).GreaterThan(0);
        RuleFor(x => x.NewStatus).IsInEnum().WithMessage("Geçersiz durum.");
        RuleFor(x => x.Comment).MaximumLength(FindingLimits.MaxCommentLength)
            .WithMessage($"Açıklama en fazla {FindingLimits.MaxCommentLength} karakter olabilir.");
    }
}
