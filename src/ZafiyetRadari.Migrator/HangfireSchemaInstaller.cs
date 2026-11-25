using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;

namespace ZafiyetRadari.Migrator;

/// <summary>
/// Hangfire tablolarını kurar. Web ve Worker'da şemayı otomatik oluşturma kapalıdır,
/// çünkü onların DDL yetkisi yoktur.
/// </summary>
internal static class HangfireSchemaInstaller
{
    public const string Schema = "hangfire";

    public static void Run(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        SqlServerObjectsInstaller.Install(connection, Schema);
    }
}
