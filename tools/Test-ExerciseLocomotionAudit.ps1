param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '../NomadicMethod/Assets/exercises.json')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseLocomotionSpaces.ps1')
$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$byId = @{}
$movements = @{}
foreach ($exercise in $catalog) {
    $byId[[int]$exercise.id] = $exercise
    if ([int]$exercise.sessionMovementId -gt 0) {
        $movements[[int]$exercise.id] = [int]$exercise.sessionMovementId
    }
}
$review = Get-ExerciseLocomotionReview -RetainedExerciseIds @($byId.Keys) -SessionMovementByExerciseId $movements
foreach ($exercise in $catalog) {
    $id = [int]$exercise.id
    $space = [string]$exercise.locomotionSpace
    $meters = [int]$exercise.maxSpaceMeters
    $valid = switch ($space) {
        None { $meters -ge 1 -and $meters -le 2 }
        Room { $meters -ge 3 -and $meters -le 10 }
        Field { $meters -gt 10 -and $meters -le 100 }
        default { $false }
    }
    if ($space -cne $review[$id] -or -not $valid) {
        throw "Exercise $id disagrees with its reviewed travel-space requirement."
    }
    foreach ($block in $exercise.sequenceBlocks) {
        if (-not $byId.ContainsKey([int]$block.exerciseId) -or
            [string]$byId[[int]$block.exerciseId].locomotionSpace -cne $space) {
            throw "Atomic sequence $id disagrees about its required travel space."
        }
    }
}
foreach ($space in @('Room', 'Field')) {
    $movementIds = @($catalog | Where-Object {
        [string]$_.locomotionSpace -ceq $space -and @($_.sequenceBlocks).Count -gt 0
    } | ForEach-Object {
        if ([int]$_.sessionMovementId -gt 0) { [int]$_.sessionMovementId } else { [int]$_.id }
    } | Sort-Object -Unique)
    $minimum = if ($space -ceq 'Room') { 25 } else { 5 }
    if ($movementIds.Count -lt $minimum) { throw "$space travel has fewer than $minimum complete session movements." }
    Write-Output "$space locomotion: $($movementIds.Count) distinct approved session movements (minimum $minimum)."
}
