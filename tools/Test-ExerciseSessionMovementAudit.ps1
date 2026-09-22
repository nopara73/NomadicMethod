param(
    [string]$ReviewPath = (
        Join-Path $PSScriptRoot 'ExerciseSessionMovements.psd1'),
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
$families = $review.Families
if ($families -isnot [System.Collections.IDictionary] -or
    $families.Count -eq 0) {
    throw 'The session-movement audit must define at least one family.'
}

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$catalogById = @{}
foreach ($exercise in $catalog) {
    $exerciseId = [int]$exercise.id
    if ($catalogById.ContainsKey($exerciseId)) {
        throw "The packaged catalog repeats exercise ID $exerciseId."
    }
    $catalogById[$exerciseId] = $exercise
}

$reviewedMovementByExerciseId = @{}
foreach ($entry in $families.GetEnumerator()) {
    $rootId = 0
    if (-not [int]::TryParse([string]$entry.Key, [ref]$rootId) -or
        $rootId -le 0) {
        throw "Invalid session-movement root '$($entry.Key)'."
    }

    $memberIds = @($entry.Value | ForEach-Object { [int]$_ })
    if ($memberIds.Count -lt 2 -or
        @($memberIds | Sort-Object -Unique).Count -ne $memberIds.Count -or
        $rootId -notin $memberIds) {
        throw "Session-movement family $rootId must contain its root and at least one unique alias."
    }

    foreach ($exerciseId in $memberIds) {
        if (-not $catalogById.ContainsKey($exerciseId)) {
            throw "Session-movement family $rootId references missing exercise $exerciseId."
        }
        if ($reviewedMovementByExerciseId.ContainsKey($exerciseId)) {
            throw "Exercise $exerciseId belongs to more than one session-movement family."
        }
        $reviewedMovementByExerciseId[$exerciseId] = $rootId
    }

    $root = $catalogById[$rootId]
    $rootCanonicalGroups = @(
        [string]$root.primaryCanonicalGroup
        @($root.secondaryCanonicalGroups | ForEach-Object { [string]$_ }))
    foreach ($exerciseId in $memberIds) {
        $exercise = $catalogById[$exerciseId]
        $exerciseCanonicalGroups = @(
            [string]$exercise.primaryCanonicalGroup
            @($exercise.secondaryCanonicalGroups |
                ForEach-Object { [string]$_ }))
        if ([int]$exercise.sessionMovementId -ne $rootId -or
            @($exerciseCanonicalGroups | Where-Object {
                    $_ -in $rootCanonicalGroups }).Count -eq 0) {
            throw "Session-movement family $rootId has drifted in the packaged catalog."
        }
    }
}

foreach ($exercise in $catalog) {
    $exerciseId = [int]$exercise.id
    $packagedMovementId = if ($null -eq $exercise.sessionMovementId) {
        0
    }
    else {
        [int]$exercise.sessionMovementId
    }
    $reviewedMovementId = if (
        $reviewedMovementByExerciseId.ContainsKey($exerciseId)) {
        [int]$reviewedMovementByExerciseId[$exerciseId]
    }
    else {
        0
    }
    if ($packagedMovementId -ne $reviewedMovementId) {
        throw "Exercise $exerciseId session-movement metadata does not match the audit."
    }
}

Write-Output (
    'Session-movement audit valid: {0} families covering {1} catalog records.' -f
        $families.Count,
        $reviewedMovementByExerciseId.Count)
