using DbUp;

namespace ZafiyetRadari.Migrator;

/// <summary>db/migrations altındaki, henüz uygulanmamış script'leri sırayla çalıştırır.</summary>
internal static class SchemaMigrator
{
    public static void Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var result = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(SchemaMigrator).Assembly,
                name => name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .WithTransactionPerScript()
            .JournalToSqlTable("dbo", "SchemaVersions")
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException($"Şema güncellemesi başarısız: {result.Error.Message}", result.Error);
        }
    }
}
