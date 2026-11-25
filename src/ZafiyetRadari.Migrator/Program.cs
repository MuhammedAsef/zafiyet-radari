using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZafiyetRadari.Migrator;

// Sıra: 1) şema script'leri (DbUp)  2) Hangfire tabloları  3) uygulama login'leri ve rol üyelikleri.
// Bu araç yetkili (admin) bağlantıyla çalışır. Web ve Worker bu yetkiye hiçbir zaman sahip olmaz.
//
// Ek komut:  dotnet run --project src/ZafiyetRadari.Migrator -- --grant-admin kisi@ornek.com

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<DatabaseLoginsOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseLoginsOptions.SectionName))
    .ValidateDataAnnotations();

using var host = builder.Build();

try
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "'ConnectionStrings:Default' tanımlı değil. Geliştirmede önce scripts/dev-setup.ps1 çalıştır.");
    }

    var adminEmail = builder.Configuration["grant-admin"];
    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        await AdminRoleGranter.GrantAsync(connectionString, adminEmail);
        WriteColored(ConsoleColor.Green, $"Admin rolü verildi: {adminEmail.Trim()}. Kullanıcı yeniden giriş yapmalı.");
        return 0;
    }

    // Eksik veya zayıf ayar varsa veritabanına dokunmadan önce durur.
    var logins = host.Services.GetRequiredService<IOptions<DatabaseLoginsOptions>>().Value;

    SchemaMigrator.Run(connectionString);
    HangfireSchemaInstaller.Run(connectionString);
    await DatabaseLoginProvisioner.RunAsync(connectionString, logins);

    WriteColored(ConsoleColor.Green, "Veritabanı güncel.");
    return 0;
}
catch (Exception ex)
{
    WriteColored(ConsoleColor.Red, ex.Message);
    return 1;
}

static void WriteColored(ConsoleColor color, string message)
{
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ResetColor();
}
