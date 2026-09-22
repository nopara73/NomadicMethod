param(
    [string]$ReviewPath = (
        Join-Path $PSScriptRoot 'ExerciseHardFloorCompatibility.psd1'),
    [string]$CatalogPath = (
        [System.IO.Path]::Combine(
            $PSScriptRoot,
            '..',
            'NomadicMethod',
            'Assets',
            'exercises.json'))
)

$ErrorActionPreference = 'Stop'

$requiredIncompatibilityReasons = @(
    'ConcentratedHeelOrForefootLoading'
    'TractionDependentWallOrBalanceLoading'
    'RepeatedJumpingOrLanding'
    'RunningOrRapidFootImpact'
    'DeliberateStomping'
    'LateralTravelOrDirectionChange'
    'TractionLoadedWideOrSplitStance'
    'PivotingStrikingOrKicking'
)

$review = Import-PowerShellDataFile -LiteralPath $ReviewPath -SkipLimitCheck
$incompatibleByReason = $review.IncompatibleByReason
if ($incompatibleByReason -isnot [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredIncompatibilityReasons `
            @($incompatibleByReason.Keys)).Count -gt 0) {
    throw 'Hard-floor incompatibility must use exactly the reviewed physical reasons.'
}

$compatibleIds = @($review.Compatible | ForEach-Object { [int]$_ })
$incompatibleIds = @(
    foreach ($reason in $requiredIncompatibilityReasons) {
        $reasonIds = @(
            $incompatibleByReason[$reason] |
                ForEach-Object { [int]$_ })
        if ($reasonIds.Count -eq 0) {
            throw "Hard-floor incompatibility reason '$reason' must not be empty."
        }
        $reasonIds
    })
$allReviewedIds = @($compatibleIds + $incompatibleIds)
if ($allReviewedIds.Count -ne
    @($allReviewedIds | Sort-Object -Unique).Count) {
    throw 'Every exercise must appear exactly once in the hard-floor audit.'
}

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$catalogIds = @($catalog | ForEach-Object { [int]$_.id })
if (@(Compare-Object `
        @($allReviewedIds | Sort-Object) `
        @($catalogIds | Sort-Object)).Count -gt 0) {
    throw 'The hard-floor audit and packaged exercise catalog do not cover the same IDs.'
}

foreach ($compatibilityReview in @(
        [pscustomobject]@{
            Compatibility = 'Compatible'
            ExerciseIds = $compatibleIds
        },
        [pscustomobject]@{
            Compatibility = 'Incompatible'
            ExerciseIds = $incompatibleIds
        })) {
    $reviewedIds = @($compatibilityReview.ExerciseIds | Sort-Object)
    $catalogCompatibilityIds = @(
        $catalog |
            Where-Object {
                $_.hardFloorCompatibility -eq
                    $compatibilityReview.Compatibility
            } |
            ForEach-Object { [int]$_.id } |
            Sort-Object)
    if (@(Compare-Object `
            $reviewedIds `
            $catalogCompatibilityIds).Count -gt 0) {
        throw "The '$($compatibilityReview.Compatibility)' review does not match the packaged catalog."
    }
}

# Any truthful catalog identity or motion profile that explicitly describes
# repeated airborne impact must remain soft-floor-only. This semantic guard
# prevents a future compatibility regression for a pogo, jump, hop, bounce,
# jack, or bound that remains honestly identified in the catalog.
$airborneImpactNamePattern =
    '(?i)\b(?:jump(?:ing|s)?|hop(?:ping|s)?|pogo|bounce(?:s)?|jack(?:s)?|bound(?:s|ing)?)\b'
# Profiles use PascalCase. Match complete motion tokens case-sensitively so
# OverheadChop is not mistaken for a Hop.
$airborneImpactProfilePattern = '(?-i:(?:Jump(?:ing|s)?|Hop(?:ping|s)?|Pogo|Bounce(?:s)?|Jack(?:s)?|Bound(?:s|ing)?)(?=[A-Z]|$))'
$hardFloorAirborneImpactExercises = @(
    $catalog | Where-Object {
        ([string]$_.name -match $airborneImpactNamePattern -or
            [string]$_.motionProfile -match $airborneImpactProfilePattern) -and
        [string]$_.hardFloorCompatibility -ne 'Incompatible'
    })
if ($hardFloorAirborneImpactExercises.Count -gt 0) {
    throw (
        'Airborne-impact exercises cannot be Hard Floor compatible: {0}.' -f
        (@($hardFloorAirborneImpactExercises.id) -join ', '))
}

$recordsById = @{}
foreach ($exercise in $catalog) {
    $recordsById[[int]$exercise.id] = $exercise
}
foreach ($root in $catalog |
        Where-Object { @($_.sequenceBlocks).Count -gt 0 }) {
    $rootCompatibility = [string]$root.hardFloorCompatibility
    foreach ($block in @($root.sequenceBlocks)) {
        $memberId = [int]$block.exerciseId
        if (-not $recordsById.ContainsKey($memberId) -or
            [string]$recordsById[$memberId].hardFloorCompatibility -ne
                $rootCompatibility) {
            throw "Exercise sequence $($root.id) mixes hard-floor classifications."
        }
    }
}

Write-Output (
    'Hard-floor audit valid: {0} compatible; {1} incompatible.' -f
        $compatibleIds.Count,
        $incompatibleIds.Count)
