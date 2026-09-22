param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\NomadicMethod\Assets\exercises.json'),
    [string]$AuditPath = (Join-Path $PSScriptRoot '..\docs\catalog-audit\exercise_usability_audit.csv'),
    [string]$ReplacementPath = (Join-Path $PSScriptRoot '..\docs\catalog-audit\catalog_replacements.csv')
)

$ErrorActionPreference = 'Stop'

function New-ClarityReplacement {
    param(
        [int]$Id,
        [string]$Name,
        [string]$Practice,
        [string]$MotionProfile,
        [string]$SideSequence,
        [string]$File,
        [string]$Url,
        [double]$StartSeconds,
        [double]$DurationSeconds,
        [int]$FramesPerSecond = 8,
        [string]$Crop = 'crop=ih:ih:(iw-ih)/2:0',
        [bool]$PingPong = $false,
        [string]$Mode = 'Repetition',
        [string]$Presentation = 'Motion',
        [int]$HoldFramePercent = 0,
        [string]$Review = 'A full human demonstration shows the complete named movement without equipment, support, jumping, travel outside three metres, or hidden resistance.'
    )

    [pscustomobject]@{
        Id = $Id
        Name = $Name
        Practice = $Practice
        MotionProfile = $MotionProfile
        SideSequence = $SideSequence
        File = $File
        Url = $Url
        StartSeconds = $StartSeconds
        DurationSeconds = $DurationSeconds
        FramesPerSecond = $FramesPerSecond
        Crop = $Crop
        PingPong = $PingPong
        Mode = $Mode
        Presentation = $Presentation
        HoldFramePercent = $HoldFramePercent
        Review = $Review
    }
}

$sixUrl = 'https://www.youtube.com/watch?v=6P_JPNPgXig'
$eleniUrl = 'https://www.youtube.com/watch?v=2_lCvBvHRFI'
$madFitUrl = 'https://www.youtube.com/watch?v=kfP_9z-BtmA'
$karateUrl = 'https://www.youtube.com/watch?v=MZWdIHO75hU'
$handUrl = 'https://www.youtube.com/watch?v=ZT4lKQ1GcEc'
$standingUrl = 'https://www.youtube.com/watch?v=FGB_9YVUmfY'

$rows = @(
    New-ClarityReplacement 15 'Alternating Standing Hamstring Curls' 'Bodyweight conditioning' 'StandingHamstringCurl' 'Continuous' 'FGB_9YVUmfY.mp4' $standingUrl 15 25
    New-ClarityReplacement 16 'Split-Stance Toe Raises' 'Standing ankle strengthening' 'ToeRaise' 'ScreenRightThenLeft' 'w3wvasIXwP0.mp4' 'https://www.youtube.com/watch?v=w3wvasIXwP0' 2 9 10
    New-ClarityReplacement 17 'Standing Toe-Touch Windmill' 'Standing mobility' 'ToeTouchWindmill' 'Continuous' 'FGB_9YVUmfY.mp4' $standingUrl 135 25
    New-ClarityReplacement 19 'Wide Plie Squat Pulses' 'Ballet conditioning' 'PliePulse' 'Continuous' '2_lCvBvHRFI.mp4' $eleniUrl 45 20
    New-ClarityReplacement 20 'Standing Rear-Leg Pulses' 'Standing lower-body conditioning' 'RearLegPulse' 'ScreenLeftThenRight' '2_lCvBvHRFI.mp4' $eleniUrl 285 20
    New-ClarityReplacement 31 'Single-Side Knee Raise with Two-Arm Pull-Down' 'Low-impact conditioning' 'KneeRaiseTwoArmPulldown' 'ScreenRightThenLeft' '6P_JPNPgXig.mp4' $sixUrl 134 12
    New-ClarityReplacement 47 'Tandem-Stance Head Turns' 'Balance and vestibular training' 'TandemHeadTurn' 'ScreenRightThenLeft' 'qH4_IVoWptU.mp4' 'https://www.youtube.com/watch?v=qH4_IVoWptU' 12 10
    New-ClarityReplacement 97 'Standing Side-Kick Reach' 'Balance training' 'SideKickReach' 'ScreenRightThenLeft' '6P_JPNPgXig.mp4' $sixUrl 873 12
    New-ClarityReplacement 107 'Wide Plie Squats' 'Ballet conditioning' 'PlieSquat' 'Continuous' '2_lCvBvHRFI.mp4' $eleniUrl 15 20
    New-ClarityReplacement 135 'Standing Snow Angels' 'Shoulder mobility' 'StandingSnowAngel' 'Continuous' 'ynsG-MumciA.mp4' 'https://www.youtube.com/watch?v=ynsG-MumciA' 1 10 10
    New-ClarityReplacement 150 'Wide-Squat Side-to-Side Shifts' 'Standing strength and mobility' 'WideSquatShift' 'Continuous' '2_lCvBvHRFI.mp4' $eleniUrl 465 20
    New-ClarityReplacement 169 'High-Knee Pulldown March' 'Low-impact conditioning' 'HighKneePulldown' 'Continuous' 'FGB_9YVUmfY.mp4' $standingUrl 75 25
    New-ClarityReplacement 179 'Hip-Hinge Rear-Leg Raises' 'Standing posterior-chain conditioning' 'HipHingeRearLegRaise' 'ScreenLeftThenRight' '2_lCvBvHRFI.mp4' $eleniUrl 315 20
    New-ClarityReplacement 180 'Standing Front Snap Kicks' 'Karate' 'FrontSnapKick' 'ScreenRightThenLeft' 'chuck-norris-private-lesson.mp4' 'https://www.youtube.com/watch?v=TIo6D2CO6uI' 1050.4 10.4 8
    New-ClarityReplacement 193 'Wide-Squat Floor-to-Overhead Reach' 'Standing mobility' 'SquatOverheadReach' 'Continuous' '6P_JPNPgXig.mp4' $sixUrl 248 12
    New-ClarityReplacement 219 'Alternating High-Knee Cross-Body Pull' 'Low-impact conditioning' 'HighKneeCrossBodyPull' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 759 12
    New-ClarityReplacement 220 'Karate Rising Block' 'Karate' 'RisingBlock' 'ScreenRightThenLeft' 'MZWdIHO75hU.mp4' $karateUrl 15 24
    New-ClarityReplacement 230 'Prayer Press Raise' 'Standing upper-body conditioning' 'PrayerPressRaise' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 226 22
    New-ClarityReplacement 239 'Standing Reverse Prayer Stretch' 'Standing hand and wrist stretching' 'ReversePrayerStretch' 'Continuous' 'reverse-prayer-Svrqn92TzIA.mp4' 'https://www.youtube.com/watch?v=Svrqn92TzIA' 4 16 8 'crop=ih:ih:(iw-ih)/2:0' $false 'Hold' 'Still' 60
    New-ClarityReplacement 241 'Isometric Palm Press Hold' 'Bodyweight isometrics' 'PalmPressIsometric' 'Continuous' 'palm-press-anxpxp0rbHs.mp4' 'https://www.youtube.com/watch?v=anxpxp0rbHs' 0 9.5 10 'crop=ih*3/4:ih*3/4:iw*0.18:ih*0.1' $false 'Hold' 'Still' 50
    New-ClarityReplacement 242 'Full-Fist Tendon Glide' 'Hand therapy' 'FullFistTendonGlide' 'ScreenRightThenLeft' 'ZT4lKQ1GcEc.mp4' $handUrl 166 9 10 'crop=ih:ih:(iw-ih)/2:0' $true
    New-ClarityReplacement 248 'Side-Tap Palm Pushes' 'Low-impact conditioning' 'SideTapPalmPush' 'ScreenRightThenLeft' '6P_JPNPgXig.mp4' $sixUrl 1043 12
    New-ClarityReplacement 251 'Forward Fold to Overhead Reach' 'Standing mobility' 'ForwardFoldOverheadReach' 'Continuous' 'forward-fold-to-overhead-reach.mp4' 'https://www.youtube.com/watch?v=Busj2ROaneY' 1.76 4 10 'crop=ih:ih:(iw-ih)/2:0' $false 'Repetition' 'Motion' 0 'A full shod human repeatedly moves from a complete forward fold through a broad arm sweep to an overhead reach and back without travel, equipment, or support.'
    New-ClarityReplacement 256 'Bent-Over Straight-Arm Lat Sweeps' 'Standing strength and mobility' 'LatSweep' 'Continuous' 'oRRKLd8dGUQ.mp4' 'https://www.youtube.com/watch?v=oRRKLd8dGUQ' 6 8 10
    New-ClarityReplacement 257 'Karate Knife-Hand Block' 'Karate' 'KnifeHandBlock' 'ScreenRightThenLeft' 'MZWdIHO75hU.mp4' $karateUrl 150 20
    New-ClarityReplacement 258 'Karate Downward Block' 'Karate' 'DownwardBlock' 'ScreenRightThenLeft' 'MZWdIHO75hU.mp4' $karateUrl 116 24
    New-ClarityReplacement 262 'Standing Bicycle Crunches' 'Standing core conditioning' 'StandingBicycleCrunch' 'Continuous' 'QDezRfZvzcQ.mp4' 'https://www.youtube.com/watch?v=QDezRfZvzcQ' 0.5 9 10
    New-ClarityReplacement 266 'Alternating T-Arm Lifts' 'Standing shoulder endurance' 'AlternatingTArmLift' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 158 24
    New-ClarityReplacement 268 'Goalpost-to-T Rotations' 'Standing shoulder mobility' 'GoalpostTRotation' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 64 24
    New-ClarityReplacement 269 'C-Rotation Arm Curls' 'Standing arm conditioning' 'CRotationCurl' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 31 24
    New-ClarityReplacement 270 'Goalpost Elbow Open-and-Close' 'Standing chest conditioning' 'GoalpostElbowOpenClose' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 126 24
    New-ClarityReplacement 275 'Backward Arm Circles' 'Shoulder endurance' 'ArmCircle' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 190 24
    New-ClarityReplacement 278 'Staggered-Stance Straight Punch' 'Boxing conditioning' 'StraightPunch' 'ScreenRightThenLeft' 'tTkZ9oNMwXs.mp4' 'https://www.youtube.com/watch?v=tTkZ9oNMwXs' 5 10 10
    New-ClarityReplacement 279 'Staggered-Stance Jab-Cross' 'Karate conditioning' 'JabCross' 'ScreenRightThenLeft' 'chuck-norris-private-lesson.mp4' 'https://www.youtube.com/watch?v=TIo6D2CO6uI' 544.45 13.8 8
    New-ClarityReplacement 282 'Side-Step Knee Drive with Alternating Side Punches' 'Low-impact boxing conditioning' 'SideStepKneeDriveSidePunch' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 702 12
    New-ClarityReplacement 283 'Alternating Palm Strikes' 'Karate conditioning' 'PalmStrike' 'Alternating' 'palm-strikes-rfX6clqMrb4.mp4' 'https://www.youtube.com/watch?v=rfX6clqMrb4' 0 8.7 10 'crop=ih:ih:iw-ih:0'
    New-ClarityReplacement 285 'Karate Inside Block' 'Karate' 'InsideBlock' 'ScreenRightThenLeft' 'MZWdIHO75hU.mp4' $karateUrl 82 24
    New-ClarityReplacement 286 'Karate Outside Block' 'Karate' 'OutsideBlock' 'ScreenRightThenLeft' 'MZWdIHO75hU.mp4' $karateUrl 47 24
    New-ClarityReplacement 287 'Wide-Stance Alternating Uppercuts' 'Boxing conditioning' 'Uppercut' 'Alternating' 'cardio-uppercuts.mp4' 'https://www.youtube.com/watch?v=zIuEdGhLtdY' 20 16 10
    New-ClarityReplacement 291 'Inward Knife-Hand Strikes' 'Karate conditioning' 'InwardKnifeHandStrike' 'Alternating' 'knife-hand-wO_WUzVXyWc.mp4' 'https://www.youtube.com/watch?v=wO_WUzVXyWc' 0 12.6 10 'crop=ih:ih:iw-ih:0'
    New-ClarityReplacement 294 'Outward Knife-Hand Strikes' 'Karate conditioning' 'OutwardKnifeHandStrike' 'Alternating' 'outward-knife-hand-hvW4IPvBU3E.mp4' 'https://www.youtube.com/watch?v=hvW4IPvBU3E' 0 11.4 10 'crop=ih:ih:iw-ih:0'
    New-ClarityReplacement 314 'Alternating Forward Lunge Pulses' 'Bodyweight conditioning' 'ForwardLungePulse' 'Continuous' 'FGB_9YVUmfY.mp4' $standingUrl 190 25
    New-ClarityReplacement 321 'Side-Tap Alternating Arm Raises' 'Low-impact conditioning' 'SideTapArmRaise' 'Continuous' '6P_JPNPgXig.mp4' $sixUrl 191 12
    New-ClarityReplacement 326 'Rear-Hand Straight Punch' 'Boxing conditioning' 'RearHandStraightPunch' 'ScreenRightThenLeft' 'tTkZ9oNMwXs.mp4' 'https://www.youtube.com/watch?v=tTkZ9oNMwXs' 18 10 10
    New-ClarityReplacement 329 'Standing Shoulder CAR' 'Shoulder mobility' 'ShoulderCAR' 'ScreenRightThenLeft' 'GRHohA9PX_U.mp4' 'https://www.youtube.com/watch?v=GRHohA9PX_U' 1 12 10
    New-ClarityReplacement 390 'Inhale Arms Up, Exhale Step-Touch' 'Breath-led mobility' 'StepTouchArmArc' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 21 12
    New-ClarityReplacement 391 'Inhale Arms Open, Exhale High-Knee' 'Breath-led conditioning' 'HighKneeOpenArmMarch' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 475 12
    New-ClarityReplacement 394 'Inhale Open, Exhale Cross-Body Knee' 'Breath-led conditioning' 'HighKneeCrossBodySweep' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 645 12
    New-ClarityReplacement 395 'Single-Side Inhale Reach Up, Exhale Knee Lift' 'Breath-led mobility' 'KneeLiftOverheadReach' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 361 12
    New-ClarityReplacement 396 'Single-Leg Knee-Lift Balance Hold' 'Standing balance' 'KneeLiftBalance' 'ScreenLeftThenRight' '2_lCvBvHRFI.mp4' $eleniUrl 135 20 8 'crop=ih:ih:(iw-ih)/2:0' $false 'Hold' 'Still' 50
    New-ClarityReplacement 397 'Alternating Side Tap with Diagonal Arm Sweep' 'Low-impact conditioning' 'SideTapCrossBodySweep' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 78 12
    New-ClarityReplacement 425 'Feet-Together Head Turns' 'Balance and vestibular training' 'FeetTogetherHeadTurn' 'Continuous' 'candidate-narrow-head-turns-tilts.mp4' 'https://www.youtube.com/watch?v=81sfTXQ6zjc' 0 9 10
    New-ClarityReplacement 507 'Alternating Cross-Body Knee-to-Elbow Crunch' 'Standing core conditioning' 'CoreCrunch' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 532 12
    New-ClarityReplacement 508 'Side-Step with Two-Arm Overhead Reach' 'Standing upper-body conditioning' 'SideStepOverheadReach' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 816 12
    New-ClarityReplacement 513 'Single-Leg Head Nods' 'Vestibular rehabilitation' 'SingleLegHeadNod' 'ScreenLeftThenRight' 'candidate-single-leg-head-turns-b3.mp4' 'https://www.youtube.com/watch?v=mgZaPNn8_PE' 32.2 7.2 10
    New-ClarityReplacement 516 'Bent-Elbow Shoulder Circles' 'Shoulder mobility' 'BentElbowShoulderCircle' 'Continuous' 'OJs2AfEUzaA.mp4' 'https://www.youtube.com/watch?v=OJs2AfEUzaA' 4 5.6 10 'crop=ih*0.82:ih*0.82:(iw-ih*0.82)/2:ih*0.18'
    New-ClarityReplacement 572 'Cossack Side-to-Side Shifts' 'Standing adductor mobility' 'CossackShift' 'Continuous' '2_lCvBvHRFI.mp4' $eleniUrl 435 20
    New-ClarityReplacement 576 'Side-Step Rainbow Reach' 'Low-impact conditioning' 'SideStepRainbowReach' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 419 12
    New-ClarityReplacement 577 'Single-Side Standing Side-Leg Raise with Side Reach' 'Low-impact conditioning' 'SideLegRaiseSideReach' 'ScreenRightThenLeft' '6P_JPNPgXig.mp4' $sixUrl 929 12
    New-ClarityReplacement 615 'Hamstring Curl with Prayer Hands' 'Standing pelvic mobility' 'HamstringCurlPrayer' 'Continuous' '6P_JPNPgXig.mp4' $sixUrl 1100 12
    New-ClarityReplacement 618 'Single-Side High-Knee Hold with Side Reach' 'Standing balance and mobility' 'HighKneeSideReach' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 986 12
    New-ClarityReplacement 677 'T-Arm Side-to-Side Sweep' 'Standing upper-back conditioning' 'TArmSideSweep' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 128 22
    New-ClarityReplacement 683 'Alternating Palm-Up T-Arm Flips' 'Shoulder mobility' 'AlternatingPalmFlip' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 286 24
    New-ClarityReplacement 685 'Static-Stance Karate Reverse Punch' 'Karate' 'ReversePunch' 'ScreenRightThenLeft' 'chuck-norris-private-lesson.mp4' 'https://www.youtube.com/watch?v=TIo6D2CO6uI' 449.7 5.5 8
    New-ClarityReplacement 745 'Standing Overhead Presses' 'Standing upper-body conditioning' 'OverheadPress' 'Continuous' 'kfP_9z-BtmA.mp4' $madFitUrl 318 24
    New-ClarityReplacement 816 'Side-Step Overhead Reach' 'Standing core conditioning' 'SideStepOverheadReach' 'Alternating' '6P_JPNPgXig.mp4' $sixUrl 589 12
    New-ClarityReplacement 834 'Single-Side Diagonal Knee Drive with Overhead Pull' 'Standing upper-body conditioning' 'DiagonalKneeOverheadPull' 'ScreenLeftThenRight' '6P_JPNPgXig.mp4' $sixUrl 305 12
)

if ($rows.Count -lt 67) {
    throw "The clarity reset must replace at least 20 percent of the 333-entry catalog; only $($rows.Count) rows were supplied."
}

$duplicateIds = @($rows | Group-Object Id | Where-Object Count -gt 1)
$duplicateNames = @($rows | Group-Object Name | Where-Object Count -gt 1)
if ($duplicateIds.Count -gt 0 -or $duplicateNames.Count -gt 0) {
    throw 'The clarity reset contains duplicate IDs or names.'
}

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$catalogById = @{}
foreach ($exercise in $catalog) {
    $catalogById[[int]$exercise.id] = $exercise
}

$regions = @('FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD', 'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')
$baselineNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'RealExerciseCatalog.psd1') -SkipLimitCheck
$bilateralNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'BilateralExerciseNames.psd1') -SkipLimitCheck
$baselineSides = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSideSequences.psd1') -SkipLimitCheck
$baselineSideById = @{}
foreach ($entry in $baselineSides.GetEnumerator()) {
    $baselineSideById[[int]$entry.Key] = [string]$entry.Value
}
$reviewedContinuous = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ReviewedContinuousExercises.psd1')
$reviewedContinuousIds = @($reviewedContinuous.Ids | ForEach-Object { [int]$_ })
$directionSequences = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseDirectionSequences.psd1') -SkipLimitCheck
$retiredDirectionOnlyIds = @(816)

$audit = @(Import-Csv -LiteralPath $AuditPath)
$auditById = @{}
foreach ($row in $audit) {
    $auditById[[int]$row.id] = $row
}

$replacements = @(Import-Csv -LiteralPath $ReplacementPath)
$replacementById = @{}
foreach ($row in $replacements) {
    $replacementById[[int]$row.id] = $row
}

foreach ($replacement in $rows) {
    $id = [int]$replacement.Id
    if (-not $catalogById.ContainsKey($id) -or -not $auditById.ContainsKey($id)) {
        throw "Clarity reset references missing catalog/audit ID $id."
    }

    $current = $catalogById[$id]
    $auditRow = $auditById[$id]
    $regionIndex = [Math]::Floor(($id - 1) / 100)
    $movementIndex = ($id - 1) % 100
    $baselineName = if ($bilateralNames.ContainsKey($id)) {
        [string]$bilateralNames[$id]
    }
    else {
        [string]@($baselineNames[$regions[$regionIndex]])[$movementIndex]
    }
    $baselineSide = if ($baselineSideById.ContainsKey($id)) {
        [string]$baselineSideById[$id]
    }
    elseif ($id -in $reviewedContinuousIds -or
            $directionSequences.ContainsKey($id) -or
            $id -in $retiredDirectionOnlyIds) {
        'Continuous'
    }
    else {
        throw "Exercise $id has no baseline side-sequence decision."
    }
    if ($baselineSide -ne 'Continuous' -and
        $baselineName.StartsWith('Alternating ', [StringComparison]::Ordinal)) {
        $baselineName = $baselineName.Substring('Alternating '.Length)
    }

    $auditRow.name = $baselineName
    $auditRow.decision = 'REMOVE'
    $auditRow.user_thought = 'The name and silent demonstration are unclear, overly specialist, hidden-effort, trivial, or visually mismatched.'
    $auditRow.observed_demo = "The reviewed entry is retired in favor of $($replacement.Name)."
    $auditRow.reason = 'The replacement is a worthwhile named movement whose complete action is visible and copyable at a glance.'

    $secondary = @($current.secondaryCanonicalGroups) -join '|'
    $record = [ordered]@{
        id = [string]$id
        retired_name = $baselineName
        name = $replacement.Name
        practice = $replacement.Practice
        motion_profile = $replacement.MotionProfile
        primary = [string]$current.primaryCanonicalGroup
        secondary = $secondary
        side_sequence = $replacement.SideSequence
        mode = $replacement.Mode
        presentation = $replacement.Presentation
        hold_frame_percent = [string]$replacement.HoldFramePercent
        media_source_id = ''
        file = $replacement.File
        url = $replacement.Url
        source_page = $replacement.Url
        start_seconds = [Convert]::ToString($replacement.StartSeconds, [Globalization.CultureInfo]::InvariantCulture)
        duration_seconds = [Convert]::ToString($replacement.DurationSeconds, [Globalization.CultureInfo]::InvariantCulture)
        frames_per_second = [string]$replacement.FramesPerSecond
        crop = $replacement.Crop
        ping_pong = $replacement.PingPong.ToString().ToLowerInvariant()
        mirror_for_alternation = 'false'
        review = $replacement.Review
    }
    $replacementById[$id] = [pscustomobject]$record
}

$auditById.Values |
    Sort-Object { [int]$_.id } |
    Export-Csv -LiteralPath $AuditPath -NoTypeInformation -Encoding utf8
$replacementById.Values |
    Sort-Object { [int]$_.id } |
    Export-Csv -LiteralPath $ReplacementPath -NoTypeInformation -Encoding utf8

Write-Host "Clarity replacements applied: $($rows.Count)"
Write-Host "Audit: $([IO.Path]::GetFullPath($AuditPath))"
Write-Host "Replacements: $([IO.Path]::GetFullPath($ReplacementPath))"
