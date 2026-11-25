using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Application.Inventory;

// ---- Komutlar (Web formlarından gelir; FluentValidation ile doğrulanır) ----

public sealed record CreateAssetGroupCommand(string Name, string? Description);

public sealed record UpdateAssetGroupCommand(int AssetGroupId, string Name, string? Description);

public sealed record AddGroupMemberCommand(int AssetGroupId, string Email, AssetGroupRole Role);

public sealed record ChangeGroupMemberRoleCommand(int AssetGroupId, string UserId, AssetGroupRole Role);

public sealed record CreateAssetCommand(
    int AssetGroupId,
    string Name,
    AssetEnvironment Environment,
    AssetCriticality Criticality,
    bool IsInternetFacing);

public sealed record UpdateAssetCommand(
    int AssetId,
    string Name,
    AssetEnvironment Environment,
    AssetCriticality Criticality,
    bool IsInternetFacing);

public sealed record AddComponentCommand(int AssetId, string Ecosystem, string Name, string Version);

/// <param name="Content">Dosya içeriği. Servis en fazla <see cref="InventoryLimits.MaxUploadBytes"/> bayt okur.</param>
public sealed record UploadInventoryCommand(int AssetId, string FileName, Stream Content);

// ---- Okuma modelleri ----

public sealed record AssetGroupListItem(
    int Id,
    string Name,
    string? Description,
    AssetGroupRole MyRole,
    int AssetCount,
    int MemberCount);

public sealed record AssetGroupDetails(
    int Id,
    string Name,
    string? Description,
    AssetGroupRole MyRole,
    IReadOnlyList<AssetListItem> Assets,
    IReadOnlyList<GroupMemberItem> Members);

public sealed record AssetListItem(
    int Id,
    string Name,
    AssetEnvironment Environment,
    AssetCriticality Criticality,
    bool IsInternetFacing,
    int ActiveComponentCount);

public sealed record GroupMemberItem(string UserId, string DisplayName, AssetGroupRole Role, DateTimeOffset AddedAt);

public sealed record AssetDetails(
    int Id,
    int AssetGroupId,
    string AssetGroupName,
    AssetGroupRole MyRole,
    string Name,
    AssetEnvironment Environment,
    AssetCriticality Criticality,
    bool IsInternetFacing,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int ActiveComponentCount,
    IReadOnlyList<UploadListItem> RecentUploads);

public sealed record UploadListItem(
    int Id,
    string OriginalFileName,
    UploadFormat Format,
    string Sha256Hex,
    int SizeBytes,
    int ComponentCount,
    DateTimeOffset UploadedAt);

public sealed record ComponentListItem(
    long Id,
    string Ecosystem,
    string Name,
    string Version,
    string? Purl,
    ComponentSource Source,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record ComponentFilter(string? Search = null, bool IncludeInactive = false, int Page = 1, int PageSize = 50)
{
    public const int MaxPageSize = 200;
    public const int MaxSearchLength = 100;
}

public sealed record UploadResult(
    int UploadId,
    UploadFormat Format,
    int Added,
    int Reactivated,
    int Unchanged,
    int Deactivated,
    int Skipped);

public static class InventoryLimits
{
    public const int MaxUploadMegabytes = 10;
    public const int MaxUploadBytes = MaxUploadMegabytes * 1024 * 1024;

    /// <summary>Tek dosyadaki ham paket girdisi üst sınırı (dev monorepo lock dosyaları ~10-20 bin).</summary>
    public const int MaxComponentsPerUpload = 50_000;

    public const int RecentUploadCount = 10;
}
