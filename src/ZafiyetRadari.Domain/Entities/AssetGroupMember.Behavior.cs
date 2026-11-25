using ZafiyetRadari.Domain.Enums;

namespace ZafiyetRadari.Domain.Entities;

public partial class AssetGroupMember
{
    public AssetGroupRole MemberRole => (AssetGroupRole)Role;

    public bool CanEdit => MemberRole is AssetGroupRole.Owner or AssetGroupRole.Editor;

    public bool CanManageMembers => MemberRole == AssetGroupRole.Owner;
}
