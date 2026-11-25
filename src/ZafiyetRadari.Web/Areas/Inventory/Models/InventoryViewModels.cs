using System.ComponentModel.DataAnnotations;
using ZafiyetRadari.Application.Common;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Web.Areas.Inventory.Models;

// Form modelleri: DataAnnotations sadece ilk savunma ve form ipuçları içindir;
// asıl doğrulama Application'daki FluentValidation + Domain kurallarıdır.

public sealed class GroupFormModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Grup adı zorunlu.")]
    [StringLength(AssetGroup.MaxNameLength)]
    [Display(Name = "Grup adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(AssetGroup.MaxDescriptionLength)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }
}

public sealed class AssetFormModel
{
    public int? Id { get; set; }

    public int AssetGroupId { get; set; }

    public string? AssetGroupName { get; set; }

    [Required(ErrorMessage = "Varlık adı zorunlu.")]
    [StringLength(Asset.MaxNameLength)]
    [Display(Name = "Varlık adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Ortam")]
    public AssetEnvironment Environment { get; set; } = AssetEnvironment.Production;

    [Display(Name = "Kritiklik")]
    public AssetCriticality Criticality { get; set; } = AssetCriticality.Medium;

    [Display(Name = "İnternete açık")]
    public bool IsInternetFacing { get; set; }
}

public sealed class AddMemberFormModel
{
    [Required(ErrorMessage = "E-posta zorunlu.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta gir.")]
    [StringLength(256)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    public AssetGroupRole Role { get; set; } = AssetGroupRole.Viewer;
}

public sealed class AddChannelFormModel
{
    [Required(ErrorMessage = "Kanal adı zorunlu.")]
    [StringLength(NotificationLimits.MaxChannelNameLength)]
    [Display(Name = "Kanal adı")]
    public string Name { get; set; } = "Ekip bildirimleri";

    [Required(ErrorMessage = "Alıcı seç.")]
    [Display(Name = "Alıcı")]
    public string RecipientUserId { get; set; } = string.Empty;

    [Display(Name = "En düşük öncelik")]
    public FindingPriority MinPriority { get; set; } = FindingPriority.ThisWeek;

    [Display(Name = "Sadece KEV / kötü amaçlı paket")]
    public bool OnlyKev { get; set; }
}

public sealed class AddComponentFormModel
{
    [Required]
    [Display(Name = "Ekosistem")]
    public string Ecosystem { get; set; } = Ecosystems.Npm;

    [Required(ErrorMessage = "Paket adı zorunlu.")]
    [StringLength(214)]
    [Display(Name = "Paket adı")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Sürüm zorunlu.")]
    [StringLength(100)]
    [Display(Name = "Sürüm")]
    public string Version { get; set; } = string.Empty;
}

public sealed class GroupDetailsViewModel
{
    public required AssetGroupDetails Group { get; init; }

    public AddMemberFormModel NewMember { get; init; } = new();

    public GroupNotificationSettings? Notifications { get; init; }

    public AddChannelFormModel NewChannel { get; init; } = new();

    public bool CanEdit => Group.MyRole is AssetGroupRole.Owner or AssetGroupRole.Editor;

    public bool CanManage => Group.MyRole == AssetGroupRole.Owner;
}

public sealed class AssetDetailsViewModel
{
    public required AssetDetails Asset { get; init; }

    public required PagedResult<ComponentListItem> Components { get; init; }

    public string? Search { get; init; }

    public bool IncludeInactive { get; init; }

    public AddComponentFormModel NewComponent { get; init; } = new();

    public bool CanEdit => Asset.MyRole is AssetGroupRole.Owner or AssetGroupRole.Editor;
}
