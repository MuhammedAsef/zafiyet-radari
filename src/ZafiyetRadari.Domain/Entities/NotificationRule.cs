using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class NotificationRule
{
    public int Id { get; set; }

    public int ChannelId { get; set; }

    public byte MinPriority { get; set; }

    public bool OnlyKev { get; set; }

    public bool IsEnabled { get; set; }

    public virtual NotificationChannel Channel { get; set; } = null!;
}
