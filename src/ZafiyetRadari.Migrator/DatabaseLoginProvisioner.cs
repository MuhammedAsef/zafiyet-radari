using System.Data;
using Microsoft.Data.SqlClient;

namespace ZafiyetRadari.Migrator;

/// <summary>
/// Web ve Worker için ayrı SQL login'leri oluşturur (varsa şifresini günceller) ve rollerine ekler.
/// Uygulama hiçbir zaman sa ile bağlanmaz.
/// </summary>
internal static class DatabaseLoginProvisioner
{
    // CREATE LOGIN / ALTER ROLE ifadeleri parametre kabul etmez. Değerler yine de SqlParameter olarak gelir;
    // T-SQL içinde QUOTENAME ile kaçışlanarak dinamik SQL'e eklenir. Kullanıcı girdisi hiçbir zaman string birleştirilmez.
    private const string ProvisionSql = """
        DECLARE @sql nvarchar(max);

        IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
            SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login)
                     + N' WITH PASSWORD = ' + QUOTENAME(@password, N'''')
                     + N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(DB_NAME()) + N';';
        ELSE
            SET @sql = N'ALTER LOGIN ' + QUOTENAME(@login)
                     + N' WITH PASSWORD = ' + QUOTENAME(@password, N'''') + N';';

        IF @sql IS NULL THROW 50001, N'Login ifadesi oluşturulamadı (değer çok uzun olabilir).', 1;
        EXEC sys.sp_executesql @sql;

        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @login)
        BEGIN
            SET @sql = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';';
            EXEC sys.sp_executesql @sql;
        END

        IF ISNULL(IS_ROLEMEMBER(@role, @login), 0) = 0
        BEGIN
            SET @sql = N'ALTER ROLE ' + QUOTENAME(@role) + N' ADD MEMBER ' + QUOTENAME(@login) + N';';
            EXEC sys.sp_executesql @sql;
        END
        """;

    public static async Task RunAsync(string connectionString, DatabaseLoginsOptions options, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await ProvisionAsync(connection, "zr_web", "zr_web_role", options.WebPassword, cancellationToken);
        await ProvisionAsync(connection, "zr_worker", "zr_worker_role", options.WorkerPassword, cancellationToken);
    }

    private static async Task ProvisionAsync(
        SqlConnection connection,
        string login,
        string role,
        string password,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ProvisionSql;
        command.Parameters.Add(new SqlParameter("@login", SqlDbType.NVarChar, 128) { Value = login });
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 128) { Value = role });
        command.Parameters.Add(new SqlParameter("@password", SqlDbType.NVarChar, 128) { Value = password });

        await command.ExecuteNonQueryAsync(cancellationToken);
        Console.WriteLine($"Login hazır: {login} -> {role}");
    }
}
