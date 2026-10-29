param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$corrections = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'ExerciseExecutionCorrections.psd1')
$cases = @(Get-Content (Join-Path $root 'web/tests/fixtures/execution-protocol-cases.json') -Raw | ConvertFrom-Json)
$poseCases = @(Get-Content (Join-Path $root 'web/tests/fixtures/catalog-position-cases.json') -Raw | ConvertFrom-Json)
$catalog = @(Get-Content (Join-Path $root 'NomadicMethod/Assets/exercises.json') -Raw | ConvertFrom-Json)
$expectedIds = @($cases.id) + @($poseCases.id)
if (@(Compare-Object @($corrections.Keys | ForEach-Object { [int]$_ } | Sort-Object) @($expectedIds | Sort-Object)).Count) {
    throw 'Reviewed execution corrections differ from the shared Android/web contract.'
}
$archive = Join-Path $root 'NomadicMethod/Assets/exercise_videos/revision82'
$expectedNames = @($cases | ForEach-Object { 'exercise_{0:D4}.mp4' -f [int]$_.id })
$actualNames = @(Get-ChildItem $archive -File | Select-Object -ExpandProperty Name)
if (@(Compare-Object ($expectedNames | Sort-Object) ($actualNames | Sort-Object)).Count) {
    throw 'Legacy execution media contains missing or orphaned files.'
}
foreach ($case in $cases) {
    $id = [int]$case.id
    $review = $corrections[$id]
    $exercise = $catalog | Where-Object id -eq $id
    if (@($exercise).Count -ne 1 -or
        $exercise.sideSequence -cne $case.side -or
        $review.SideSequence -cne $case.side -or
        $exercise.directionSequence -cne $case.direction -or
        $review.DirectionSequence -cne $case.direction -or
        @($exercise.sequenceBlocks).Count -ne $case.blocks) {
        throw "Exercise $id lost its reviewed complete execution protocol."
    }
    $video = Join-Path $archive ('exercise_{0:D4}.mp4' -f $id)
    if ((Get-FileHash $video -Algorithm SHA256).Hash.ToLowerInvariant() -cne $case.previousVideoSha256) {
        throw "Exercise $id changed the footage owned by an unfinished workout."
    }
}
foreach ($case in $poseCases) {
    $id = [int]$case.id
    $exercise = @($catalog | Where-Object id -eq $id)
    if ($exercise.Count -ne 1 -or
        $exercise[0].sideSequence -cne $case.sideSequence -or
        $corrections[$id].SideSequence -cne $case.sideSequence -or
        $corrections[$id].DirectionSequence -cne 'None') {
        throw "Exercise $id lost its reviewed whole-pose correction."
    }
}
Write-Output "Execution protocols audited: $($cases.Count) earlier and $($poseCases.Count) whole-pose corrections; original workout footage preserved byte-for-byte."
