using Microsoft.Extensions.Logging;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Application.Inventory;

/// <summary>
/// Envanter işlemlerinin tek yetki kapısı (IDOR önlemi). Kural: grup üyeliği + yeterli rol.
/// Sistem rolü (Admin dahil) bu kontrolü atlamaz.
/// Üye olmayana "bulunamadı" döner (grubun varlığı sızdırılmaz); üye ama rolü yetmeyene "yetkiniz yok" döner.
/// Her ret audit log'a yazılır.
/// </summary>
internal sealed class InventoryAccessGuard(
    ICurrentUser currentUser,
    IAssetGroupAccessReader accessReader,
    IAuditTrail auditTrail,
    ILogger<InventoryAccessGuard> logger)
{
    /// <returns>Başarılıysa işlemi yapan kullanıcının kimliği.</returns>
    public async Task<Result<string>> RequireAsync(int assetGroupId, AssetGroupRole minimumRole, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(userId))
        {
            return AssetGroupErrors.NotFound;
        }

        var role = await accessReader.GetRoleAsync(assetGroupId, userId, cancellationToken);

        // Rol değerleri küçüldükçe yetki artar: Owner (1) > Editor (2) > Viewer (3).
        if (role is not null && role.Value <= minimumRole)
        {
            return userId;
        }

        logger.LogWarning(
            "Envanter erişimi reddedildi. Kullanıcı {UserId}, grup {AssetGroupId}, rol {Role}, gereken {MinimumRole}",
            userId, assetGroupId, role?.ToString() ?? "üye değil", minimumRole);

        await auditTrail.AppendAsync(
            new AuditEvent(
                AuditEventTypes.AccessDenied,
                userId,
                currentUser.UserName,
                currentUser.IpAddress,
                TargetType: "AssetGroup",
                TargetId: assetGroupId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Details: role is null ? "Üye değil" : $"Rol {role}, gereken {minimumRole}"),
            cancellationToken);

        return role is null ? AssetGroupErrors.NotFound : AssetGroupErrors.Forbidden;
    }

    /// <summary>Oturumu açık kullanıcı (grup bağımsız işlemler: grup oluşturma, listeleme).</summary>
    public Result<string> RequireUser()
    {
        var userId = currentUser.UserId;
        if (!currentUser.IsAuthenticated || string.IsNullOrEmpty(userId))
        {
            return AssetGroupErrors.Forbidden;
        }

        return userId;
    }

    public ICurrentUser CurrentUser => currentUser;
}
