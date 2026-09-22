param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineCatalogPath,
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\NomadicMethod\Assets\exercises.json'),
    [string]$ReviewEvidencePath,
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\docs\catalog-audit\demonstration_metadata_integrity_current.csv')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')

$baseline = @(Get-Content -LiteralPath $BaselineCatalogPath -Raw | ConvertFrom-Json)
$current = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$baselineById = @{}
$currentById = @{}
foreach ($exercise in $baseline) {
    if ($baselineById.ContainsKey([int]$exercise.id)) { throw 'Duplicate baseline exercise ID.' }
    $baselineById[[int]$exercise.id] = $exercise
}
foreach ($exercise in $current) {
    if ($currentById.ContainsKey([int]$exercise.id)) { throw 'Duplicate current exercise ID.' }
    $currentById[[int]$exercise.id] = $exercise
}
$reviews = Read-CatalogReviewEvidence -Path $ReviewEvidencePath
$assetsRoot = Split-Path -Parent ([IO.Path]::GetFullPath($CatalogPath))
$ids = @(@($baselineById.Keys) + @($currentById.Keys) | Sort-Object -Unique)

$rows = foreach ($id in $ids) {
    $prior = $baselineById[$id]
    $next = $currentById[$id]
    $changes = @()
    if ($null -ne $prior -and $null -ne $next) {
        $properties = @(@($prior.PSObject.Properties.Name) + @($next.PSObject.Properties.Name) |
            Sort-Object -Unique)
        foreach ($property in $properties) {
            $before = ConvertTo-Json -InputObject $prior.$property -Depth 100 -Compress
            $after = ConvertTo-Json -InputObject $next.$property -Depth 100 -Compress
            if ($before -cne $after) { $changes += "${property}: $before => $after" }
        }
    }
    $changeStatus = if ($null -eq $next) { 'RETIRED' }
        elseif ($null -eq $prior) { 'ADDED' }
        elseif ($changes.Count -gt 0) { 'CHANGED' }
        else { 'UNCHANGED' }
    $review = if ($null -ne $next) {
        Get-CatalogReviewResult -Exercise $next -AssetsRoot $assetsRoot -Review $reviews[$id]
    } else { $null }
    $row = [ordered]@{
        ExerciseId = $id
        ChangeStatus = $changeStatus
        Verdict = if ($null -eq $next) { 'RETIRED' } else { $review.Status }
        PreviousName = if ($null -eq $prior) { '<absent>' } else { [string]$prior.name }
        CurrentName = if ($null -eq $next) { '<retired>' } else { [string]$next.name }
        PreviousMetadata = ConvertTo-Json -InputObject $prior -Depth 100 -Compress
        CurrentMetadata = ConvertTo-Json -InputObject $next -Depth 100 -Compress
        ActualFinalDemonstration = if ($null -eq $next) { 'Not present in the current catalog.' }
            else { $review.ActualFinalDemonstration }
        ExactCorrection = $changes -join ' | '
        Reason = if ($null -eq $next) { 'The ID is absent from the current catalog; removal is not a visual verdict.' }
            else { $review.Reason }
        MetadataSha256 = if ($null -eq $next) { '' } else { $review.MetadataSha256 }
        AssetSha256 = if ($null -eq $next) { '{}' }
            else { ConvertTo-Json -InputObject $review.AssetSha256 -Compress }
    }
    foreach ($dimension in $catalogReviewDimensions) {
        $row[$dimension] = if ($null -eq $next) { 'NOT_APPLICABLE' } else { $review.Dimensions[$dimension] }
    }
    [pscustomobject]$row
}
if ($rows.Count -ne $ids.Count) { throw 'Expected exactly one row per current or retired exercise.' }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8
Write-Output "Baseline SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $BaselineCatalogPath).Hash)"
Write-Output "Current SHA-256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $CatalogPath).Hash)"
Write-Output "Ledger: $([IO.Path]::GetFullPath($OutputPath))"
foreach ($group in ($rows | Group-Object Verdict | Sort-Object Name)) {
    Write-Output "$($group.Name): $($group.Count)"
}
