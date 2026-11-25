using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class NotificationLog
{
    public long Id { get; set; }

    public int ChannelId { get; set; }

    public long? FindingId { get; set; }

    public byte Status { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual NotificationChannel Channel { get; set; } = null!;
}
