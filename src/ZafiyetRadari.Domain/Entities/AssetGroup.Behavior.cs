using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Domain.Entities;

public partial class AssetGroup
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    /// <summary>Grubu oluşturur ve oluşturan kullanıcıyı Owner olarak ekler. Sahipsiz grup olamaz.</summary>
    public static Result<AssetGroup> Create(string name, string? description, string ownerUserId, DateTimeOffset now)
    {
        var validation = Validate(ref name, ref description);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            return AssetGroupErrors.OwnerRequired;
        }

        var group = new AssetGroup
        {
            Name = name,
            Description = description,
            CreatedBy = ownerUserId,
            CreatedAt = now
        };

        group.AssetGroupMembers.Add(new AssetGroupMember
        {
            UserId = ownerUserId,
            Role = (byte)AssetGroupRole.Owner,
            AddedAt = now
        });

        return group;
    }

    public Result Update(string name, string? description)
    {
        var validation = Validate(ref name, ref description);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name;
        Description = description;
        return Result.Success();
    }

    /// <summary>Kullanıcının bu gruptaki rolü; üye değilse null.</summary>
    public AssetGroupRole? RoleOf(string userId) =>
        AssetGroupMembers.FirstOrDefault(member => member.UserId == userId)?.MemberRole;

    public Result AddMember(string userId, AssetGroupRole role, DateTimeOffset now)
    {
        if (!Enum.IsDefined(role))
        {
            return Result.Failure(AssetGroupErrors.InvalidRole);
        }

        if (RoleOf(userId) is not null)
        {
            return Result.Failure(AssetGroupErrors.MemberAlreadyExists);
        }

        AssetGroupMembers.Add(new AssetGroupMember { UserId = userId, Role = (byte)role, AddedAt = now });
        return Result.Success();
    }

    public Result ChangeMemberRole(string userId, AssetGroupRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return Result.Failure(AssetGroupErrors.InvalidRole);
        }

        var member = AssetGroupMembers.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(AssetGroupErrors.MemberNotFound);
        }

        if (member.MemberRole == AssetGroupRole.Owner && role != AssetGroupRole.Owner && IsLastOwner(member))
        {
            return Result.Failure(AssetGroupErrors.LastOwner);
        }

        member.Role = (byte)role;
        return Result.Success();
    }

    public Result RemoveMember(string userId)
    {
        var member = AssetGroupMembers.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            return Result.Failure(AssetGroupErrors.MemberNotFound);
        }

        if (member.MemberRole == AssetGroupRole.Owner && IsLastOwner(member))
        {
            return Result.Failure(AssetGroupErrors.LastOwner);
        }

        AssetGroupMembers.Remove(member);
        return Result.Success();
    }

    private bool IsLastOwner(AssetGroupMember member) =>
        !AssetGroupMembers.Any(m => m != member && m.MemberRole == AssetGroupRole.Owner);

    private static Result Validate(ref string name, ref string? description)
    {
        name = name?.Trim() ?? string.Empty;
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (name.Length == 0)
        {
            return Result.Failure(AssetGroupErrors.NameRequired);
        }

        if (name.Length > MaxNameLength)
        {
            return Result.Failure(AssetGroupErrors.NameTooLong);
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            return Result.Failure(AssetGroupErrors.DescriptionTooLong);
        }

        return Result.Success();
    }
}
