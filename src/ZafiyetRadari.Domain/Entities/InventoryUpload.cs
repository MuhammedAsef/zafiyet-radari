using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class InventoryUpload
{
    public int Id { get; set; }

    public int AssetId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public byte Format { get; set; }

    public byte[] Sha256 { get; set; } = null!;

    public int SizeBytes { get; set; }

    public int ComponentCount { get; set; }

    public string UploadedBy { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual ICollection<Component> Components { get; set; } = new List<Component>();
}
