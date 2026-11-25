using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using ZafiyetRadari.Application;
using ZafiyetRadari.Infrastructure;
using ZafiyetRadari.Web.Filters;
using ZafiyetRadari.Web.Logging;
using ZafiyetRadari.Web.Ops;
using ZafiyetRadari.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// Sunucu yazılımı ve sürümü yanıt başlıklarında görünmez.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddBackgroundJobs(builder.Configuration)
    .AddWebSecurity()
    .AddReverseProxySupport(builder.Configuration)
    .AddWebIdentity(builder.Configuration, builder.Environment);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<ConcurrencyConflictExceptionFilter>();
});

var app = builder.Build();

// En başta: sonraki her şey (HTTPS kontrolü, HSTS, rate limit, audit IP'si) gerçek istemciyi görmeli.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Production'da stack trace kullanıcıya gösterilmez, sadece istek kimliği.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = RequestLogLevel.Get;
    options.EnrichDiagnosticContext = (diagnostics, context) =>
        diagnostics.Set("ClientIp", context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
});
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Statik dosyalar (CSS/JS) giriş sayfasında da yüklenebilmeli.
app.MapStaticAssets().AllowAnonymous();

// Her modül kendi Area'sında: /Inventory/..., /Findings/...
app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHealthChecks("/health").AllowAnonymous();

// Hangfire'ın kendi koyu modu kapalı: panel her zaman uygulamanın temasını kullanır.
HangfireDashboardTheme.Apply();

app.MapHangfireDashboard("/ops/jobs", new DashboardOptions
    {
        DashboardTitle = "Zafiyet Radarı · İşler",
        DisplayStorageConnectionString = false,
        DarkModeEnabled = false,
        Authorization = [new HangfireDashboardAdminFilter()]
    })
    .RequireAuthorization(Policies.RequireAdmin);

app.Run();
