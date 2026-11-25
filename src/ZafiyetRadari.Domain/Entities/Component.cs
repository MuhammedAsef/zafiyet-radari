using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class Component
{
    public long Id { get; set; }

    public int AssetId { get; set; }

    public string Ecosystem { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Version { get; set; } = null!;

    public string? Purl { get; set; }

    public byte Source { get; set; }

    public int? UploadId { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual ICollection<Finding> Findings { get; set; } = new List<Finding>();

    public virtual InventoryUpload? Upload { get; set; }
}
