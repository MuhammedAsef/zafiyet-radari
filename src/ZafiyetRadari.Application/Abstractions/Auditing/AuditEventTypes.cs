namespace ZafiyetRadari.Application.Abstractions.Auditing;

/// <summary>Audit olay türleri. Tek yerde tanımlı ki raporlama ve alarm kuralları sabit isimlere dayanabilsin.</summary>
public static class AuditEventTypes
{
    public const string LoginSucceeded = "Auth.LoginSucceeded";
    public const string LoginFailed = "Auth.LoginFailed";
    public const string LockedOut = "Auth.LockedOut";
    public const string Logout = "Auth.Logout";
    public const string UserRegistered = "Auth.UserRegistered";
    public const string EmailConfirmed = "Auth.EmailConfirmed";
    public const string PasswordChanged = "Auth.PasswordChanged";
    public const string TwoFactorChanged = "Auth.TwoFactorChanged";
    public const string RoleChanged = "Auth.RoleChanged";
    public const string AccessDenied = "Auth.AccessDenied";

    public const string ApiKeyCreated = "ApiKey.Created";
    public const string ApiKeyRevoked = "ApiKey.Revoked";

    public const string GroupCreated = "AssetGroup.Created";
    public const string GroupDeleted = "AssetGroup.Deleted";
    public const string GroupMemberAdded = "AssetGroup.MemberAdded";
    public const string GroupMemberRemoved = "AssetGroup.MemberRemoved";
    public const string GroupMemberRoleChanged = "AssetGroup.MemberRoleChanged";

    public const string AssetDeleted = "Asset.Deleted";
    public const string InventoryUploaded = "Inventory.Uploaded";

    public const string FindingStatusChanged = "Finding.StatusChanged";
    public const string FindingsExported = "Finding.Exported";
    public const string FindingRiskAccepted = "Finding.RiskAccepted";
    public const string FindingFalsePositive = "Finding.FalsePositive";
}
