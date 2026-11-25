using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class AssetGroupMember
{
    public int AssetGroupId { get; set; }

    public string UserId { get; set; } = null!;

    public byte Role { get; set; }

    public DateTimeOffset AddedAt { get; set; }

    public virtual AssetGroup AssetGroup { get; set; } = null!;
}
