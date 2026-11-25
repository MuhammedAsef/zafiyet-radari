using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class AffectedRange
{
    public long Id { get; set; }

    public long AffectedPackageId { get; set; }

    public byte RangeType { get; set; }

    public string? Introduced { get; set; }

    public string? Fixed { get; set; }

    public string? LastAffected { get; set; }

    public virtual AffectedPackage AffectedPackage { get; set; } = null!;
}
