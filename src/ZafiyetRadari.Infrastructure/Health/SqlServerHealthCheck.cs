using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure.Health;

internal sealed class SqlServerHealthCheck(ISqlConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // Exception detayı (sunucu adı vb.) sadece loga gider; /health yanıtında sadece durum görünür.
            return HealthCheckResult.Unhealthy("SQL Server'a erişilemiyor.", ex);
        }
    }
}
