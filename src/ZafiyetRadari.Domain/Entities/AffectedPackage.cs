using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class AffectedPackage
{
    public long Id { get; set; }

    public int VulnerabilityId { get; set; }

    public string Ecosystem { get; set; } = null!;

    public string PackageName { get; set; } = null!;

    public string? Purl { get; set; }

    public virtual ICollection<AffectedRange> AffectedRanges { get; set; } = new List<AffectedRange>();

    public virtual Vulnerability Vulnerability { get; set; } = null!;
}
