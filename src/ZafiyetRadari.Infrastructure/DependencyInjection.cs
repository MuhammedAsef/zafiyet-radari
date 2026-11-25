using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Abstractions.Ingestion;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Application.Dashboard;
using ZafiyetRadari.Application.Findings;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Application.Matching;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Infrastructure.Auditing;
using ZafiyetRadari.Infrastructure.Authorization;
using ZafiyetRadari.Infrastructure.Health;
using ZafiyetRadari.Infrastructure.Identity;
using ZafiyetRadari.Infrastructure.Ingestion;
using ZafiyetRadari.Infrastructure.Ingestion.Epss;
using ZafiyetRadari.Infrastructure.Ingestion.Kev;
using ZafiyetRadari.Infrastructure.Ingestion.Osv;
using ZafiyetRadari.Infrastructure.Dashboard;
using ZafiyetRadari.Infrastructure.Findings;
using ZafiyetRadari.Infrastructure.Inventory;
using ZafiyetRadari.Infrastructure.Inventory.Parsing;
using ZafiyetRadari.Infrastructure.Matching;
using ZafiyetRadari.Infrastructure.Notifications;
using ZafiyetRadari.Infrastructure.Persistence;

namespace ZafiyetRadari.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));

        // EnableSensitiveDataLogging bilerek kapalı: parametre değerleri (kullanıcı verisi) loglara düşmez.
        services.AddDbContext<ZafiyetRadariDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ZafiyetRadariDbContext>());

        services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IAuditTrail, SqlAuditTrail>();
        services.AddScoped<IAssetGroupAccessReader, AssetGroupAccessReader>();

        services.AddScoped<IUserDirectory, UserDirectory>();

        services.AddVulnerabilityIngestion(configuration);
        services.AddEmail(configuration);
        services.AddInventoryPersistence();

        services.AddScoped<IMatchingDataSource, MatchingDataSource>();
        services.AddScoped<IFindingRepository, FindingRepository>();
        services.AddScoped<IFindingQueries, FindingQueries>();
        services.AddScoped<IFindingStore, FindingStore>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["db", "ready"]);

        return services;
    }

    /// <summary>
    /// Hangfire istemcisini ve SQL Server depolamasını kaydeder.
    /// Job çalıştıran sunucu sadece Worker'da eklenir; Web sadece kuyruğa iş atar.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            // Ağ hataları geçicidir; 3 denemeden sonra iş "Failed" olarak panelde kalır.
            .UseFilter(new AutomaticRetryAttribute { Attempts = 3 })
            .UseSqlServerStorage(
                () => new SqlConnection(connectionString),
                new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    // Tabloları Migrator kurar; uygulama login'lerinin DDL yetkisi yoktur.
                    PrepareSchemaIfNecessary = false,
                    QueuePollInterval = TimeSpan.Zero
                }));

        services.AddScoped<IMatchingScheduler, HangfireMatchingScheduler>();
        services.AddScoped<INotificationScheduler, HangfireNotificationScheduler>();

        return services;
    }

    private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.IsTransportSecure(), "Smtp: yerel test sunucusu dışında EnableSsl=true olmalı.")
            .ValidateOnStart();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.IsValid(), "App:PublicBaseUrl mutlak bir https adresi olmalı.")
            .ValidateOnStart();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationStore, NotificationStore>();
        services.AddSingleton<IAppLinks, AppLinks>();
    }

    private static void AddInventoryPersistence(this IServiceCollection services)
    {
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryQueries, InventoryQueries>();

        // Sıra önemli: ilk eşleşen ayrıştırıcı kullanılır (CycloneDX imzası en belirgin olanı).
        services.AddSingleton<IInventoryFormatParser, CycloneDxParser>();
        services.AddSingleton<IInventoryFormatParser, NpmLockParser>();
        services.AddSingleton<IInventoryFormatParser, NuGetLockParser>();
        services.AddSingleton<IInventoryFileParser, InventoryFileParser>();
    }

    private static void AddVulnerabilityIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IngestionOptions>()
            .Bind(configuration.GetSection(IngestionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.AllUrlsAreHttps(), "Ingestion adresleri https olmalı.")
            .ValidateOnStart();

        services.AddHttpClient(FeedDownloader.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(15); // OSV npm all.zip büyük
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ZafiyetRadari/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true, // https → http yönlendirmesini .NET zaten izlemez
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            });

        services.AddScoped<FeedDownloader>();
        services.AddScoped<IFeedSyncStateStore, FeedSyncStateStore>();

        services.AddScoped<IVulnerabilityFeed>(sp =>
            ActivatorUtilities.CreateInstance<OsvFeed>(sp, new OsvFeedSettings(Ecosystems.Npm, FeedSources.OsvNpm)));
        services.AddScoped<IVulnerabilityFeed>(sp =>
            ActivatorUtilities.CreateInstance<OsvFeed>(sp, new OsvFeedSettings(Ecosystems.NuGet, FeedSources.OsvNuGet)));
        services.AddScoped<IVulnerabilityFeed, KevFeed>();
        services.AddScoped<IVulnerabilityFeed, EpssFeed>();
    }

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
        ?? throw new InvalidOperationException($"'{ConnectionStringName}' bağlantı cümlesi tanımlı değil.");
}
