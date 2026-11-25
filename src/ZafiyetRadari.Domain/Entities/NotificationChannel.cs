using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class NotificationChannel
{
    public int Id { get; set; }

    public int AssetGroupId { get; set; }

    public byte Type { get; set; }

    public string Name { get; set; } = null!;

    public string Target { get; set; } = null!;

    public string? ProtectedSecret { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public virtual AssetGroup AssetGroup { get; set; } = null!;

    public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();

    public virtual ICollection<NotificationRule> NotificationRules { get; set; } = new List<NotificationRule>();
}
