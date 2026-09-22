$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseCatalogDefinitions.ps1')
$definitions = @(Get-ExerciseCatalogDefinitions)
$legacy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'RealExerciseCatalog.psd1') -SkipLimitCheck
$regions = @('FEET', 'LEGS', 'HANDS', 'ARMS', 'HEAD',
    'SHOULDERS', 'HIPS', 'CHEST', 'BACK', 'CORE')
for ($regionIndex = 0; $regionIndex -lt $regions.Count; $regionIndex++) {
    for ($index = 0; $index -lt 100; $index++) {
        $entry = $definitions[$regionIndex * 100 + $index]
        if ($entry.Id -ne $regionIndex * 100 + $index + 1 -or
            $entry.Name -cne $legacy[$regions[$regionIndex]][$index] -or
            $entry.Region -cne $regions[$regionIndex] -or $entry.Additional) {
            throw 'An additional admission changed a historical source identity.'
        }
    }
}
$newExercise = @($definitions | Where-Object Id -eq 1001)
if ($newExercise.Count -ne 1 -or -not $newExercise[0].Additional -or
    $newExercise[0].Name -cne 'Press Raised Knee Inward and Up Against Hands') {
    throw 'The new admission must have its own declared identity.'
}

$fixture = Join-Path ([IO.Path]::GetTempPath()) ('NomadicMethodCatalogDefinitions-' + [guid]::NewGuid())
$null = New-Item -ItemType Directory -Path $fixture
try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RealExerciseCatalog.psd1') -Destination $fixture
    foreach ($additional in @(
        "@{ 999 = @{ Name='Old ID'; Region='LEGS'; Practice='Test'; MotionProfile='Test' } }",
        "@{ '01001' = @{ Name='Alias ID'; Region='LEGS'; Practice='Test'; MotionProfile='Test' } }",
        "@{ 1001 = @{ Name='Missing region'; Practice='Test'; MotionProfile='Test' } }",
        "@{ 1001 = @{ Name='First'; Region='LEGS'; Practice='Test'; MotionProfile='Test' }; '1001' = @{ Name='Repeated'; Region='LEGS'; Practice='Test'; MotionProfile='Test' } }"
    )) {
        Set-Content -LiteralPath (Join-Path $fixture 'AdditionalExerciseCatalog.psd1') -Value $additional
        $rejected = $false
        try { $null = Get-ExerciseCatalogDefinitions -ToolsRoot $fixture }
        catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid or recycled additional identity was accepted.' }
    }

    $reviewPath = Join-Path $fixture 'MirrorReview.psd1'
    $catalogPath = Join-Path $fixture 'exercises.json'
    $emptyReview = @'
@{
    MirrorOnlyByCoverage = @{ UpperBody = @(); FullBody = @() }
    BenefitsGreatlyByCoverage = @{ UpperBody = @(); FullBody = @() }
    BenefitsGreatlyByCriterion = @{
        TechnicalMartialArts = @()
        DanceAndAlignmentSensitivePoses = @()
        ComplexSingleLegAlignment = @()
        LivePlaneOrSymmetryCorrection = @()
        GazeStabilityFeedback = @()
        SubtlePelvicPositionFeedback = @()
    }
    Agnostic = @()
}
'@
    foreach ($cell in @(
        @('Agnostic', 'None'),
        @('MirrorOnly', 'UpperBody'),
        @('MirrorOnly', 'FullBody'),
        @('BenefitsGreatly', 'UpperBody'),
        @('BenefitsGreatly', 'FullBody')
    )) {
        $relationship, $coverage = $cell
        $reviewText = $emptyReview
        if ($relationship -eq 'Agnostic') {
            $reviewText = $reviewText.Replace('Agnostic = @()', 'Agnostic = @(1)')
        } else {
            $emptyCell = "$($relationship)ByCoverage = @{ UpperBody = @(); FullBody = @() }"
            $reviewText = $reviewText.Replace($emptyCell, $emptyCell.Replace("$coverage = @()", "$coverage = @(1)"))
            if ($relationship -eq 'BenefitsGreatly') {
                $reviewText = $reviewText.Replace('TechnicalMartialArts = @()', 'TechnicalMartialArts = @(1)')
            }
        }
        Set-Content -LiteralPath $reviewPath -Value $reviewText
        $entry = @{
            id = 1
            mirrorRelationship = $relationship
            minimumMirrorCoverage = $coverage
            equipment = $(if ($relationship -eq 'MirrorOnly') { 'Mirror' } else { 'None' })
        }
        ConvertTo-Json -InputObject @($entry) | Set-Content -LiteralPath $catalogPath
        $null = & (Join-Path $PSScriptRoot 'Test-ExerciseMirrorRelationshipAudit.ps1') -ReviewPath $reviewPath -CatalogPath $catalogPath
    }

    # Empty populations are valid; missing schema, duplicate membership and
    # contradictory coverage or equipment are still invalid.
    $agnosticReview = $emptyReview.Replace('Agnostic = @()', 'Agnostic = @(1)')
    $agnosticCatalog = '[{"id":1,"mirrorRelationship":"Agnostic","minimumMirrorCoverage":"None","equipment":"None"}]'
    foreach ($invalid in @(
        @($agnosticReview.Replace('GazeStabilityFeedback = @()', ''), $agnosticCatalog),
        @($agnosticReview.Replace('Agnostic = @(1)', 'Agnostic = @(1, 1)'), $agnosticCatalog),
        @($agnosticReview.Replace('Agnostic = @(1)', 'Agnostic = @(2)'), $agnosticCatalog),
        @($agnosticReview, $agnosticCatalog.Replace('"minimumMirrorCoverage":"None"', '"minimumMirrorCoverage":"UpperBody"')),
        @($agnosticReview, $agnosticCatalog.Replace('"equipment":"None"', '"equipment":"Mirror"'))
    )) {
        Set-Content -LiteralPath $reviewPath -Value $invalid[0]
        Set-Content -LiteralPath $catalogPath -Value $invalid[1]
        $rejected = $false
        try {
            $null = & (Join-Path $PSScriptRoot 'Test-ExerciseMirrorRelationshipAudit.ps1') -ReviewPath $reviewPath -CatalogPath $catalogPath
        } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid mirror review schema or metadata was accepted.' }
    }

    $floorCatalog = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot '../NomadicMethod/Assets/exercises.json') -Raw | ConvertFrom-Json)
    $chop = $floorCatalog | Where-Object id -eq 616
    foreach ($profile in @('OverheadChop', 'SingleLegHop', 'CountermovementJump', 'JumpingJack', 'AlternatingBounds')) {
        $chop.motionProfile = $profile
        ConvertTo-Json -InputObject $floorCatalog -Depth 100 | Set-Content -LiteralPath $catalogPath
        $airborneRejected = $false
        try {
            $null = & (Join-Path $PSScriptRoot 'Test-ExerciseHardFloorCompatibilityAudit.ps1') -CatalogPath $catalogPath
        } catch {
            if ($_.Exception.Message -notlike 'Airborne-impact exercises cannot be Hard Floor compatible:*') { throw }
            $airborneRejected = $true
        }
        if ($airborneRejected -ne ($profile -ne 'OverheadChop')) {
            throw "Hard-floor motion classification incorrectly handled $profile."
        }
    }
} finally {
    foreach ($fixtureFile in @('RealExerciseCatalog.psd1', 'AdditionalExerciseCatalog.psd1', 'MirrorReview.psd1', 'exercises.json')) {
        $fixturePath = Join-Path $fixture $fixtureFile
        if (Test-Path -LiteralPath $fixturePath) { Remove-Item -LiteralPath $fixturePath }
    }
    Remove-Item -LiteralPath $fixture
}
Write-Output 'Historical identities preserved; valid new admission accepted; invalid and recycled identities rejected.'
Write-Output 'Empty mirror cells and criteria accepted; invalid schema, partitions, coverage and equipment rejected.'
Write-Output 'Planted overhead chops accepted; airborne PascalCase motion profiles rejected.'
