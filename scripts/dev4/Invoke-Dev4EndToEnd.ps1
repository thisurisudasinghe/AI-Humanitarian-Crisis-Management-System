[CmdletBinding()]
param(
    [string]$ApiBaseUrl = 'http://localhost:5210',
    [Parameter(Mandatory=$true)][string]$LogisticsManagerToken,
    [Parameter(Mandatory=$true)][Guid]$ApprovedResourceDecisionId,
    [Parameter(Mandatory=$true)][string]$ShelterAdminToken,
    [Parameter(Mandatory=$true)][Guid]$ApprovedShelterDecisionId,
    [Parameter(Mandatory=$true)][string]$VolunteerCoordinatorToken,
    [Parameter(Mandatory=$true)][string]$VolunteerToken
)

$ErrorActionPreference = 'Stop'
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')
function Invoke-CmsApi([string]$Token, [string]$Method, [string]$Path, $Body = $null, [hashtable]$ExtraHeaders = @{}) {
    $headers = @{ Authorization = "Bearer $Token"; Accept = 'application/json' }
    foreach ($key in $ExtraHeaders.Keys) { $headers[$key] = $ExtraHeaders[$key] }
    $params = @{ Uri = "$ApiBaseUrl/api/v1$Path"; Method = $Method; Headers = $headers }
    if ($null -ne $Body) { $params.ContentType = 'application/json'; $params.Body = ($Body | ConvertTo-Json -Depth 20 -Compress) }
    return Invoke-RestMethod @params
}

Write-Host '1/3 Resource workflow: approved decision -> allocation -> dispatch -> delivery confirmation' -ForegroundColor Cyan
$idempotencyKey = [Guid]::NewGuid().ToString()
$allocation = Invoke-CmsApi $LogisticsManagerToken 'POST' '/allocations' @{ decisionId = $ApprovedResourceDecisionId } @{ 'Idempotency-Key' = $idempotencyKey }
if (-not $allocation.id) { throw 'Allocation response did not include an id.' }
Write-Host "Allocation: $($allocation.id) [$($allocation.status)]"
$dispatch = Invoke-CmsApi $LogisticsManagerToken 'POST' "/dispatches/$($allocation.id)" @{ decisionId = $ApprovedResourceDecisionId }
if (-not $dispatch.id) { throw 'Dispatch response did not include an id.' }
Write-Host "Dispatch: $($dispatch.id) [$($dispatch.status)]"
$delivery = Invoke-CmsApi $LogisticsManagerToken 'POST' "/dispatches/$($dispatch.id)/confirm" @{ decisionId = $ApprovedResourceDecisionId }
if ($delivery.status -ne 'Fulfilled') { throw "Expected delivery status Fulfilled, received '$($delivery.status)'." }
Write-Host "Delivery confirmation: PASS [$($delivery.status)]" -ForegroundColor Green

Write-Host '2/3 Shelter workflow: approved shelter decision -> guarded occupancy allocation' -ForegroundColor Cyan
$shelterAllocation = Invoke-CmsApi $ShelterAdminToken 'POST' '/shelters/allocations' @{ decisionId = $ApprovedShelterDecisionId }
if (-not $shelterAllocation.id) { throw 'Shelter allocation response did not include an id.' }
Write-Host "Shelter allocation: $($shelterAllocation.id) for $($shelterAllocation.householdSize) people" -ForegroundColor Green

Write-Host '3/3 Volunteer workflow: profile -> availability -> verification -> assignment -> accept -> complete' -ForegroundColor Cyan
$now = [DateTime]::UtcNow
$profile = Invoke-CmsApi $VolunteerToken 'POST' '/volunteers/profile' @{
    skillsJson = '["Food distribution","Logistics"]'; languagesJson = '["English","Sinhala"]';
    lat = 7.2088; lng = 79.8358; active = $true
}
if (-not $profile.id) { throw 'Volunteer profile response did not include an id.' }
# The API rejects availability windows that start in the past. Open the window a
# few seconds from now, then let it become active before attempting assignment.
$starts = $now.AddSeconds(5).ToString('o'); $ends = $now.AddDays(7).ToString('o')
Invoke-CmsApi $VolunteerToken 'POST' '/volunteers/availability' @{ startsAt = $starts; endsAt = $ends } | Out-Null
Invoke-CmsApi $VolunteerCoordinatorToken 'POST' "/volunteers/$($profile.id)/verify" @{ approved = $true; reason = 'Automated Dev4 LocalDB verification run' } | Out-Null
Start-Sleep -Seconds 6
$task = Invoke-CmsApi $VolunteerCoordinatorToken 'POST' '/volunteer-tasks' @{
    requestId = $null; title = 'Dev4 end-to-end verification task';
    requiredSkillsJson = '["Food distribution"]'; lat = 7.2088; lng = 79.8358; scheduledAt = $null
}
if (-not $task.id) { throw 'Volunteer task response did not include an id.' }
Invoke-CmsApi $VolunteerCoordinatorToken 'POST' "/volunteer-tasks/$($task.id)/assign?volunteerId=$($profile.id)" | Out-Null
Invoke-CmsApi $VolunteerToken 'POST' "/volunteer-tasks/$($task.id)/respond" @{ accept = $true } | Out-Null
$completed = Invoke-CmsApi $VolunteerToken 'POST' "/volunteer-tasks/$($task.id)/complete"
if ($completed.status -ne 'Completed') { throw "Expected volunteer task Completed, received '$($completed.status)'." }
Write-Host "Volunteer task: PASS [$($completed.status)]" -ForegroundColor Green

Write-Host 'END-TO-END WORKFLOW PASS. Check audit events, outbox delivery, request status transitions and the UI manually; this script does not claim those asynchronous side effects have been consumed.' -ForegroundColor Green
