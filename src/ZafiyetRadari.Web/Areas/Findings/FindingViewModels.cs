using System.ComponentModel.DataAnnotations;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Findings;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Web.Areas.Findings;

public sealed class FindingIndexViewModel
{
    public required PagedResult<FindingListItem> Findings { get; init; }

    public required FindingFilter Filter { get; init; }

    public required IReadOnlyList<AssetGroupListItem> Groups { get; init; }
}

public sealed class FindingDetailsViewModel
{
    public required FindingDetails Finding { get; init; }

    public ChangeStatusFormModel Form { get; init; } = new();

    public bool CanEdit => Finding.MyRole is AssetGroupRole.Owner or AssetGroupRole.Editor;
}

public sealed class ChangeStatusFormModel
{
    [Display(Name = "Yeni durum")]
    public FindingStatus NewStatus { get; set; } = FindingStatus.InReview;

    [StringLength(FindingLimits.MaxCommentLength)]
    [Display(Name = "Açıklama / gerekçe")]
    public string? Comment { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Risk kabulü bitiş tarihi")]
    public DateOnly? RiskAcceptedUntil { get; set; }
}
