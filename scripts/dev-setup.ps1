#Requires -Version 5.1
<#
.SYNOPSIS
    Lokal geliştirme sırlarını User Secrets'a yazar. Hiçbir sır repoya girmez.

.DESCRIPTION
    - Migrator: sa bağlantısı + Web/Worker login şifreleri
    - Web:      zr_web login'i ile bağlantı
    - Worker:   zr_worker login'i ile bağlantı
    Web/Worker şifreleri her çalıştırmada yeniden üretilir. Sonrasında Migrator'ı çalıştır,
    login şifreleri veritabanında güncellensin.

    User Secrets dosyaları %APPDATA%\Microsoft\UserSecrets altında, repo dışında durur.
    Sadece geliştirme içindir; production'da ortam değişkenleri / Docker secrets kullanılır.
#>

$ErrorActionPreference = 'Stop'

function New-StrongPassword {
    $bytes = New-Object byte[] 24
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    $core = [Convert]::ToBase64String($bytes) -replace '[^A-Za-z0-9]', ''
    # Büyük harf, küçük harf, rakam ve sembol garanti (SQL Server CHECK_POLICY).
    return "Zr!$core" + '9a'
}

function Set-Secret([string]$Project, [string]$Key, [string]$Value) {
    dotnet user-secrets set $Key $Value --project $Project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "user-secrets başarısız: $Project / $Key" }
}

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Test-Path '.env')) {
    Copy-Item '.env.example' '.env'
    Write-Host '.env oluşturuldu (.env.example kopyası).'
}

$saLine = Get-Content '.env' | Where-Object { $_ -match '^\s*MSSQL_SA_PASSWORD\s*=' } | Select-Object -First 1
$saPassword = ($saLine -replace '^\s*MSSQL_SA_PASSWORD\s*=\s*', '').Trim()
if ([string]::IsNullOrWhiteSpace($saPassword)) { throw '.env içinde MSSQL_SA_PASSWORD bulunamadı.' }

$baseConnection = 'Server=localhost,1433;Database=ZafiyetRadari;Encrypt=True;TrustServerCertificate=True'
$webPassword = New-StrongPassword
$workerPassword = New-StrongPassword

$migrator = 'src/ZafiyetRadari.Migrator'
$web = 'src/ZafiyetRadari.Web'
$worker = 'src/ZafiyetRadari.Worker'

Set-Secret $migrator 'ConnectionStrings:Default' "$baseConnection;User Id=sa;Password=$saPassword"
Set-Secret $migrator 'DatabaseLogins:WebPassword' $webPassword
Set-Secret $migrator 'DatabaseLogins:WorkerPassword' $workerPassword

Set-Secret $web 'ConnectionStrings:Default' "$baseConnection;User Id=zr_web;Password=$webPassword"
Set-Secret $worker 'ConnectionStrings:Default' "$baseConnection;User Id=zr_worker;Password=$workerPassword"

Write-Host ''
Write-Host 'Sırlar User Secrets''a yazıldı.' -ForegroundColor Green
Write-Host 'Şimdi Migrator''ı çalıştır:  dotnet run --project src/ZafiyetRadari.Migrator'
