param([ValidateSet('precise-admissions-2026-10-23', 'ability-admissions-2026-10-23')]
    [string]$Admission = 'precise-admissions-2026-10-23')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'NomadicMethod/Assets'
$catalog = @(Get-Content (Join-Path $assets 'exercises.json') -Raw | ConvertFrom-Json)
$audit = Get-Content (Join-Path $root "docs/catalog-audit/$Admission.json") -Raw | ConvertFrom-Json -AsHashtable
$expectedCount = if ($Admission -eq 'ability-admissions-2026-10-23') { 1 } else { 16 }
$reviews = Read-CatalogReviewEvidence -Path (Join-Path $root $audit.reviewEvidence)
$assessment = Get-Content (Join-Path $PSScriptRoot 'ExerciseFitnessCeilingScores.json') -Raw | ConvertFrom-Json -AsHashtable
$ratings = Get-Content (Join-Path $assets 'fitness_ratings.json') -Raw | ConvertFrom-Json -AsHashtable
if ($audit.newRecordCount -ne $expectedCount -or @($audit.entries).Count -ne $expectedCount -or $reviews.Count -ne $expectedCount) {
    throw "Expected $expectedCount explicitly reviewed admissions for $Admission."
}
foreach ($entry in $audit.entries) {
    $id = [int]$entry.id
    $exercise = @($catalog | Where-Object id -EQ $id)
    $movementId = if ($exercise.Count -eq 1 -and [int]$exercise[0].sessionMovementId -gt 0) {
        [int]$exercise[0].sessionMovementId
    } else { $id }
    if ($exercise.Count -ne 1 -or $exercise[0].name -cne $entry.name -or
        $movementId -ne [int]$entry.sessionMovementId) {
        throw "Admission identity changed: $id."
    }
    $result = Get-CatalogReviewResult -Exercise $exercise[0] -AssetsRoot $assets -Review $reviews[$id]
    if ($result.Status -ne 'PASS') { throw "Exercise $id requires a new explicit media/metadata review: $($result.Status)." }
    $profiles = @($assessment.profiles.Values | Where-Object { $id -in $_.exerciseIds })
    if ($profiles.Count -ne 1 -or $entry.trainedLevels.Count -ne 10) {
        throw "Exercise $id requires exactly one complete ten-characteristic assessment."
    }
    for ($axis = 0; $axis -lt $assessment.dimensions.Count; $axis++) {
        $dimension = $assessment.dimensions[$axis]
        $levels = $entry.trainedLevels[$dimension]
        if ($null -eq $levels -or $levels.Count -gt 1 -or
            (ConvertTo-Json -InputObject $levels -Compress) -cne
                (ConvertTo-Json -InputObject $profiles[0].levels[$dimension] -Compress) -or
            (ConvertTo-Json -InputObject $levels -Compress) -cne
                (ConvertTo-Json -InputObject $ratings.ratings[[string]$id][$axis] -Compress)) {
            throw "Exercise $id has a range or a changed $dimension assessment; review the admission explicitly."
        }
    }
}
Write-Output "$expectedCount precise admissions match their explicit reviews, canonical identities and all ten runtime rating cells."
