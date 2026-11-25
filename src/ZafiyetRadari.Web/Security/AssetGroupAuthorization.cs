using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Web.Security;

/// <summary>Yetki kontrolü yapılan kaynak: bir varlık grubu.</summary>
public sealed record AssetGroupResource(int AssetGroupId);

public sealed class AssetGroupAccessRequirement(AssetGroupRole minimumRole) : IAuthorizationRequirement
{
    public AssetGroupRole MinimumRole { get; } = minimumRole;
}

/// <summary>
/// Controller'larda kullanım:
/// <c>await authorizationService.AuthorizeAsync(User, new AssetGroupResource(id), AssetGroupOperations.Edit)</c>
/// </summary>
public static class AssetGroupOperations
{
    public static readonly AssetGroupAccessRequirement View = new(AssetGroupRole.Viewer);
    public static readonly AssetGroupAccessRequirement Edit = new(AssetGroupRole.Editor);
    public static readonly AssetGroupAccessRequirement Manage = new(AssetGroupRole.Owner);
}

/// <summary>
/// IDOR önlemi: URL'deki grup kimliği değiştirilse bile, kullanıcı o grubun üyesi değilse erişemez.
/// Sistem rolü (Admin dahil) bu kontrolü atlamaz; envanter verisi sadece üyelere açıktır.
/// </summary>
public sealed class AssetGroupAccessHandler(IAssetGroupAccessReader accessReader)
    : AuthorizationHandler<AssetGroupAccessRequirement, AssetGroupResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AssetGroupAccessRequirement requirement,
        AssetGroupResource resource)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var role = await accessReader.GetRoleAsync(resource.AssetGroupId, userId);

        // Rol değerleri küçüldükçe yetki artar: Owner (1) > Editor (2) > Viewer (3).
        if (role is not null && role.Value <= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }
    }
}
