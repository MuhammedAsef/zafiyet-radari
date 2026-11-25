using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class FindingHistory
{
    public long Id { get; set; }

    public long FindingId { get; set; }

    public byte? FromStatus { get; set; }

    public byte ToStatus { get; set; }

    public string? Comment { get; set; }

    public string? ChangedByUserId { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public virtual Finding Finding { get; set; } = null!;
}
