param(
    [string]$ReviewPath = (
        Join-Path $PSScriptRoot 'ExerciseSecondaryTrainingClaims.psd1'),
    [string]$TaxonomyPath = (
        Join-Path $PSScriptRoot 'CanonicalMuscleGroups.psd1'),
    [string]$CatalogPath = (
        [System.IO.Path]::Combine(
            $PSScriptRoot,
            '..',
            'NomadicMethod',
            'Assets',
            'exercises.json'))
)

$ErrorActionPreference = 'Stop'

$review = Import-PowerShellDataFile -LiteralPath $ReviewPath -SkipLimitCheck
$taxonomy = Import-PowerShellDataFile -LiteralPath $TaxonomyPath -SkipLimitCheck
$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)

if (@(Compare-Object `
        @('ByCanonicalGroup', 'RubricVersion') `
        @($review.Keys | ForEach-Object { [string]$_ })).Count -gt 0 -or
    [int]$review.RubricVersion -ne 1 -or
    $review.ByCanonicalGroup -isnot [System.Collections.IDictionary]) {
    throw 'The secondary-training audit must use the reviewed version-1 schema.'
}

$canonicalGroupKeys = @(
    $taxonomy.Groups |
        Sort-Object { [int]$_.Id } |
        ForEach-Object { [string]$_.StableKey })
if (@(Compare-Object `
        @($canonicalGroupKeys | Sort-Object) `
        @($review.ByCanonicalGroup.Keys | ForEach-Object { [string]$_ } |
            Sort-Object)).Count -gt 0) {
    throw 'The secondary-training audit must contain every canonical group exactly once.'
}

$catalogById = @{}
foreach ($exercise in $catalog) {
    $exerciseId = [int]$exercise.id
    if ($catalogById.ContainsKey($exerciseId)) {
        throw "The packaged catalog repeats exercise ID $exerciseId."
    }
    $catalogById[$exerciseId] = $exercise
}

$reviewedClaims = @(
    foreach ($groupKey in $canonicalGroupKeys) {
        foreach ($exerciseId in @(
                $review.ByCanonicalGroup[$groupKey] |
                    ForEach-Object { [int]$_ })) {
            if (-not $catalogById.ContainsKey($exerciseId)) {
                throw "The secondary-training audit references missing exercise $exerciseId."
            }
            "${exerciseId}:$groupKey"
        }
    })
if ($reviewedClaims.Count -ne @($reviewedClaims | Sort-Object -Unique).Count) {
    throw 'The secondary-training audit repeats an exercise and muscle claim.'
}

$packagedClaims = @(
    foreach ($exercise in $catalog) {
        foreach ($groupKey in @(
                $exercise.secondaryCanonicalGroups |
                    ForEach-Object { [string]$_ })) {
            "$([int]$exercise.id):$groupKey"
        }
    })
if (@(Compare-Object `
        @($reviewedClaims | Sort-Object) `
        @($packagedClaims | Sort-Object)).Count -gt 0) {
    throw 'Packaged secondary muscle claims have drifted from the exact training audit.'
}

$missingDirectPrimaryGroups = @(
    $canonicalGroupKeys | Where-Object {
        $groupKey = $_
        -not @($catalog | Where-Object {
                [string]$_.primaryCanonicalGroup -eq $groupKey
            })
    })
if ($missingDirectPrimaryGroups.Count -gt 0) {
    throw "Every canonical muscle group needs direct primary work; missing: $($missingDirectPrimaryGroups -join ', ')."
}

Write-Output (
    'Training-claim audit valid: {0} exact secondary claims and direct primary work for {1} canonical groups.' -f
        $reviewedClaims.Count,
        $canonicalGroupKeys.Count)
