using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class Asset
{
    public int Id { get; set; }

    public int AssetGroupId { get; set; }

    public string Name { get; set; } = null!;

    public byte Environment { get; set; }

    public byte Criticality { get; set; }

    public bool IsInternetFacing { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual AssetGroup AssetGroup { get; set; } = null!;

    public virtual ICollection<Component> Components { get; set; } = new List<Component>();

    public virtual ICollection<InventoryUpload> InventoryUploads { get; set; } = new List<InventoryUpload>();
}
