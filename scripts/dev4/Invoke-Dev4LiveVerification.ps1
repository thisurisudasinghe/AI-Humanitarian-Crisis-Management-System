[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5210',
    [string]$Database = 'CmsDev4Verification',
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Password = 'Demo1234!'
)

# Live, repeatable Member 4 verification against the disposable LocalDB database.
# Run only after Initialize-Dev4LocalDb.ps1 has created the schema, the API has
# started once in Development to seed demo users, and RunScenarioSeeds has run.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$base = $ApiBaseUrl.TrimEnd('/')
$r6 = '1d000000-0000-0000-0000-000000000006'
$r7 = '1d000000-0000-0000-0000-000000000007'
$fullShelter = '44000000-0000-0000-0000-000000000003'

function Write-Step([string]$Message) { Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
    Write-Host "PASS: $Message" -ForegroundColor Green
}
function Invoke-Api([string]$Token, [string]$Method, [string]$Path, $Body = $null, [hashtable]$ExtraHeaders = @{}) {
    $headers = @{ Accept = 'application/json' }
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    foreach ($key in $ExtraHeaders.Keys) { $headers[$key] = $ExtraHeaders[$key] }
    $params = @{ Uri = "$base/api/v1$Path"; Method = $Method; Headers = $headers; TimeoutSec = 60; UseBasicParsing = $true }
    if ($null -ne $Body) { $params.ContentType = 'application/json'; $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress) }
    $status = 0; $content = ''
    try {
        $response = Invoke-WebRequest @params
        $status = [int]$response.StatusCode
        $content = $response.Content
    }
    catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $status = [int]$response.StatusCode
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $content = $reader.ReadToEnd()
    }
    $json = $null
    try { $json = $content | ConvertFrom-Json } catch { }
    return [pscustomobject]@{ Status = $status; Json = $json; Raw = $content }
}
function Login([string]$Email) {
    $result = Invoke-Api '' POST '/auth/login' @{ email = $Email; password = $Password }
    Assert-True ($result.Status -eq 200) "login $Email returned $($result.Status)"
    return $result.Json.accessToken
}
function New-Recommendation([string]$Token, [string]$Type, [string]$RequestId) {
    $result = Invoke-Api $Token POST '/recommendations/match' @{ requestId = $RequestId; type = $Type }
    Assert-True ($result.Status -eq 200) "$Type recommendation for $RequestId returned $($result.Status): $($result.Raw)"
    return $result.Json
}
function New-ApprovedDecision([string]$Token, [string]$RecommendationId, [hashtable]$Payload, [string]$Reason) {
    $body = @{
        outcome = 'Approved'
        finalPayloadJson = ($Payload | ConvertTo-Json -Depth 10 -Compress)
        reason = $Reason
    }
    $result = Invoke-Api $Token POST "/recommendations/$RecommendationId/decisions" $body
    Assert-True ($result.Status -eq 201) "decision for $RecommendationId returned $($result.Status): $($result.Raw)"
    return $result.Json.id
}
function Send-ConcurrentPost([string]$Token, [string]$Path, $Body, [string]$Key) {
    $uri = "$base/api/v1$Path"
    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    $script = {
        param([string]$Uri, [string]$Token, [string]$Json, [string]$Key)
        $headers = @{ Accept = 'application/json'; Authorization = "Bearer $Token" }
        if ($Key) { $headers['Idempotency-Key'] = $Key }
        try {
            $response = Invoke-WebRequest -Uri $Uri -Method Post -Headers $headers -ContentType 'application/json' -Body $Json -UseBasicParsing -TimeoutSec 60
            $status = [int]$response.StatusCode
            $content = $response.Content
        }
        catch {
            $response = $_.Exception.Response
            if ($null -eq $response) { throw }
            $status = [int]$response.StatusCode
            $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
            $content = $reader.ReadToEnd()
        }
        $parsed = $null
        try { $parsed = $content | ConvertFrom-Json } catch { }
        return [pscustomobject]@{ Status = $status; Json = $parsed; Raw = $content }
    }
    return Start-Job -ScriptBlock $script -ArgumentList @($uri, $Token, $json, $Key)
}
function Read-TaskResult($Job) {
    Wait-Job -Job $Job | Out-Null
    $result = Receive-Job -Job $Job -ErrorAction Stop
    Remove-Job -Job $Job
    return $result
}
function Sql-Scalar([string]$Query) {
    $output = & sqlcmd -S $Server -d $Database -I -b -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $($output -join "`n")" }
    return (($output -join "`n").Trim())
}

Write-Step 'Health check'
$health = Invoke-RestMethod "$base/api/health" -TimeoutSec 30
Assert-True ($health.status -eq 'Healthy') "health endpoint reports Healthy"

Write-Step 'Demo logins'
$coordinator = Login 'coordinator@example.org'
$caseManager = Login 'casemanager@example.org'
$logistics = Login 'logistics@example.org'
$shelter = Login 'shelter@example.org'
$volCoordinator = Login 'volunteercoordinator@example.org'
$volunteer = Login 'volunteer@example.org'
$citizen = Login 'citizen@example.org'
Write-Host 'All seven role-specific logins succeeded.'

Write-Step 'Dev 2 request data + Dev 3 recommendation/decision chain (R6)'
$before = Invoke-Api $coordinator GET "/requests/$r6"
Assert-True ($before.Status -eq 200) "GET /requests/$r6 returned $($before.Status)"
Write-Host "R6 status before recommendation: $($before.Json.status)"

$resourceRec = New-Recommendation $logistics 'ResourceMatch' $r6
Assert-True ($resourceRec.candidates.Count -gt 0) 'ResourceMatch returned at least one candidate'
$resourceInventory = $resourceRec.candidates[0].targetId
Write-Host "Top resource candidate: $resourceInventory"
$resourceDecision = New-ApprovedDecision $logistics $resourceRec.recommendationId @{
    requestId = $r6; inventoryId = $resourceInventory; quantity = 1
} 'Dev4 live verification: approved resource allocation'
Write-Host "Approved ResourceMatch decision: $resourceDecision"

$shelterRec = New-Recommendation $shelter 'ShelterMatch' $r6
Assert-True ($shelterRec.candidates.Count -gt 0) 'ShelterMatch returned at least one candidate'
$topShelter = $shelterRec.candidates[0].targetId
Write-Host "Top shelter candidate: $topShelter"
$shelterDecision = New-ApprovedDecision $shelter $shelterRec.recommendationId @{
    requestId = $r6; shelterId = $topShelter; householdSize = 5
} 'Dev4 live verification: approved shelter allocation'
Write-Host "Approved ShelterMatch decision: $shelterDecision"

$afterRec = Invoke-Api $coordinator GET "/requests/$r6"
Assert-True ($afterRec.Status -eq 200) "GET /requests/$r6 after recommendation returned $($afterRec.Status)"
Write-Host "R6 status after recommendation: $($afterRec.Json.status)"

Write-Step 'End-to-end resource dispatch + shelter + volunteer workflow'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\')).Path
& (Join-Path $repoRoot 'scripts\dev4\Invoke-Dev4EndToEnd.ps1') `
    -ApiBaseUrl $base `
    -LogisticsManagerToken $logistics -ApprovedResourceDecisionId $resourceDecision `
    -ShelterAdminToken $shelter -ApprovedShelterDecisionId $shelterDecision `
    -VolunteerCoordinatorToken $volCoordinator -VolunteerToken $volunteer

Write-Step 'Live API idempotency: two concurrent allocations with the same key'
$idemRec = New-Recommendation $logistics 'ResourceMatch' $r7
Assert-True ($idemRec.candidates.Count -gt 0) 'ResourceMatch for R7 returned at least one candidate'
$idemInventory = $idemRec.candidates[0].targetId
$idemDecision = New-ApprovedDecision $logistics $idemRec.recommendationId @{
    requestId = $r7; inventoryId = $idemInventory; quantity = 1
} 'Dev4 live verification: concurrent idempotency'
$idemKey = "DEV4-LIVE-IDEM-$([Guid]::NewGuid().ToString('N'))"
$availableBefore = [int](Sql-Scalar "SELECT available FROM res.inventories WHERE id='$idemInventory';")
$t1 = Send-ConcurrentPost $logistics '/allocations' @{ decisionId = $idemDecision } $idemKey
$t2 = Send-ConcurrentPost $logistics '/allocations' @{ decisionId = $idemDecision } $idemKey
$r1 = Read-TaskResult $t1
$r2 = Read-TaskResult $t2
Assert-True ($r1.Status -in 200,201 -and $r2.Status -in 200,201) "parallel same-key allocations returned $($r1.Status)/$($r2.Status)"
Assert-True ($r1.Json.id -eq $r2.Json.id) "parallel same-key allocations returned the same allocation id $($r1.Json.id)"
$keyRows = [int](Sql-Scalar "SELECT COUNT(*) FROM res.allocations WHERE idempotency_key='$idemKey';")
Assert-True ($keyRows -eq 1) "exactly one allocation row exists for key $idemKey"
$availableAfter = [int](Sql-Scalar "SELECT available FROM res.inventories WHERE id='$idemInventory';")
Assert-True (($availableBefore - $availableAfter) -eq 1) "stock decremented exactly once ($availableBefore -> $availableAfter)"

Write-Step 'Live API: shelter overbooking is rejected'
$fullRec = New-Recommendation $shelter 'ShelterMatch' $r7
$fullDecision = New-ApprovedDecision $shelter $fullRec.recommendationId @{
    requestId = $r7; shelterId = $fullShelter; householdSize = 1
} 'Dev4 live verification: full shelter must be rejected'
$fullResult = Invoke-Api $shelter POST '/shelters/allocations' @{ decisionId = $fullDecision }
Assert-True ($fullResult.Status -eq 409) "full shelter allocation returned $($fullResult.Status): $($fullResult.Raw)"
Assert-True ($fullResult.Raw -match 'SHELTER_FULL') "full shelter error code is SHELTER_FULL"

Write-Step 'Live API: concurrent duplicate shelter allocation on one decision'
$dupRec = New-Recommendation $shelter 'ShelterMatch' $r7
Assert-True ($dupRec.candidates.Count -gt 0) 'ShelterMatch for duplicate probe returned a candidate'
$dupShelter = $dupRec.candidates[0].targetId
$dupDecision = New-ApprovedDecision $shelter $dupRec.recommendationId @{
    requestId = $r7; shelterId = $dupShelter; householdSize = 1
} 'Dev4 live verification: duplicate shelter allocation probe'
$occupancyBefore = [int](Sql-Scalar "SELECT occupancy FROM shelter.shelters WHERE id='$dupShelter';")
$s1 = Send-ConcurrentPost $shelter '/shelters/allocations' @{ decisionId = $dupDecision } $null
$s2 = Send-ConcurrentPost $shelter '/shelters/allocations' @{ decisionId = $dupDecision } $null
$sr1 = Read-TaskResult $s1
$sr2 = Read-TaskResult $s2
$occupancyAfter = [int](Sql-Scalar "SELECT occupancy FROM shelter.shelters WHERE id='$dupShelter';")
$decisionRows = [int](Sql-Scalar "SELECT COUNT(*) FROM shelter.shelter_allocations WHERE decision_id='$dupDecision';")
Write-Host "Duplicate shelter probe statuses: $($sr1.Status)/$($sr2.Status); decision rows: $decisionRows; occupancy: $occupancyBefore -> $occupancyAfter"
Assert-True ($decisionRows -eq 1 -and ($occupancyAfter - $occupancyBefore) -eq 1) 'duplicate shelter decision created exactly one allocation and one occupancy increment'

Write-Step 'Mapping, geo fallback and API error handling'
$geo = Invoke-Api '' GET '/geo/distance?fromLat=7.2088&fromLng=79.8358&toLat=6.9271&toLng=79.8612'
Assert-True ($geo.Status -eq 200) "anonymous geo distance returned $($geo.Status)"
Assert-True ($null -ne $geo.Json.km) 'geo distance response contains km'
$badGeo = Invoke-Api '' GET '/geo/distance?fromLat=91&fromLng=0&toLat=0&toLng=0'
Assert-True ($badGeo.Status -gt 399) "invalid geo coordinates rejected with $($badGeo.Status)"
$missingKey = Invoke-Api $logistics POST '/allocations' @{ decisionId = $resourceDecision }
Assert-True ($missingKey.Status -gt 399) "missing Idempotency-Key rejected with $($missingKey.Status)"
$forbidden = Invoke-Api $citizen GET '/allocations'
Assert-True ($forbidden.Status -eq 403) "citizen allocation read forbidden ($($forbidden.Status))"
$publicShelters = Invoke-Api '' GET '/shelters/public'
Assert-True ($publicShelters.Status -eq 200) "anonymous public shelters returned $($publicShelters.Status)"
$areasLogistics = Invoke-Api $logistics GET '/operations/areas'
Assert-True ($areasLogistics.Status -eq 200) "LogisticsManager GET /operations/areas returned $($areasLogistics.Status)"
$areasShelter = Invoke-Api $shelter GET '/operations/areas'
Assert-True ($areasShelter.Status -eq 200) "ShelterAdmin GET /operations/areas returned $($areasShelter.Status)"
$areasVolCoordinator = Invoke-Api $volCoordinator GET '/operations/areas'
Assert-True ($areasVolCoordinator.Status -eq 200) "VolunteerCoordinator GET /operations/areas returned $($areasVolCoordinator.Status)"
$typesShelter = Invoke-Api $shelter GET '/resources/types'
Assert-True ($typesShelter.Status -eq 200) "ShelterAdmin GET /resources/types returned $($typesShelter.Status)"

Write-Step 'LIVE VERIFICATION COMPLETE'
