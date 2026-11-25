using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Ingestion;

internal sealed class FeedSyncStateStore(ZafiyetRadariDbContext dbContext) : IFeedSyncStateStore
{
    private const int MaxErrorLength = 1000;

    public Task<string?> GetCursorAsync(string source, CancellationToken cancellationToken) =>
        dbContext.FeedSyncStates
            .AsNoTracking()
            .Where(state => state.Source == source)
            .Select(state => state.SyncCursor)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task MarkAttemptAsync(string source, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var state = await GetOrCreateAsync(source, cancellationToken);
        state.LastAttemptAt = at;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSuccessAsync(string source, DateTimeOffset at, string? cursor, int itemsProcessed, CancellationToken cancellationToken)
    {
        var state = await GetOrCreateAsync(source, cancellationToken);
        state.LastSuccessAt = at;
        state.SyncCursor = cursor;
        state.ItemsProcessed = itemsProcessed;
        state.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailureAsync(string source, DateTimeOffset at, string error, CancellationToken cancellationToken)
    {
        var state = await GetOrCreateAsync(source, cancellationToken);
        state.LastAttemptAt = at;
        state.LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<FeedSyncState> GetOrCreateAsync(string source, CancellationToken cancellationToken)
    {
        var state = await dbContext.FeedSyncStates.FirstOrDefaultAsync(s => s.Source == source, cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new FeedSyncState { Source = source };
        dbContext.FeedSyncStates.Add(state);
        return state;
    }
}
