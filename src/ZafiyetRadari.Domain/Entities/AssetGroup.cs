using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class AssetGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<AssetGroupMember> AssetGroupMembers { get; set; } = new List<AssetGroupMember>();

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual ICollection<NotificationChannel> NotificationChannels { get; set; } = new List<NotificationChannel>();
}
