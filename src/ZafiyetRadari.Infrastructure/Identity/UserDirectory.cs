using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Identity;

namespace ZafiyetRadari.Infrastructure.Identity;

internal sealed class UserDirectory(AuthDbContext dbContext) : IUserDirectory
{
    public Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Identity e-postaları NormalizedEmail (büyük harf, invariant) ile saklar ve indeksler.
        var normalized = email.Trim().ToUpperInvariant();

        return dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalized)
            .Select(user => user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetConfirmedEmailsAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var ids = userIds.Distinct(StringComparer.Ordinal).ToList();

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.EmailConfirmed && user.Email != null)
            .ToDictionaryAsync(user => user.Id, user => user.Email!, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var ids = userIds.Distinct(StringComparer.Ordinal).ToList();

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName ?? user.Id, cancellationToken);
    }
}
