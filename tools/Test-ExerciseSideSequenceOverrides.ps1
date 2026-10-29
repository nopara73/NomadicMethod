param()
$ErrorActionPreference = 'Stop'

# Execute the production replacement override, without generating or modifying
# catalog assets. This guards the stale Alternating membership regression.
$tokens = $null
$parseErrors = $null
$syntax = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Generate-ExerciseCatalog.ps1'),
    [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'The catalog generator has syntax errors.' }
$override = $syntax.Find({
    param($node)
    $node -is [Management.Automation.Language.ForEachStatementAst] -and
        $node.Condition.Extent.Text -eq '$replacementExerciseIds' -and
        $node.Body.Extent.Text.Contains('$alternatingExerciseIds')
}, $false)
if ($null -eq $override) { throw 'Cannot locate the production side override.' }
$declaration = $syntax.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'New-ExerciseSequenceBlocks'
}, $false)
if ($null -eq $declaration) { throw 'Cannot locate production block generation.' }
. ([scriptblock]::Create($declaration.Extent.Text))

$replacementExerciseIds = @(1, 2, 3, 4)
$catalogExerciseReplacements = @{}
$rawExerciseCanonicalGroups = @{}
$externalExerciseMedia = @{}
$exerciseSideSequences = @{ 1 = 'ScreenLeftThenRight'; 4 = 'ScreenRightThenLeft' }
$alternatingExerciseIds = @(1, 2, 3, 99)
$reviewedContinuousExerciseIds = @(1, 2, 3, 99)
$holdExerciseFrames = @{}
$stillExercisePresentations = @{}
$sides = @('Continuous', 'Alternating', 'ScreenRightThenLeft', 'Continuous')
foreach ($id in $replacementExerciseIds) {
    $catalogExerciseReplacements[$id] = @{
        Primary = 'HipFlexors'; Secondary = @(); Media = @{}
        SideSequence = $sides[$id - 1]; Mode = 'Repetition'; Presentation = 'Motion'
    }
    $rawExerciseCanonicalGroups[$id] = @{ Primary = 'HipFlexors'; Secondary = @() }
}
. ([scriptblock]::Create($override.Extent.Text))
if (($alternatingExerciseIds | Sort-Object) -join ',' -ne '2,99') {
    throw 'A replacement retained stale alternating membership or changed an untouched entry.'
}
if (($reviewedContinuousExerciseIds | Sort-Object) -join ',' -ne '1,2,4,99') {
    throw 'Replacement continuous membership is incorrect.'
}
if ($exerciseSideSequences.Count -ne 1 -or $exerciseSideSequences[3] -ne 'ScreenRightThenLeft') {
    throw 'Replacement fixed-side membership is incorrect.'
}
$continuous = @(New-ExerciseSequenceBlocks -ExerciseId 1 -SideSequence 'Continuous' -DirectionSequence 'None')
$alternating = @(New-ExerciseSequenceBlocks -ExerciseId 2 -SideSequence 'Alternating' -DirectionSequence 'None')
$sided = @(New-ExerciseSequenceBlocks -ExerciseId 3 -SideSequence $exerciseSideSequences[3] -DirectionSequence 'None')
if ($continuous.Count -ne 1 -or $continuous[0].sideCue -ne 'None' -or
    $alternating.Count -ne 1 -or $alternating[0].sideCue -ne 'None' -or
    $sided.Count -ne 2 -or $sided[0].sideCue -ne 'ScreenRight' -or
    $sided[0].mirrorMedia -or $sided[1].sideCue -ne 'ScreenLeft' -or
    -not $sided[1].mirrorMedia) {
    throw 'A replacement generated the wrong workout phases.'
}
Write-Output 'Replacement side membership and workout phase regression checks passed.'

$executionOverlay = $syntax.Find({
    param($node)
    $node -is [Management.Automation.Language.ForEachStatementAst] -and
        $node.Condition.Extent.Text -eq '$executionCorrections.GetEnumerator()'
}, $false)
if ($null -eq $executionOverlay) { throw 'Cannot locate the production execution overlay.' }
$executionCorrections = @{
    1 = @{ SideSequence = 'Continuous'; DirectionSequence = 'None' }
    2 = @{ SideSequence = 'Alternating'; DirectionSequence = 'BackwardThenForward' }
    3 = @{ SideSequence = 'ScreenLeftThenRight'; DirectionSequence = 'None' }
    4 = @{ SideSequence = 'Continuous'; DirectionSequence = 'None'; StartSeconds = 1.0; DurationSeconds = 2.8 }
}
$retainedExerciseIds = @(1, 2, 3, 4)
$exerciseDirectionSequences = @{ 1 = 'ClockwiseThenCounterclockwise' }
$originalMedia = @{ LocalSourceFile = 'reviewed.mp4'; StartSeconds = 0.0; DurationSeconds = 9.0 }
$externalExerciseMedia[4] = $originalMedia
. ([scriptblock]::Create($executionOverlay.Extent.Text))
if ($exerciseDirectionSequences.ContainsKey(1) -or
    $exerciseDirectionSequences[2] -ne 'BackwardThenForward' -or
    $exerciseSideSequences[3] -ne 'ScreenLeftThenRight' -or
    ($alternatingExerciseIds | Sort-Object) -join ',' -ne '2,99' -or
    $externalExerciseMedia[4].DurationSeconds -ne 2.8 -or
    $originalMedia.DurationSeconds -ne 9.0) {
    throw 'Execution corrections retained stale phases or modified the original source descriptor.'
}
Write-Output 'Execution overlay replaces stale sides/directions and preserves its source descriptor.'

# The first combined side/direction protocol must contain the full Cartesian
# product. Mirroring a side reverses a screen-clockwise cue as well as footage.
$combined = @(New-ExerciseSequenceBlocks -ExerciseId 1299 `
    -SideSequence 'ScreenLeftThenRight' `
    -DirectionSequence 'ClockwiseThenCounterclockwise')
if ($combined.Count -ne 4 -or
    ($combined.directionCue -join ',') -ne 'Clockwise,Counterclockwise,Counterclockwise,Clockwise' -or
    ($combined.sideCue -join ',') -ne 'ScreenLeft,ScreenLeft,ScreenRight,ScreenRight' -or
    ($combined.mediaSegment -join ',') -ne 'FirstDirection,SecondDirection,FirstDirection,SecondDirection' -or
    $combined[0].mirrorMedia -or $combined[1].mirrorMedia -or
    -not $combined[2].mirrorMedia -or -not $combined[3].mirrorMedia) {
    throw 'Combined sides and directions lost a phase or mislabeled mirrored motion.'
}
Write-Output 'Combined side/direction execution regression checks passed.'

# Alternating repetitions do not alternate an interlaced clasp or ankle order.
$fixedPose = @(New-ExerciseSequenceBlocks -ExerciseId 1223 `
    -SideSequence 'ShownHandAndFootPositionThenOpposite' -DirectionSequence 'None')
if ($fixedPose.Count -ne 2 -or
    ($fixedPose.sideCue -join ',') -ne 'ShownHandAndFootPosition,OppositeHandAndFootPosition' -or
    $fixedPose[0].mirrorMedia -or -not $fixedPose[1].mirrorMedia -or
    @($fixedPose | Where-Object exerciseId -ne 1223).Count -gt 0) {
    throw 'Fixed hand and foot positions must remain one complete mirrored pair.'
}
Write-Output 'Fixed hand and foot position regression checks passed.'
