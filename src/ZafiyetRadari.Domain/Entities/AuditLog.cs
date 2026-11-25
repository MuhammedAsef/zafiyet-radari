using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class AuditLog
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string EventType { get; set; } = null!;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string? IpAddress { get; set; }

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public string? Details { get; set; }

    public byte[] PreviousHash { get; set; } = null!;

    public byte[] Hash { get; set; } = null!;
}
