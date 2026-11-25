using Microsoft.Data.SqlClient;

namespace ZafiyetRadari.Infrastructure.Persistence;

/// <summary>
/// Okuma ağırlıklı sorgular (dashboard, raporlar) ve health check için ham bağlantı üretir.
/// Infrastructure dışına açılmaz: Application katmanı veritabanını bilmez.
/// </summary>
internal interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

internal sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
