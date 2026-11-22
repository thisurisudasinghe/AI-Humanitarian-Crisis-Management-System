[CmdletBinding()]
param(
    [string]$CollectionPath = (Join-Path $PSScriptRoot '..\..\docs\postman\Member4_Operations.postman_collection.json')
)

$ErrorActionPreference = 'Stop'
$fullPath = [IO.Path]::GetFullPath($CollectionPath)
if (-not (Test-Path $fullPath)) {
    throw "Postman collection not found: $fullPath"
}

$collection = Get-Content $fullPath -Raw | ConvertFrom-Json
$requests = [System.Collections.Generic.List[object]]::new()

function Walk-Items($items) {
    foreach ($item in $items) {
        if ($null -ne $item.request) {
            $raw = if ($item.request.url.raw) { $item.request.url.raw } else { [string]$item.request.url }
            $requests.Add([pscustomobject]@{
                Name   = $item.name
                Method = $item.request.method
                Url    = $raw
            })
        }
        elseif ($null -ne $item.item) {
            Walk-Items $item.item
        }
    }
}

Walk-Items $collection.item

Write-Host "Collection: $($collection.info.name)"
Write-Host "Requests: $($requests.Count)"
$requests | ForEach-Object { Write-Host ("  {0,-6} {1}" -f $_.Method, $_.Url) }

if ($requests.Count -ne 41) {
    throw "Expected 41 requests (40 owned P1 endpoints plus one negative idempotency-key probe); found $($requests.Count)."
}

$required = @(
    '/api/v1/allocations',
    '/api/v1/shelters/allocations',
    '/api/v1/volunteer-tasks',
    '/api/v1/geo/geocode',
    '/api/v1/geo/distance'
)
foreach ($path in $required) {
    if (-not ($requests.Url | Where-Object { $_ -like "*$path*" })) {
        throw "Missing required endpoint in collection: $path"
    }
}

$negative = @($requests | Where-Object { $_.Url -like '*/api/v1/allocations' -and $_.Method -eq 'POST' })
if ($negative.Count -lt 2) {
    throw 'Missing negative Idempotency-Key probe on POST /allocations.'
}

Write-Host 'PASS: static Postman collection validation.'
