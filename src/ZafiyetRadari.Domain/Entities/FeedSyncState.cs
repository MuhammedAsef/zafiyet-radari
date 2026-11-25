using System;
using System.Collections.Generic;

namespace ZafiyetRadari.Domain.Entities;

public partial class FeedSyncState
{
    public string Source { get; set; } = null!;

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    public string? SyncCursor { get; set; }

    public int ItemsProcessed { get; set; }

    public string? LastError { get; set; }
}
