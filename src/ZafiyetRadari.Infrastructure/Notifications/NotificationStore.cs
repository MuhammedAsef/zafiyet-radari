using System.Data;
using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Notifications;

internal sealed class NotificationStore(ZafiyetRadariDbContext dbContext, ISqlConnectionFactory connectionFactory) : INotificationStore
{
    // Alıcı kontrolü auth tablosuna dokunur. Worker login'inin bu tabloda sadece Id, NormalizedEmail ve
    // EmailConfirmed kolonlarını okuma yetkisi var (0009); sorgu da sadece bunları kullanır.
    private const string ValidRecipientSql = """
        SELECT CASE WHEN EXISTS (
            SELECT 1
            FROM inventory.AssetGroupMembers AS m
            JOIN auth.AspNetUsers AS u ON u.Id = m.UserId
            WHERE m.AssetGroupId = @AssetGroupId
              AND u.NormalizedEmail = @NormalizedEmail
              AND u.EmailConfirmed = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<IReadOnlyList<ChannelListItem>> ListChannelsAsync(int assetGroupId, CancellationToken cancellationToken)
    {
        const byte sent = (byte)NotificationStatus.Sent;

        var rows = await dbContext.NotificationChannels
            .AsNoTracking()
            .Where(c => c.AssetGroupId == assetGroupId)
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Target,
                Rule = c.NotificationRules.OrderBy(r => r.Id).Select(r => new { r.MinPriority, r.OnlyKev }).FirstOrDefault(),
                c.IsEnabled,
                c.WeeklyDigest,
                LastSentAt = c.NotificationLogs.Where(l => l.Status == sent).Max(l => (DateTimeOffset?)l.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ChannelListItem(
                r.Id,
                r.Name,
                r.Target,
                r.Rule is null ? FindingPriority.Now : (FindingPriority)r.Rule.MinPriority,
                r.Rule?.OnlyKev ?? false,
                r.IsEnabled,
                r.LastSentAt,
                r.WeeklyDigest))
            .ToList();
    }

    public Task<NotificationChannel?> GetChannelAsync(int channelId, CancellationToken cancellationToken) =>
        dbContext.NotificationChannels
            .Include(c => c.NotificationRules)
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken);

    public void AddChannel(NotificationChannel channel) => dbContext.NotificationChannels.Add(channel);

    // Kurallar ve gönderim kayıtları veritabanında cascade ile silinir.
    public void RemoveChannel(NotificationChannel channel) => dbContext.NotificationChannels.Remove(channel);

    public async Task<IReadOnlyList<NotificationChannel>> GetEnabledChannelsForAssetAsync(int assetId, CancellationToken cancellationToken) =>
        await dbContext.NotificationChannels
            .AsNoTracking()
            .Include(c => c.NotificationRules)
            .Where(c => c.IsEnabled && c.AssetGroup.Assets.Any(a => a.Id == assetId))
            .ToListAsync(cancellationToken);

    public Task<ChannelDispatchInfo?> GetDispatchInfoAsync(int channelId, CancellationToken cancellationToken) =>
        dbContext.NotificationChannels
            .AsNoTracking()
            .Where(c => c.Id == channelId)
            .Select(c => new ChannelDispatchInfo(c.Id, c.AssetGroupId, c.AssetGroup.Name, c.Target, c.IsEnabled))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetDigestChannelIdsAsync(CancellationToken cancellationToken)
    {
        const byte email = (byte)NotificationChannelType.Email;

        return await dbContext.NotificationChannels
            .AsNoTracking()
            .Where(c => c.IsEnabled && c.WeeklyDigest && c.Type == email)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    // Özet, bulgu kimliği olmayan "Sent" kaydıyla iz bırakır (bulgu bildirimleri her zaman FindingId taşır).
    public Task<bool> HasDigestSentSinceAsync(int channelId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        const byte sent = (byte)NotificationStatus.Sent;

        return dbContext.NotificationLogs
            .AsNoTracking()
            .AnyAsync(l => l.ChannelId == channelId && l.Status == sent && l.FindingId == null && l.CreatedAt >= since, cancellationToken);
    }

    public async Task<GroupDigest> GetGroupDigestAsync(int assetGroupId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        const byte open = (byte)FindingStatus.Open, inReview = (byte)FindingStatus.InReview, fixedStatus = (byte)FindingStatus.Fixed;

        var groupFindings = dbContext.Findings
            .AsNoTracking()
            .Where(f => f.Component.Asset.AssetGroupId == assetGroupId);

        var actionable = groupFindings.Where(f => f.Status == open || f.Status == inReview);

        var byPriority = await actionable
            .GroupBy(f => f.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var openKev = await actionable.CountAsync(f => f.Vulnerability.IsKev, cancellationToken);
        var newCount = await groupFindings.CountAsync(f => f.FirstSeenAt >= since, cancellationToken);
        var fixedCount = await groupFindings.CountAsync(f => f.Status == fixedStatus && f.ResolvedAt >= since, cancellationToken);

        var topIds = await actionable
            .OrderBy(f => f.Priority).ThenByDescending(f => f.RiskScore).ThenBy(f => f.Id)
            .Select(f => f.Id)
            .Take(NotificationLimits.DigestTopFindings)
            .ToListAsync(cancellationToken);

        IReadOnlyList<FindingEmailItem> top = topIds.Count == 0 ? [] : await GetActionableFindingsAsync(topIds, cancellationToken);

        return new GroupDigest(
            byPriority.ToDictionary(x => (FindingPriority)x.Priority, x => x.Count),
            newCount,
            fixedCount,
            openKev,
            top);
    }

    public async Task<bool> IsValidRecipientAsync(int assetGroupId, string email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ValidRecipientSql;
        command.WithParameter("@AssetGroupId", SqlDbType.Int, assetGroupId);
        command.WithParameter("@NormalizedEmail", SqlDbType.NVarChar, 256, email.Trim().ToUpperInvariant());

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    public async Task<IReadOnlySet<long>> GetSentFindingIdsAsync(
        int channelId, IReadOnlyCollection<long> findingIds, DateTimeOffset since, CancellationToken cancellationToken)
    {
        const byte sent = (byte)NotificationStatus.Sent;
        var ids = findingIds.ToList();

        var sentIds = await dbContext.NotificationLogs
            .AsNoTracking()
            .Where(l => l.ChannelId == channelId && l.Status == sent && l.CreatedAt >= since && l.FindingId != null && ids.Contains(l.FindingId.Value))
            .Select(l => l.FindingId!.Value)
            .ToListAsync(cancellationToken);

        return sentIds.ToHashSet();
    }

    public async Task<IReadOnlyList<FindingEmailItem>> GetActionableFindingsAsync(IReadOnlyCollection<long> findingIds, CancellationToken cancellationToken)
    {
        const byte open = (byte)FindingStatus.Open, inReview = (byte)FindingStatus.InReview;
        var ids = findingIds.ToList();

        var rows = await dbContext.Findings
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id) && (f.Status == open || f.Status == inReview))
            .OrderBy(f => f.Priority).ThenByDescending(f => f.RiskScore)
            .Select(f => new
            {
                f.Id,
                f.Vulnerability.ExternalId,
                Cve = f.Vulnerability.VulnerabilityAliases.Where(a => a.Alias.StartsWith("CVE-")).OrderBy(a => a.Alias).Select(a => a.Alias).FirstOrDefault(),
                f.Vulnerability.Summary,
                f.Component.Name,
                f.Component.Version,
                AssetName = f.Component.Asset.Name,
                f.Priority,
                f.RiskScore,
                f.Vulnerability.IsKev,
                f.Vulnerability.KevAddedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new FindingEmailItem(
                r.Id,
                r.ExternalId.StartsWith("CVE-", StringComparison.Ordinal) ? r.ExternalId : r.Cve ?? r.ExternalId,
                r.Summary,
                r.Name,
                r.Version,
                r.AssetName,
                (FindingPriority)r.Priority,
                r.RiskScore,
                r.IsKev,
                r.ExternalId.StartsWith("MAL-", StringComparison.Ordinal),
                r.KevAddedAt))
            .ToList();
    }

    public void AddLogs(IEnumerable<NotificationLog> logs) => dbContext.NotificationLogs.AddRange(logs);
}
