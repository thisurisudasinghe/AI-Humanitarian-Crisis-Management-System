[CmdletBinding()]
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'CmsDev4Verification',
    [ValidateRange(1,100)][int]$Iterations = 20
)

$ErrorActionPreference = 'Stop'
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) { throw 'sqlcmd was not found.' }
$runId = [Guid]::NewGuid().ToString('N')
$resourceTypeId = '49000000-0000-0000-0000-000000000001'
$inventoryId = '49100000-0000-0000-0000-000000000001'
$shelterId = '49200000-0000-0000-0000-000000000001'
$key = "DEV4-SQL-RACE-$runId"
$evidenceRoot = Join-Path $PSScriptRoot '../../docs/evidence/dev4/runs'
New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
$temp = Join-Path $evidenceRoot "sql-guards-$runId"
New-Item -ItemType Directory -Path $temp | Out-Null

function Invoke-Sql([string]$Query) {
    # SET NOCOUNT ON keeps sqlcmd's "(N rows affected)" footer out of assertions.
    $output = & $sqlcmd.Source -S $Server -d $Database -I -b -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join [Environment]::NewLine)" }
    return ($output -join [Environment]::NewLine).Trim()
}
function Start-SqlFile([string]$Path, [string]$Name) {
    $out = Join-Path $temp "$Name.out.txt"
    $err = Join-Path $temp "$Name.err.txt"
    Remove-Item $out,$err -Force -ErrorAction SilentlyContinue

    # Start-Process -NoNewWindow does not populate ExitCode reliably in Windows
    # PowerShell. Use System.Diagnostics.Process so the SQL exit code is real.
    $psi = New-Object System.Diagnostics.ProcessStartInfo($sqlcmd.Source)
    $psi.Arguments = '-S "' + $Server + '" -d "' + $Database + '" -I -b -i "' + $Path + '"'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($psi)
    return [pscustomobject]@{
        Process = $process
        OutPath = $out
        ErrPath = $err
        OutTask = $process.StandardOutput.ReadToEndAsync()
        ErrTask = $process.StandardError.ReadToEndAsync()
    }
}
function Wait-SqlFile($Job) {
    $Job.Process.WaitForExit()
    Set-Content -Path $Job.OutPath -Value $Job.OutTask.Result -Encoding UTF8
    Set-Content -Path $Job.ErrPath -Value $Job.ErrTask.Result -Encoding UTF8
    return $Job.Process.ExitCode
}
function Assert-OneWins([string]$Label, [string]$Query, [string]$ResetQuery, [string]$VerifyQuery, [string]$Expected) {
    $a = Join-Path $temp "$Label-A.sql"; $b = Join-Path $temp "$Label-B.sql"
    Set-Content -Path $a -Value $Query -Encoding UTF8
    Set-Content -Path $b -Value $Query -Encoding UTF8
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        Invoke-Sql $ResetQuery | Out-Null
        $p1 = Start-SqlFile $a "$Label-A"; $p2 = Start-SqlFile $b "$Label-B"
        $exit1 = Wait-SqlFile $p1; $exit2 = Wait-SqlFile $p2
        if ($exit1 -ne 0 -or $exit2 -ne 0) { throw "$Label SQL race failed on iteration $iteration (exit $exit1/$exit2). Inspect $temp." }
        $actual = Invoke-Sql $VerifyQuery
        if ($actual -ne $Expected) { throw "$Label race iteration $iteration expected '$Expected' but got '$actual'." }
    }
    Write-Host "$Label guarded race: PASS ($Iterations/$Iterations iterations; $Expected)" -ForegroundColor Green
}

try {
    # Dedicated test fixtures; no production/demo inventory or shelter is modified.
    Invoke-Sql @"
IF NOT EXISTS (SELECT 1 FROM res.resource_types WHERE id='$resourceTypeId')
 INSERT res.resource_types(id,name,unit,description,created_at) VALUES('$resourceTypeId',N'Dev4 SQL race fixture',N'unit',N'temporary verification fixture',SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM res.inventories WHERE id='$inventoryId')
 INSERT res.inventories(id,resource_type_id,org_id,quantity,available,reserved,allocated,dispatched,utilised,reorder_threshold,lat,lng,accessibility_capable,created_at)
 VALUES('$inventoryId','$resourceTypeId','0f000000-0000-0000-0000-000000000001',1,1,0,0,0,0,0,7.2,79.8,0,SYSUTCDATETIME());
ELSE UPDATE res.inventories SET quantity=1,available=1,reserved=0,allocated=0,dispatched=0,utilised=0 WHERE id='$inventoryId';
IF NOT EXISTS (SELECT 1 FROM shelter.shelters WHERE id='$shelterId')
 INSERT shelter.shelters(id,name,capacity,occupancy,facilities,accessibility_features,services,lat,lng,org_id,approved,created_at)
 VALUES('$shelterId',N'Dev4 SQL race fixture',1,0,N'[]',N'[]',N'[]',7.2,79.8,'0f000000-0000-0000-0000-000000000001',0,SYSUTCDATETIME());
ELSE UPDATE shelter.shelters SET capacity=1,occupancy=0 WHERE id='$shelterId';
"@ | Out-Null

    $resourceRace = @"
SET NOCOUNT ON;
BEGIN TRAN;
UPDATE res.inventories SET available=available-1, allocated=allocated+1, updated_at=SYSUTCDATETIME()
WHERE id='$inventoryId' AND available>=1;
DECLARE @changed int=@@ROWCOUNT;
WAITFOR DELAY '00:00:00.500';
IF @changed=1 COMMIT TRAN ELSE ROLLBACK TRAN;
SELECT @changed AS RowsChanged;
"@
    Assert-OneWins 'ResourceLastUnit' $resourceRace "UPDATE res.inventories SET available=1,allocated=0 WHERE id='$inventoryId';" "SELECT CONCAT(CONVERT(int,available),':',CONVERT(int,allocated)) FROM res.inventories WHERE id='$inventoryId';" '0:1'

    $shelterRace = @"
SET NOCOUNT ON;
BEGIN TRAN;
UPDATE shelter.shelters SET occupancy=occupancy+1, updated_at=SYSUTCDATETIME()
WHERE id='$shelterId' AND occupancy+1<=capacity;
DECLARE @changed int=@@ROWCOUNT;
WAITFOR DELAY '00:00:00.500';
IF @changed=1 COMMIT TRAN ELSE ROLLBACK TRAN;
SELECT @changed AS RowsChanged;
"@
    Assert-OneWins 'ShelterLastPlace' $shelterRace "UPDATE shelter.shelters SET occupancy=0 WHERE id='$shelterId';" "SELECT CONCAT(occupancy,':',capacity) FROM shelter.shelters WHERE id='$shelterId';" '1:1'

    # Unique idempotency key: repeated concurrent pairs must leave exactly one row per key.
    $a = Join-Path $temp 'Idempotency-A.sql'; $b = Join-Path $temp 'Idempotency-B.sql'
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        $iterationKey = "$key-$iteration"
        $insertRace = @"
SET NOCOUNT ON;
BEGIN TRY
 INSERT res.allocations(id,request_id,inventory_id,quantity,decision_id,status,explanation,idempotency_key,created_at)
 VALUES(NEWID(),'55555555-5555-5555-5555-555555555555','$inventoryId',1,'66666666-6666-6666-6666-666666666666',N'Allocated',N'SQL unique-key verification','$iterationKey',SYSUTCDATETIME());
 SELECT N'INSERTED';
END TRY
BEGIN CATCH
 IF ERROR_NUMBER() IN (2601,2627) SELECT N'DUPLICATE_BLOCKED'; ELSE THROW;
END CATCH;
"@
        Set-Content $a $insertRace -Encoding UTF8; Set-Content $b $insertRace -Encoding UTF8
        $p1 = Start-SqlFile $a "Idempotency-A-$iteration"; $p2 = Start-SqlFile $b "Idempotency-B-$iteration"
        $exit1 = Wait-SqlFile $p1; $exit2 = Wait-SqlFile $p2
        if ($exit1 -ne 0 -or $exit2 -ne 0) { throw "Idempotency race failed on iteration $iteration (exit $exit1/$exit2)." }
        $count = Invoke-Sql "SELECT COUNT(*) FROM res.allocations WHERE idempotency_key='$iterationKey';"
        if ($count -ne '1') { throw "Idempotency iteration $iteration expected one row; found $count." }
    }
    Write-Host "Concurrent idempotency-key uniqueness: PASS ($Iterations/$Iterations iterations; one row per key)" -ForegroundColor Green

    # Defence-in-depth CHECK constraints must reject invalid direct SQL writes.
    $inventoryRejected = Invoke-Sql @"
DECLARE @rejected bit=0;
BEGIN TRY UPDATE res.inventories SET available=-1 WHERE id='$inventoryId'; END TRY
BEGIN CATCH IF ERROR_NUMBER()=547 SET @rejected=1 ELSE THROW; END CATCH;
IF @rejected=0 THROW 51001, 'Inventory CHECK constraint did not reject negative available.', 1;
SELECT 'REJECTED';
"@
    if ($inventoryRejected -ne 'REJECTED') { throw 'Inventory negative-stock CHECK probe failed.' }
    $shelterRejected = Invoke-Sql @"
DECLARE @rejected bit=0;
BEGIN TRY UPDATE shelter.shelters SET occupancy=capacity+1 WHERE id='$shelterId'; END TRY
BEGIN CATCH IF ERROR_NUMBER()=547 SET @rejected=1 ELSE THROW; END CATCH;
IF @rejected=0 THROW 51002, 'Shelter CHECK constraint did not reject occupancy above capacity.', 1;
SELECT 'REJECTED';
"@
    if ($shelterRejected -ne 'REJECTED') { throw 'Shelter occupancy CHECK probe failed.' }
    Write-Host 'Inventory and shelter CHECK constraints: PASS' -ForegroundColor Green

    Write-Host 'NOTE: These are real SQL Server guard/unique-index tests. They do not replace concurrent API tests of OperationsService, decision gates, audit, outbox or Phase-3 recovery.' -ForegroundColor Yellow
}
finally {
    try { Invoke-Sql "DELETE FROM res.allocations WHERE idempotency_key LIKE '$key%'; DELETE FROM res.inventories WHERE id='$inventoryId'; DELETE FROM res.resource_types WHERE id='$resourceTypeId'; DELETE FROM shelter.shelters WHERE id='$shelterId';" | Out-Null } catch { Write-Warning "Fixture cleanup needs attention: $_" }
    Write-Host "SQL process output retained in: $temp" -ForegroundColor Cyan
}
