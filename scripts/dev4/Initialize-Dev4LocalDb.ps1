[CmdletBinding()]
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [ValidatePattern('^[A-Za-z0-9_]+$')][string]$Database = 'CmsDev4Verification',
    [switch]$ResetExisting,
    [switch]$RunScenarioSeeds
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$dbDir = Join-Path $repoRoot 'backend/db'
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) { throw 'sqlcmd was not found. Install SQL Server command-line utilities and retry.' }

function Invoke-SqlFile([string]$File, [string]$TargetDatabase) {
    $path = Join-Path $dbDir $File
    if (-not (Test-Path $path)) { throw "Required SQL script is missing: $path" }
    Write-Host "SQL [$TargetDatabase] <- $File" -ForegroundColor Cyan
    & $sqlcmd.Source -S $Server -d $TargetDatabase -I -b -i $path
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for $File (exit $LASTEXITCODE)." }
}

if ($RunScenarioSeeds) {
    & $sqlcmd.Source -S $Server -d $Database -I -b -Q "SELECT DB_NAME() AS CurrentDatabase;"
    if ($LASTEXITCODE -ne 0) { throw "Cannot connect to $Database on $Server." }
    Write-Host 'Run only after starting the API once in Development so DevelopmentDemoSeeder creates the demo users.' -ForegroundColor Yellow
    Invoke-SqlFile '90_seed_scenario.sql' $Database
    Invoke-SqlFile '91_seed_ops.sql' $Database
    # Re-run all schema scripts and the idempotent seeds to verify re-runnability.
    foreach ($file in @('00_common.sql','01_cases.sql','02_notif.sql','03_ai.sql','04_res.sql','05_shelter.sql','06_vol.sql','08_don.sql','98_foreign_keys.sql','99_seed_users.sql','90_seed_scenario.sql','91_seed_ops.sql')) {
        Invoke-SqlFile $file $Database
    }
    Write-Host 'Scenario seeds and idempotent re-run pass completed. Run Test-Dev4SqlGuards.ps1 next.' -ForegroundColor Green
    exit 0
}

$existing = (& $sqlcmd.Source -S $Server -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID(N'$Database');").Trim()
if ($LASTEXITCODE -ne 0) { throw "Cannot connect to SQL Server instance $Server." }
if ($existing -and $existing -ne 'NULL') {
    if (-not $ResetExisting) { throw "Database '$Database' already exists. Use -ResetExisting only if you are certain it can be deleted." }
    Write-Warning "Dropping database '$Database'. All data in it will be deleted."
    & $sqlcmd.Source -S $Server -d master -I -b -Q "ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database];"
    if ($LASTEXITCODE -ne 0) { throw 'Could not drop the existing verification database.' }
}
& $sqlcmd.Source -S $Server -d master -I -b -Q "CREATE DATABASE [$Database];"
if ($LASTEXITCODE -ne 0) { throw "Could not create database '$Database'." }

# 07_geo.sql is intentionally omitted: this implementation uses IMemoryCache, not geo.geo_cache.
foreach ($file in @('00_common.sql','01_cases.sql','02_notif.sql','03_ai.sql','04_res.sql','05_shelter.sql','06_vol.sql','08_don.sql','98_foreign_keys.sql','99_seed_users.sql')) {
    Invoke-SqlFile $file $Database
}

Write-Host ''
Write-Host "Schema initialized in [$Database]. Next:" -ForegroundColor Green
Write-Host "1. Start backend/src/Cms.Api in Development with ConnectionStrings__DefaultConnection targeting [$Database]."
Write-Host '2. Wait for GET /api/health to return Healthy (this creates the Development demo users).'
Write-Host "3. Run: .\scripts\dev4\Initialize-Dev4LocalDb.ps1 -Server '$Server' -Database '$Database' -RunScenarioSeeds"
Write-Host '4. Run Test-Dev4SqlGuards.ps1 and then execute the live API/Postman workflow.'
