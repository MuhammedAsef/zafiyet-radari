using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class Finding
{
    public long Id { get; set; }

    public long ComponentId { get; set; }

    public int VulnerabilityId { get; set; }

    public byte Status { get; set; }

    public byte MatchConfidence { get; set; }

    public decimal RiskScore { get; set; }

    public byte Priority { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateOnly? RiskAcceptedUntil { get; set; }

    public string? AssignedToUserId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;

    public virtual ICollection<FindingHistory> FindingHistories { get; set; } = new List<FindingHistory>();

    public virtual Vulnerability Vulnerability { get; set; } = null!;
}
