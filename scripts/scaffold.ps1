#Requires -Version 5.1
<#
.SYNOPSIS
    DB First: veritabanı şemasından EF Core entity'lerini ve DbContext'i üretir.

.DESCRIPTION
    - Entity'ler   -> src/ZafiyetRadari.Domain/Entities           (saf POCO, EF bağımlılığı yok)
    - DbContext    -> src/ZafiyetRadari.Infrastructure/Persistence (fluent API konfigürasyonu)
    Üretilen dosyalar her çalıştırmada ezilir (--force). Elle kod YAZMA:
    davranışlar *.Behavior.cs, ek konfigürasyon ZafiyetRadariDbContext.Partial.cs partial dosyalarına yazılır.

    Şemayı okumak için Migrator'ın yetkili bağlantısı (User Secrets) kullanılır.
    Akış: yeni SQL script -> Migrator -> bu script -> dotnet build
#>

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet tool restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore başarısız.' }

$prefix = 'ConnectionStrings:Default = '
$line = dotnet user-secrets list --project src/ZafiyetRadari.Migrator |
    Where-Object { $_.StartsWith($prefix) } |
    Select-Object -First 1
if (-not $line) { throw 'Migrator bağlantısı bulunamadı. Önce scripts/dev-setup.ps1 çalıştır.' }
$connectionString = $line.Substring($prefix.Length).Trim()

# Sadece uygulama tabloları. Hangfire, DbUp journal, Data Protection ve Identity tabloları bilerek dışarıda.
$tables = @(
    'inventory.AssetGroups', 'inventory.AssetGroupMembers', 'inventory.Assets',
    'inventory.InventoryUploads', 'inventory.Components',
    'vuln.Vulnerabilities', 'vuln.VulnerabilityAliases', 'vuln.AffectedPackages',
    'vuln.AffectedRanges', 'vuln.VulnerabilityReferences',
    'finding.Findings', 'finding.FindingHistory',
    'notify.NotificationChannels', 'notify.NotificationRules', 'notify.NotificationLog',
    'ops.FeedSyncState', 'ops.AuditLog'
)
$tableArgs = $tables | ForEach-Object { '--table'; $_ }

dotnet ef dbcontext scaffold $connectionString Microsoft.EntityFrameworkCore.SqlServer `
    --project src/ZafiyetRadari.Infrastructure `
    --startup-project src/ZafiyetRadari.Infrastructure `
    --context ZafiyetRadariDbContext `
    --context-dir Persistence `
    --context-namespace ZafiyetRadari.Infrastructure.Persistence `
    --output-dir ../ZafiyetRadari.Domain/Entities `
    --namespace ZafiyetRadari.Domain.Entities `
    --no-onconfiguring `
    --force `
    @tableArgs

if ($LASTEXITCODE -ne 0) { throw 'Scaffold başarısız.' }

Write-Host ''
Write-Host 'Scaffold tamamlandı. Şimdi: dotnet build' -ForegroundColor Green
