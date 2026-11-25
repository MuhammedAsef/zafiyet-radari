using Hangfire;
using Serilog;
using ZafiyetRadari.Application;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Jobs;
using ZafiyetRadari.Infrastructure;
using ZafiyetRadari.Worker.Jobs;
using ZafiyetRadari.Worker.Security;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddBackgroundJobs(builder.Configuration);

builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"zr-worker-{Environment.MachineName}";
    options.Queues = ["default"];
});

// Eşleştirme işleri ayrı kuyrukta, tek işçiyle: sırayla çalışır, bulgu kayıtlarında yarış olmaz.
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = $"zr-matching-{Environment.MachineName}";
    options.Queues = ["matching"];
    options.WorkerCount = 1;
});

builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();

builder.Services.AddTransient<HeartbeatJob>();
builder.Services.AddTransient<AuditChainAnchorJob>();
builder.Services.AddTransient<SyncVulnerabilityFeedJob>();
builder.Services.AddTransient<MatchingJob>();
builder.Services.AddTransient<NotificationJob>();

var host = builder.Build();

RecurringJobRegistry.Register(host.Services.GetRequiredService<IRecurringJobManager>());

host.Run();
