using System.Data;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Auditing;

/// <summary>
/// Audit kayıtlarını ops.usp_AppendAuditLog prosedürüyle ekler. Hash zinciri veritabanında hesaplanır,
/// uygulama hash üretemez veya değiştiremez. Tüm değerler tipli SqlParameter olarak gider.
/// </summary>
internal sealed class SqlAuditTrail(ISqlConnectionFactory connectionFactory) : IAuditTrail
{
    public async Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditEvent.EventType);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(auditEvent.EventType.Length, 50);

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "ops.usp_AppendAuditLog";

        command
            .WithParameter("@EventType", SqlDbType.VarChar, 50, auditEvent.EventType)
            .WithParameter("@UserId", SqlDbType.NVarChar, 450, auditEvent.UserId)
            .WithParameter("@UserName", SqlDbType.NVarChar, 256, Truncate(auditEvent.UserName, 256))
            .WithParameter("@IpAddress", SqlDbType.VarChar, 45, Truncate(auditEvent.IpAddress, 45))
            .WithParameter("@TargetType", SqlDbType.VarChar, 50, Truncate(auditEvent.TargetType, 50))
            .WithParameter("@TargetId", SqlDbType.NVarChar, 100, Truncate(auditEvent.TargetId, 100))
            .WithParameter("@Details", SqlDbType.NVarChar, 2000, Truncate(auditEvent.Details, 2000));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AuditChainStatus> VerifyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        long? firstBrokenId;
        await using (var verify = connection.CreateCommand())
        {
            verify.CommandType = CommandType.StoredProcedure;
            verify.CommandText = "ops.usp_VerifyAuditChain";
            var result = await verify.ExecuteScalarAsync(cancellationToken);
            firstBrokenId = result is null or DBNull ? null : Convert.ToInt64(result);
        }

        long? headId = null;
        string? headHash = null;
        await using (var head = connection.CreateCommand())
        {
            head.CommandText = "SELECT TOP (1) Id, Hash FROM ops.AuditLog ORDER BY Id DESC";
            await using var reader = await head.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                headId = reader.GetInt64(0);
                headHash = Convert.ToHexString((byte[])reader.GetValue(1));
            }
        }

        return new AuditChainStatus(firstBrokenId is null, firstBrokenId, headId, headHash);
    }

    // Kesmek, sessizce hata vermekten iyidir: audit kaydı uzun detay yüzünden kaybolmamalı.
    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
