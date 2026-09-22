param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\NomadicMethod\Assets'),
    [ValidateRange(1, [int]::MaxValue)]
    [int]$StartExercise = 1,
    [ValidateRange(0, [int]::MaxValue)]
    [int]$MaxExercises = 0,
    [ValidateRange(1, [int]::MaxValue)]
    [int[]]$ExerciseIds = @(),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'ReviewedSourceMedia.ps1')
. (Join-Path $PSScriptRoot 'ExerciseSourceDownload.ps1')
. (Join-Path $PSScriptRoot 'ExactExerciseMediaTiming.ps1')
. (Join-Path $PSScriptRoot 'ExerciseCatalogDefinitions.ps1')
$sourceDefinitions = @(Get-ExerciseCatalogDefinitions)
$sourceExerciseIds = @($sourceDefinitions | ForEach-Object { [int]$_.Id })

# The historical source catalog remains partitioned into ten 100-item families
# so stable exercise IDs and media-generation profiles do not move. These source
# families are generation details and are not emitted as runtime classifications.
$regions = @(
    'FEET',
    'LEGS',
    'HANDS',
    'ARMS',
    'HEAD',
    'SHOULDERS',
    'HIPS',
    'CHEST',
    'BACK',
    'CORE'
)

$catalogNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'RealExerciseCatalog.psd1') -SkipLimitCheck
$catalogExerciseReplacements = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CatalogExerciseReplacements.psd1') -SkipLimitCheck
$bilateralExerciseNames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'BilateralExerciseNames.psd1') -SkipLimitCheck
$exercisePracticeTaxonomy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExercisePracticeTaxonomy.psd1') -SkipLimitCheck
$exercisePracticeOverrides = $exercisePracticeTaxonomy.CatalogPracticeOverrides
$exerciseRegionOverrides = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseRegionOverrides.psd1') -SkipLimitCheck
$canonicalTaxonomy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CanonicalMuscleGroups.psd1') -SkipLimitCheck
$canonicalGroups = @($canonicalTaxonomy.Groups | Sort-Object { [int]$_.Id })
$canonicalGroupKeys = @(
    $canonicalGroups | ForEach-Object { [string]$_.StableKey })
$rawExerciseCanonicalGroups = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseCanonicalGroups.psd1') -SkipLimitCheck
$secondaryTrainingClaimReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSecondaryTrainingClaims.psd1') -SkipLimitCheck
$holdExerciseFrames = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'HoldExerciseFrames.psd1') -SkipLimitCheck
$stillExercisePresentations = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'StillExercisePresentations.psd1') -SkipLimitCheck
$externalExerciseMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck
$exerciseSideSequences = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSideSequences.psd1') -SkipLimitCheck
$baselineExerciseSideSequences = @{}
foreach ($entry in $exerciseSideSequences.GetEnumerator()) {
    $baselineExerciseSideSequences[[int]$entry.Key] = [string]$entry.Value
}
$reviewedContinuousExercises = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ReviewedContinuousExercises.psd1')
$reviewedContinuousExerciseIds = @(
    $reviewedContinuousExercises.Ids | ForEach-Object { [int]$_ })
$baselineReviewedContinuousExerciseIds = @($reviewedContinuousExerciseIds)
$alternatingExerciseReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseAlternatingSequences.psd1')
$alternatingExerciseIds = @(
    $alternatingExerciseReview.Ids | ForEach-Object { [int]$_ })
$insectCompatibilityReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseInsectCompatibility.psd1') -SkipLimitCheck
$insectCompatibleExerciseIds = @(
    $insectCompatibilityReview.Compatible | ForEach-Object { [int]$_ })
$insectIncompatibleExerciseIds = @(
    $insectCompatibilityReview.Incompatible | ForEach-Object { [int]$_ })
$hardFloorCompatibilityReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseHardFloorCompatibility.psd1') -SkipLimitCheck
$hardFloorCompatibleExerciseIds = @(
    $hardFloorCompatibilityReview.Compatible |
        ForEach-Object { [int]$_ })
$hardFloorIncompatibleExerciseIds = @(
    $hardFloorCompatibilityReview.IncompatibleByReason.Values |
        ForEach-Object { $_ } |
        ForEach-Object { [int]$_ })
$upperBodyClothingRequirementReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseUpperBodyClothingRequirements.psd1') -SkipLimitCheck
$upperBodyClothingRequiredExerciseIds = @(
    $upperBodyClothingRequirementReview.ClothingRequired |
        ForEach-Object { [int]$_ })
$bareUpperBodyRequiredExerciseIds = @(
    $upperBodyClothingRequirementReview.BareUpperBodyRequired |
        ForEach-Object { [int]$_ })
$upperBodyClothingAgnosticExerciseIds = @(
    $upperBodyClothingRequirementReview.Agnostic |
        ForEach-Object { [int]$_ })
$shyCompatibilityReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseShyCompatibility.psd1') -SkipLimitCheck
$shyCompatibleExerciseIds = @(
    $shyCompatibilityReview.Compatible |
        ForEach-Object { [int]$_ })
$shyIncompatibleExerciseIds = @(
    $shyCompatibilityReview.IncompatibleByReason.Values |
        ForEach-Object { $_ } |
        ForEach-Object { [int]$_ })
$wallRequirementReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseWallRequirements.psd1') -SkipLimitCheck
$wallRequiredExerciseIds = @(
    $wallRequirementReview.Required | ForEach-Object { [int]$_ })
$soleWallContactRequirementReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSoleWallContactRequirements.psd1') -SkipLimitCheck
$soleWallContactRequiredExerciseIds = @(
    $soleWallContactRequirementReview.Required |
        ForEach-Object { [int]$_ })
$silenceCompatibilityReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSilenceCompatibility.psd1') -SkipLimitCheck
$silentExerciseIds = @(
    $silenceCompatibilityReview.Silent | ForEach-Object { [int]$_ })
$nonSilentExerciseIds = @(
    $silenceCompatibilityReview.NonSilent | ForEach-Object { [int]$_ })
$mirrorRelationshipReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseMirrorRelationships.psd1') -SkipLimitCheck
$requiredMirrorCoverages = @('UpperBody', 'FullBody')
$requiredMirrorBenefitsGreatlyCriteria = @(
    'TechnicalMartialArts'
    'DanceAndAlignmentSensitivePoses'
    'ComplexSingleLegAlignment'
    'LivePlaneOrSymmetryCorrection'
    'GazeStabilityFeedback'
    'SubtlePelvicPositionFeedback'
)
$mirrorOnlyByCoverage = $mirrorRelationshipReview.MirrorOnlyByCoverage
if ($mirrorOnlyByCoverage -isnot [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredMirrorCoverages `
            @($mirrorOnlyByCoverage.Keys)).Count -gt 0) {
    throw 'MirrorOnly exercises must use exactly the UpperBody and FullBody coverage groups.'
}
$mirrorOnlyExerciseIds = @(
    foreach ($coverage in $requiredMirrorCoverages) {
        $coverageExerciseIds = @(
            $mirrorOnlyByCoverage[$coverage] | ForEach-Object { [int]$_ })
        $coverageExerciseIds
    })
$mirrorBenefitsGreatlyByCriterion =
    $mirrorRelationshipReview.BenefitsGreatlyByCriterion
if ($mirrorBenefitsGreatlyByCriterion -isnot
        [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredMirrorBenefitsGreatlyCriteria `
            @($mirrorBenefitsGreatlyByCriterion.Keys)).Count -gt 0) {
    throw 'BenefitsGreatly exercises must use exactly the approved narrow audit criteria.'
}
$mirrorBenefitsGreatlyExerciseIds = @(
    foreach ($criterion in $requiredMirrorBenefitsGreatlyCriteria) {
        $criterionExerciseIds = @(
            $mirrorBenefitsGreatlyByCriterion[$criterion] |
                ForEach-Object { [int]$_ })
        $criterionExerciseIds
    })
$mirrorBenefitsGreatlyByCoverage =
    $mirrorRelationshipReview.BenefitsGreatlyByCoverage
if ($mirrorBenefitsGreatlyByCoverage -isnot
        [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredMirrorCoverages `
            @($mirrorBenefitsGreatlyByCoverage.Keys)).Count -gt 0) {
    throw 'BenefitsGreatly exercises must use exactly the UpperBody and FullBody coverage groups.'
}
$mirrorBenefitsGreatlyCoverageExerciseIds = @(
    foreach ($coverage in $requiredMirrorCoverages) {
        $coverageExerciseIds = @(
            $mirrorBenefitsGreatlyByCoverage[$coverage] |
                ForEach-Object { [int]$_ })
        $coverageExerciseIds
    })
if (@(Compare-Object `
        @($mirrorBenefitsGreatlyExerciseIds | Sort-Object) `
        @($mirrorBenefitsGreatlyCoverageExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'BenefitsGreatly criterion and coverage reviews must identify the same exercises.'
}
$mirrorCoverageByExerciseId = @{}
foreach ($coverage in $requiredMirrorCoverages) {
    foreach ($exerciseId in @(
            $mirrorOnlyByCoverage[$coverage] +
            $mirrorBenefitsGreatlyByCoverage[$coverage] |
                ForEach-Object { [int]$_ })) {
        if ($mirrorCoverageByExerciseId.ContainsKey($exerciseId)) {
            throw "Exercise $exerciseId has more than one mirror coverage review."
        }
        $mirrorCoverageByExerciseId[$exerciseId] = $coverage
    }
}
$mirrorAgnosticExerciseIds = @(
    $mirrorRelationshipReview.Agnostic | ForEach-Object { [int]$_ })
$muscularDemandReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseMuscularDemand.psd1') -SkipLimitCheck
$sessionMovementReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSessionMovements.psd1') -SkipLimitCheck
$sessionMovementByExerciseId = @{}
foreach ($entry in $sessionMovementReview.Families.GetEnumerator()) {
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
        if ($sessionMovementByExerciseId.ContainsKey($exerciseId)) {
            throw "Exercise $exerciseId belongs to more than one session-movement family."
        }
        $sessionMovementByExerciseId[$exerciseId] = $rootId
    }
}
$muscularDemandByExerciseId = @{}
foreach ($score in 0..2) {
    foreach ($exerciseId in @(
            $muscularDemandReview["Score$score"] |
                ForEach-Object { [int]$_ })) {
        if ($muscularDemandByExerciseId.ContainsKey($exerciseId)) {
            throw "Exercise $exerciseId has more than one muscular-demand score."
        }
        $muscularDemandByExerciseId[$exerciseId] = $score
    }
}
$exerciseDirectionSequences = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseDirectionSequences.psd1') -SkipLimitCheck
$retiredDirectionOnlyExerciseIds = @(816)
$exerciseDirectionMediaTransforms = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseDirectionMediaTransforms.psd1') -SkipLimitCheck
$exerciseSequenceReview = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseSequences.psd1') -SkipLimitCheck
$posecodeExerciseMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'PosecodeExerciseMedia.psd1') -SkipLimitCheck
$exactExerciseMediaCopies = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaCopies.psd1') -SkipLimitCheck
$exactExerciseMediaTransforms = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaTransforms.psd1') -SkipLimitCheck
$verifiedExerciseDemos = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
$retainedExerciseIds = @(
    $verifiedExerciseDemos.ReviewedExternal +
        $verifiedExerciseDemos.ReviewedInternalAnatomy +
        $verifiedExerciseDemos.ReviewedPosecode +
        $verifiedExerciseDemos.PurposeBuiltSvg +
        $verifiedExerciseDemos.ReviewedExactCopies +
        $verifiedExerciseDemos.ReviewedExactTransforms |
        ForEach-Object { [int]$_ } |
        Sort-Object -Unique)
$expectedExerciseCount = $retainedExerciseIds.Count

if ($wallRequiredExerciseIds.Count -ne
        @($wallRequiredExerciseIds | Sort-Object -Unique).Count -or
    @($wallRequiredExerciseIds | Where-Object {
            $_ -notin $retainedExerciseIds }).Count -gt 0) {
    throw 'WallRequired must contain unique retained exercise IDs only.'
}
$invalidSoleWallContactRequirementIds = @(
    $soleWallContactRequiredExerciseIds | Where-Object {
        $_ -notin $retainedExerciseIds -or
        $_ -notin $wallRequiredExerciseIds
    })
if ($soleWallContactRequiredExerciseIds.Count -ne
        @($soleWallContactRequiredExerciseIds |
            Sort-Object -Unique).Count -or
    $invalidSoleWallContactRequirementIds.Count -gt 0) {
    throw 'SoleWallContactRequired must contain unique retained WallRequired exercise IDs only.'
}
$baseWallRequiredExerciseIds = @(
    $wallRequiredExerciseIds | Where-Object {
        $_ -notin $soleWallContactRequiredExerciseIds
    })
$wallRequiredSessionMovementCount = @(
    $baseWallRequiredExerciseIds | ForEach-Object {
        if ($sessionMovementByExerciseId.ContainsKey($_)) {
            [int]$sessionMovementByExerciseId[$_]
        }
        else {
            [int]$_
        }
    } | Sort-Object -Unique).Count
if ($wallRequiredSessionMovementCount -lt 20) {
    throw "WallRequired without sole contact requires at least 20 distinct session movements; found $wallRequiredSessionMovementCount."
}
$soleWallContactRequiredSessionMovementCount = @(
    $soleWallContactRequiredExerciseIds | ForEach-Object {
        if ($sessionMovementByExerciseId.ContainsKey($_)) {
            [int]$sessionMovementByExerciseId[$_]
        }
        else {
            [int]$_
        }
    } | Sort-Object -Unique).Count
if ($soleWallContactRequiredSessionMovementCount -lt 5) {
    throw "SoleWallContactRequired requires at least 5 distinct session movements; found $soleWallContactRequiredSessionMovementCount."
}

$allHardFloorReviewedExerciseIds = @(
    $hardFloorCompatibleExerciseIds +
    $hardFloorIncompatibleExerciseIds)
if ($allHardFloorReviewedExerciseIds.Count -ne
        @($allHardFloorReviewedExerciseIds | Sort-Object -Unique).Count -or
    @(Compare-Object `
            @($allHardFloorReviewedExerciseIds | Sort-Object) `
            @($retainedExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'Every retained exercise must have exactly one reviewed hard-floor classification.'
}

$allUpperBodyClothingReviewedExerciseIds = @(
    $upperBodyClothingRequiredExerciseIds +
    $bareUpperBodyRequiredExerciseIds +
    $upperBodyClothingAgnosticExerciseIds)
if ($allUpperBodyClothingReviewedExerciseIds.Count -ne
        @($allUpperBodyClothingReviewedExerciseIds |
            Sort-Object -Unique).Count -or
    @(Compare-Object `
            @($allUpperBodyClothingReviewedExerciseIds | Sort-Object) `
            @($retainedExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'Every retained exercise must have exactly one reviewed upper-body clothing requirement.'
}

$allShyReviewedExerciseIds = @(
    $shyCompatibleExerciseIds +
    $shyIncompatibleExerciseIds)
if ($allShyReviewedExerciseIds.Count -ne
        @($allShyReviewedExerciseIds | Sort-Object -Unique).Count -or
    @(Compare-Object `
            @($allShyReviewedExerciseIds | Sort-Object) `
            @($retainedExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'Every retained exercise must have exactly one reviewed Shy compatibility classification.'
}

if ([int]$muscularDemandReview.RubricVersion -ne 1 -or
    $muscularDemandByExerciseId.Count -ne $expectedExerciseCount -or
    @(Compare-Object @($muscularDemandByExerciseId.Keys | Sort-Object) `
            @($retainedExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'Every retained exercise must have exactly one reviewed muscular-demand score.'
}

$sequenceMembersByRootId = @{}
$sequenceRootByExerciseId = @{}
foreach ($entry in $exerciseSequenceReview.Sequences.GetEnumerator()) {
    $rootId = 0
    if (-not [int]::TryParse([string]$entry.Key, [ref]$rootId) -or
        $rootId -le 0) {
        throw "Invalid exercise-sequence root '$($entry.Key)'."
    }
    $memberIds = @($entry.Value | ForEach-Object { [int]$_ })
    if ($memberIds.Count -lt 2 -or
        $memberIds[0] -ne $rootId -or
        @($memberIds | Sort-Object -Unique).Count -ne $memberIds.Count -or
        @($memberIds | Where-Object { $_ -notin $retainedExerciseIds }).Count -gt 0) {
        throw "Exercise sequence $rootId must begin with its retained root and contain unique retained members."
    }
    foreach ($memberId in $memberIds) {
        if ($sequenceRootByExerciseId.ContainsKey($memberId)) {
            throw "Exercise $memberId belongs to more than one mandatory sequence."
        }
        $sequenceRootByExerciseId[$memberId] = $rootId
    }
    $sequenceMembersByRootId[$rootId] = $memberIds
}

$standaloneExerciseIds = @(
    $exerciseSequenceReview.StandaloneIds |
        ForEach-Object { [int]$_ })
$invalidStandaloneExerciseIds = @(
    $standaloneExerciseIds | Where-Object {
        $_ -notin $retainedExerciseIds -or
        $sequenceRootByExerciseId.ContainsKey($_)
    })
$reviewedSchedulingExerciseIds = @(
    @($sequenceRootByExerciseId.Keys | ForEach-Object { [int]$_ }) +
        $standaloneExerciseIds |
        Sort-Object -Unique)
if ($standaloneExerciseIds.Count -ne
        @($standaloneExerciseIds | Sort-Object -Unique).Count -or
    $invalidStandaloneExerciseIds.Count -gt 0 -or
    @(Compare-Object `
            @($retainedExerciseIds | Sort-Object) `
            @($reviewedSchedulingExerciseIds | Sort-Object)).Count -gt 0) {
    throw 'Every retained exercise must have exactly one explicit mandatory-sequence or standalone scheduling decision.'
}

$replacementExerciseIds = @(
    $catalogExerciseReplacements.Keys | ForEach-Object { [int]$_ } |
        Sort-Object -Unique)
$invalidReplacementDefinitions = @(
    foreach ($entry in $catalogExerciseReplacements.GetEnumerator()) {
        $exerciseId = [int]$entry.Key
        $replacement = $entry.Value
        if ($exerciseId -notin $retainedExerciseIds -or
            $replacement -isnot [System.Collections.IDictionary] -or
            -not $replacement.ContainsKey('RetiredName') -or
            [string]::IsNullOrWhiteSpace([string]$replacement.RetiredName) -or
            -not $replacement.ContainsKey('Name') -or
            [string]::IsNullOrWhiteSpace([string]$replacement.Name) -or
            [string]$replacement.Name -eq [string]$replacement.RetiredName -or
            -not $replacement.ContainsKey('Practice') -or
            [string]::IsNullOrWhiteSpace([string]$replacement.Practice) -or
            -not $replacement.ContainsKey('MotionProfile') -or
            [string]::IsNullOrWhiteSpace([string]$replacement.MotionProfile) -or
            -not $replacement.ContainsKey('Primary') -or
            -not $replacement.ContainsKey('Secondary') -or
            -not $replacement.ContainsKey('SideSequence') -or
            [string]$replacement.SideSequence -notin @(
                'Continuous', 'Alternating',
                'ScreenLeftThenRight', 'ScreenRightThenLeft',
                'ScreenLeftLeadThenRightLead',
                'ScreenRightLeadThenLeftLead') -or
            -not $replacement.ContainsKey('Mode') -or
            [string]$replacement.Mode -notin @('Repetition', 'Hold') -or
            -not $replacement.ContainsKey('Presentation') -or
            [string]$replacement.Presentation -notin @('Motion', 'Still') -or
            -not $replacement.ContainsKey('HoldFramePercent') -or
            [int]$replacement.HoldFramePercent -lt 0 -or
            [int]$replacement.HoldFramePercent -gt 99 -or
            ([string]$replacement.Mode -eq 'Repetition' -and
                [int]$replacement.HoldFramePercent -ne 0) -or
            ([string]$replacement.Mode -eq 'Hold' -and
                [int]$replacement.HoldFramePercent -eq 0) -or
            ([string]$replacement.Presentation -eq 'Still' -and
                [string]$replacement.Mode -ne 'Hold') -or
            -not $replacement.ContainsKey('Media') -or
            $replacement.Media -isnot [System.Collections.IDictionary]) {
            $exerciseId
        }
    })
if ($invalidReplacementDefinitions.Count -gt 0 -or
    $replacementExerciseIds.Count -ne $catalogExerciseReplacements.Count) {
    throw "The catalog replacement map contains invalid entries: $($invalidReplacementDefinitions -join ', ')."
}

foreach ($exerciseId in $replacementExerciseIds) {
    $replacement = $catalogExerciseReplacements[$exerciseId]
    $canonicalAssignment = $rawExerciseCanonicalGroups[$exerciseId]
    $replacementSecondary = @(
        $replacement.Secondary | ForEach-Object { [string]$_ })
    if ($canonicalAssignment -isnot [System.Collections.IDictionary] -or
        [string]$canonicalAssignment.Primary -ne [string]$replacement.Primary -or
        (@($canonicalAssignment.Secondary | ForEach-Object { [string]$_ } |
                Sort-Object) -join "`n") -ne
            (@($replacementSecondary | Sort-Object) -join "`n")) {
        throw "Replacement $exerciseId canonical groups have drifted from ExerciseCanonicalGroups.psd1."
    }
    $externalExerciseMedia[$exerciseId] = $replacement.Media
    $exerciseSideSequences.Remove($exerciseId)
    $alternatingExerciseIds = @(
        $alternatingExerciseIds | Where-Object { $_ -ne $exerciseId })
    $reviewedContinuousExerciseIds = @(
        $reviewedContinuousExerciseIds | Where-Object { $_ -ne $exerciseId })
    if ([string]$replacement.SideSequence -eq 'Continuous') {
        $reviewedContinuousExerciseIds += $exerciseId
    }
    elseif ([string]$replacement.SideSequence -eq 'Alternating') {
        # Alternating exercises use the continuous one-phase runtime while
        # carrying a distinct preview label. Keep them in the reviewed
        # continuous set as well as the alternating classification set.
        $reviewedContinuousExerciseIds += $exerciseId
        if ($exerciseId -notin $alternatingExerciseIds) {
            $alternatingExerciseIds += $exerciseId
        }
    }
    else {
        $exerciseSideSequences[$exerciseId] = [string]$replacement.SideSequence
    }
    $holdExerciseFrames.Remove($exerciseId)
    $stillExercisePresentations.Remove($exerciseId)
    if ([string]$replacement.Mode -eq 'Hold') {
        $holdExerciseFrames[$exerciseId] = [int]$replacement.HoldFramePercent
    }
    if ([string]$replacement.Presentation -eq 'Still') {
        $stillExercisePresentations[$exerciseId] = $true
    }
}

$reviewedContinuousExerciseIds = @(
    $reviewedContinuousExerciseIds | Where-Object {
        -not $exerciseDirectionSequences.ContainsKey($_) -or
        $_ -in $alternatingExerciseIds
    })

$canonicalIds = @($canonicalGroups | ForEach-Object { [int]$_.Id })
$canonicalDisplayNames = @(
    $canonicalGroups | ForEach-Object { [string]$_.DisplayName })
if ($canonicalGroups.Count -ne 30 -or
    @(Compare-Object $canonicalIds @(1..30)).Count -gt 0 -or
    @($canonicalGroupKeys | Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_)
        }).Count -gt 0 -or
    @($canonicalGroupKeys | Sort-Object -Unique).Count -ne 30 -or
    @($canonicalDisplayNames | Where-Object {
            [string]::IsNullOrWhiteSpace([string]$_)
        }).Count -gt 0 -or
    @($canonicalDisplayNames | Sort-Object -Unique).Count -ne 30) {
    throw 'The canonical muscle-group taxonomy must define exactly 30 stable, ordered leaves.'
}

$invalidPracticeOverrides = @(
    $exercisePracticeOverrides.GetEnumerator() | Where-Object {
        [int]$_.Key -notin $retainedExerciseIds -or
        [string]::IsNullOrWhiteSpace([string]$_.Value)
    })
if ($invalidPracticeOverrides.Count -gt 0) {
    throw 'The exercise-practice override map contains an invalid entry.'
}

if ($bilateralExerciseNames.Count -eq 0 -or @(
        $bilateralExerciseNames.GetEnumerator() | Where-Object {
            [int]$_.Key -lt 1 -or
            [int]$_.Key -notin $sourceExerciseIds -or
            [string]::IsNullOrWhiteSpace([string]$_.Value)
        }).Count -gt 0) {
    throw 'The bilateral catalog replacement map contains an invalid entry.'
}

$invalidRegionOverrides = @(
    $exerciseRegionOverrides.GetEnumerator() | Where-Object {
        [int]$_.Key -lt 1 -or
        [int]$_.Key -notin $sourceExerciseIds -or
        [string]$_.Value -notin $regions
    })
if ($invalidRegionOverrides.Count -gt 0) {
    throw 'The exercise-region override map contains an invalid entry.'
}

$exerciseCanonicalGroups = @{}
$invalidCanonicalAssignmentIds = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $rawExerciseCanonicalGroups.GetEnumerator()) {
    $exerciseId = 0
    if (-not [int]::TryParse([string]$entry.Key, [ref]$exerciseId) -or
        $exerciseId -lt 1 -or
        $exerciseId -notin $sourceExerciseIds -or
        $exerciseCanonicalGroups.ContainsKey($exerciseId)) {
        $invalidCanonicalAssignmentIds.Add([string]$entry.Key)
        continue
    }

    $exerciseCanonicalGroups[$exerciseId] = $entry.Value
}
$canonicalAssignmentIds = @($exerciseCanonicalGroups.Keys | Sort-Object)
$catalogAssignmentDifference = @(
    Compare-Object ($retainedExerciseIds | Sort-Object) $canonicalAssignmentIds)
$invalidCanonicalAssignments = @(
    foreach ($entry in $exerciseCanonicalGroups.GetEnumerator()) {
        $exerciseId = [int]$entry.Key
        $assignment = $entry.Value
        $primaryValues = @($assignment.Primary)
        $secondaryValues = @(
            $assignment.Secondary | ForEach-Object { [string]$_ })
        if ($assignment -isnot [System.Collections.IDictionary] -or
            -not $assignment.ContainsKey('Primary') -or
            -not $assignment.ContainsKey('Secondary') -or
            $primaryValues.Count -ne 1 -or
            [string]::IsNullOrWhiteSpace([string]$primaryValues[0]) -or
            [string]$primaryValues[0] -notin $canonicalGroupKeys -or
            @($secondaryValues | Where-Object {
                    [string]::IsNullOrWhiteSpace($_) -or
                    $_ -notin $canonicalGroupKeys
                }).Count -gt 0 -or
            @($secondaryValues | Sort-Object -Unique).Count -ne
                $secondaryValues.Count -or
            [string]$primaryValues[0] -in $secondaryValues) {
            $exerciseId
        }
    })
if ($invalidCanonicalAssignmentIds.Count -gt 0 -or
    $catalogAssignmentDifference.Count -gt 0 -or
    $invalidCanonicalAssignments.Count -gt 0) {
    throw 'The canonical exercise assignment map must cover every retained stable ID exactly once with one valid primary and unique valid secondaries.'
}

$expectedSecondaryTrainingClaimReviewKeys = @(
    'ByCanonicalGroup'
    'RubricVersion'
)
$reviewedSecondaryGroups = $secondaryTrainingClaimReview.ByCanonicalGroup
$reviewedSecondaryClaimKeys = @(
    foreach ($groupKey in $canonicalGroupKeys) {
        foreach ($exerciseId in @(
                $reviewedSecondaryGroups[$groupKey] |
                    ForEach-Object { [int]$_ })) {
            "${exerciseId}:$groupKey"
        }
    })
$catalogSecondaryClaimKeys = @(
    foreach ($entry in $exerciseCanonicalGroups.GetEnumerator()) {
        foreach ($groupKey in @(
                $entry.Value.Secondary | ForEach-Object { [string]$_ })) {
            "$([int]$entry.Key):$groupKey"
        }
    })
$invalidReviewedSecondaryClaimIds = @(
    foreach ($groupKey in $canonicalGroupKeys) {
        @($reviewedSecondaryGroups[$groupKey] |
            ForEach-Object { [int]$_ } |
            Where-Object { $_ -notin $retainedExerciseIds })
    })
if (@(Compare-Object `
            @($expectedSecondaryTrainingClaimReviewKeys | Sort-Object) `
            @($secondaryTrainingClaimReview.Keys | ForEach-Object { [string]$_ } |
                Sort-Object)).Count -gt 0 -or
    [int]$secondaryTrainingClaimReview.RubricVersion -ne 1 -or
    $reviewedSecondaryGroups -isnot [System.Collections.IDictionary] -or
    @(Compare-Object `
            @($canonicalGroupKeys | Sort-Object) `
            @($reviewedSecondaryGroups.Keys | ForEach-Object { [string]$_ } |
                Sort-Object)).Count -gt 0 -or
    $reviewedSecondaryClaimKeys.Count -ne
        @($reviewedSecondaryClaimKeys | Sort-Object -Unique).Count -or
    $invalidReviewedSecondaryClaimIds.Count -gt 0 -or
    @(Compare-Object `
            @($reviewedSecondaryClaimKeys | Sort-Object) `
            @($catalogSecondaryClaimKeys | Sort-Object)).Count -gt 0) {
    throw 'Every secondary association must exactly match the catalog-wide material-training audit.'
}

$canonicalGroupsWithoutDirectPrimaryWork = @(
    $canonicalGroupKeys | Where-Object {
        $groupKey = $_
        -not @($exerciseCanonicalGroups.GetEnumerator() | Where-Object {
                [string]$_.Value.Primary -eq $groupKey
            })
    })
if ($canonicalGroupsWithoutDirectPrimaryWork.Count -gt 0) {
    throw "Every canonical muscle group needs genuine direct primary work; missing: $($canonicalGroupsWithoutDirectPrimaryWork -join ', ')."
}

$invalidSessionMovementFamilies = @(
    foreach ($entry in $sessionMovementReview.Families.GetEnumerator()) {
        $rootId = [int]$entry.Key
        $memberIds = @($entry.Value | ForEach-Object { [int]$_ })
        if ($rootId -notin $retainedExerciseIds -or
            @($memberIds | Where-Object { $_ -notin $retainedExerciseIds }).Count -gt 0) {
            $rootId
            continue
        }
        $rootAssignment = $exerciseCanonicalGroups[$rootId]
        $rootGroups = @(
            [string]$rootAssignment.Primary
            @($rootAssignment.Secondary | ForEach-Object { [string]$_ }))
        if (@($memberIds | Where-Object {
                    $memberAssignment = $exerciseCanonicalGroups[$_]
                    $memberGroups = @(
                        [string]$memberAssignment.Primary
                        @($memberAssignment.Secondary |
                            ForEach-Object { [string]$_ }))
                    @($memberGroups | Where-Object {
                            $_ -in $rootGroups }).Count -eq 0
                }).Count -gt 0) {
            $rootId
        }
    })
if ($invalidSessionMovementFamilies.Count -gt 0) {
    throw "Session-movement families contain missing or anatomically unrelated exercises: $($invalidSessionMovementFamilies -join ', ')."
}
if ($holdExerciseFrames.Count -eq 0 -or @(
        $holdExerciseFrames.GetEnumerator() | Where-Object {
            [int]$_.Key -lt 1 -or
            [int]$_.Key -notin $sourceExerciseIds -or
            [int]$_.Value -lt 1 -or
            [int]$_.Value -gt 99
        }).Count -gt 0) {
    throw 'The reviewed hold-frame map contains an invalid entry.'
}

$invalidStillPresentations = @(
    $stillExercisePresentations.GetEnumerator() | Where-Object {
        [int]$_.Key -notin $retainedExerciseIds -or
        -not $holdExerciseFrames.ContainsKey([int]$_.Key) -or
        [bool]$_.Value -ne $true
    })
if ($invalidStillPresentations.Count -gt 0) {
    throw 'Every still presentation must identify a retained reviewed hold.'
}

$invalidExternalMedia = @(
    $externalExerciseMedia.GetEnumerator() | Where-Object {
        $exerciseId = [int]$_.Key
        $media = $_.Value
        $exerciseId -lt 1 -or
        $exerciseId -notin $sourceExerciseIds -or
        $media -isnot [System.Collections.IDictionary] -or
        -not $media.ContainsKey('File') -or
        [string]::IsNullOrWhiteSpace([string]$media.File) -or
        ($media.ContainsKey('Url') -and
            [string]::IsNullOrWhiteSpace([string]$media.Url)) -or
        ($media.ContainsKey('ArchiveUrl') -xor
            $media.ContainsKey('ArchiveEntry')) -or
        ($media.ContainsKey('ArchiveUrl') -and
            [string]::IsNullOrWhiteSpace([string]$media.ArchiveUrl)) -or
        ($media.ContainsKey('ArchiveEntry') -and
            [string]::IsNullOrWhiteSpace([string]$media.ArchiveEntry)) -or
        ($media.ContainsKey('StartSeconds') -and
            [double]$media.StartSeconds -lt 0) -or
        ($media.ContainsKey('DurationSeconds') -and
            [double]$media.DurationSeconds -le 0) -or
        ($media.ContainsKey('FramesPerSecond') -and
            [int]$media.FramesPerSecond -le 0) -or
        ($media.ContainsKey('DelayCentiseconds') -and
            [int]$media.DelayCentiseconds -le 0) -or
        ($media.ContainsKey('LocalSourceFile') -xor
            $media.ContainsKey('LocalSourceSha256')) -or
        ($media.ContainsKey('LocalSourceFile') -and (
            [string]$media.LocalSourceFile -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]*\.(gif|mp4)$' -or
            [string]$media.LocalSourceSha256 -cnotmatch '^[a-f0-9]{64}$'))
    })
if ($invalidExternalMedia.Count -gt 0) {
    throw 'The reviewed external-media map contains an invalid entry.'
}

$reviewedExternalIds = @(
    $verifiedExerciseDemos.ReviewedExternal | ForEach-Object { [int]$_ })
$reviewedInternalAnatomyIds = @(
    $verifiedExerciseDemos.ReviewedInternalAnatomy |
        ForEach-Object { [int]$_ })
$reviewedPosecodeIds = @(
    $verifiedExerciseDemos.ReviewedPosecode | ForEach-Object { [int]$_ })
$reviewedSvgIds = @(
    $verifiedExerciseDemos.PurposeBuiltSvg | ForEach-Object { [int]$_ })
$reviewedCopyIds = @(
    $verifiedExerciseDemos.ReviewedExactCopies | ForEach-Object { [int]$_ })
$reviewedTransformIds = @(
    $verifiedExerciseDemos.ReviewedExactTransforms | ForEach-Object { [int]$_ })
$invalidHumanSources = @($reviewedExternalIds | Where-Object {
        -not $externalExerciseMedia.ContainsKey($_) -or
        -not $externalExerciseMedia[$_].ContainsKey('Human') -or
        -not [bool]$externalExerciseMedia[$_].Human
    })
if ($invalidHumanSources.Count -gt 0) {
    throw "Every retained external demonstration must show an actual person: $($invalidHumanSources -join ', ')."
}
$invalidInternalAnatomySources = @(
    $reviewedInternalAnatomyIds | Where-Object {
        -not $externalExerciseMedia.ContainsKey($_) -or
        -not $externalExerciseMedia[$_].ContainsKey(
            'AuthoritativeAnatomicalVisualization') -or
        -not [bool]$externalExerciseMedia[$_].AuthoritativeAnatomicalVisualization -or
        ($externalExerciseMedia[$_].ContainsKey('Human') -and
            [bool]$externalExerciseMedia[$_].Human)
    })
if ($invalidInternalAnatomySources.Count -gt 0) {
    throw "Internally invisible training needs an explicitly reviewed authoritative anatomical visualization: $($invalidInternalAnatomySources -join ', ')."
}

$validSideSequences = @(
    'ScreenLeftThenRight',
    'ScreenRightThenLeft',
    'ScreenLeftLeadThenRightLead',
    'ScreenRightLeadThenLeftLead')
$validDirectionSequences = @(
    'ForwardThenBackward',
    'BackwardThenForward',
    'ClockwiseThenCounterclockwise',
    'CounterclockwiseThenClockwise',
    'InwardThenOutward',
    'OutwardThenInward')
$invalidSideSequences = @(
    $exerciseSideSequences.GetEnumerator() | Where-Object {
        [int]$_.Key -notin $retainedExerciseIds -or
        [string]$_.Value -notin $validSideSequences
    })
$invalidContinuousExerciseIds = @(
    $reviewedContinuousExerciseIds | Where-Object {
        $_ -notin $retainedExerciseIds -or
        $exerciseSideSequences.ContainsKey($_) -or
        ($exerciseDirectionSequences.ContainsKey($_) -and
            $_ -notin $alternatingExerciseIds)
    })
$invalidAlternatingExerciseIds = @(
    $alternatingExerciseIds | Where-Object {
        $_ -notin $retainedExerciseIds -or
        $_ -notin $reviewedContinuousExerciseIds
    })
$invalidDirectionSequences = @(
    $exerciseDirectionSequences.GetEnumerator() | Where-Object {
        [int]$_.Key -notin $retainedExerciseIds -or
        [string]$_.Value -notin $validDirectionSequences -or
        $holdExerciseFrames.ContainsKey([int]$_.Key) -or
        $stillExercisePresentations.ContainsKey([int]$_.Key)
    })
$invalidDirectionMediaTransforms = @(
    $exerciseDirectionMediaTransforms.GetEnumerator() | Where-Object {
        $transform = $_.Value
        [int]$_.Key -notin $retainedExerciseIds -or
        $transform -isnot [System.Collections.IDictionary] -or
        [string]$transform.Mode -notin @(
            'HorizontalMirror', 'TemporalReverse', 'ExactExercise') -or
        ([string]$transform.Mode -eq 'ExactExercise' -and (
            -not $transform.ContainsKey('SecondExerciseId') -or
            [int]$transform.SecondExerciseId -notin $retainedExerciseIds -or
            [int]$transform.SecondExerciseId -eq [int]$_.Key))
    })
$directionSequenceIds = @(
    $exerciseDirectionSequences.Keys | ForEach-Object { [int]$_ } | Sort-Object)
$directionTransformIds = @(
    $exerciseDirectionMediaTransforms.Keys |
        ForEach-Object { [int]$_ } |
        Sort-Object)
$reviewedSideSequenceIds = @(
    @($exerciseSideSequences.Keys | ForEach-Object { [int]$_ }) +
        @($exerciseDirectionSequences.Keys | ForEach-Object { [int]$_ }) +
        $reviewedContinuousExerciseIds |
        Sort-Object -Unique)
if ($invalidSideSequences.Count -gt 0 -or
    $invalidDirectionSequences.Count -gt 0 -or
    $invalidDirectionMediaTransforms.Count -gt 0 -or
    $invalidAlternatingExerciseIds.Count -gt 0 -or
    $alternatingExerciseIds.Count -ne
        @($alternatingExerciseIds | Sort-Object -Unique).Count -or
    @(Compare-Object $directionSequenceIds $directionTransformIds).Count -gt 0 -or
    $invalidContinuousExerciseIds.Count -gt 0 -or
    $reviewedContinuousExerciseIds.Count -ne
        @($reviewedContinuousExerciseIds | Sort-Object -Unique).Count -or
    @(Compare-Object $retainedExerciseIds $reviewedSideSequenceIds).Count -gt 0) {
    $sideSequenceDetails = @(
        if ($invalidSideSequences) {
            'invalid sides=' + (@($invalidSideSequences.Key) -join ',')
        }
        if ($invalidAlternatingExerciseIds) {
            'invalid alternating=' + ($invalidAlternatingExerciseIds -join ',')
        }
        if ($invalidDirectionSequences) {
            'invalid directions=' + (@($invalidDirectionSequences.Key) -join ',')
        }
        if ($invalidDirectionMediaTransforms) {
            'invalid direction media=' +
                (@($invalidDirectionMediaTransforms.Key) -join ',')
        }
        if ($invalidContinuousExerciseIds) {
            'invalid continuous=' + ($invalidContinuousExerciseIds -join ',')
        }
        if ($alternatingExerciseIds.Count -ne
            @($alternatingExerciseIds | Sort-Object -Unique).Count) {
            'duplicate alternating IDs'
        }
        if ($reviewedContinuousExerciseIds.Count -ne
            @($reviewedContinuousExerciseIds | Sort-Object -Unique).Count) {
            'duplicate continuous IDs'
        }
        $directionDrift = @(
            Compare-Object $directionSequenceIds $directionTransformIds)
        if ($directionDrift) {
            'direction media drift=' + (@($directionDrift | ForEach-Object {
                        "{0}:{1}" -f $_.InputObject, $_.SideIndicator
                    }) -join ',')
        }
        $reviewDrift = @(Compare-Object $retainedExerciseIds $reviewedSideSequenceIds)
        if ($reviewDrift) {
            'review drift=' + (@($reviewDrift | ForEach-Object {
                        "{0}:{1}" -f $_.InputObject, $_.SideIndicator
                    }) -join ',')
        }
    ) -join '; '
    throw "Every retained exercise must have an explicit reviewed side-sequence decision: $sideSequenceDetails"
}

$reviewedInsectExerciseIds = @(
    $insectCompatibleExerciseIds + $insectIncompatibleExerciseIds |
        Sort-Object -Unique)
if ($insectCompatibleExerciseIds.Count -ne
        @($insectCompatibleExerciseIds | Sort-Object -Unique).Count -or
    $insectIncompatibleExerciseIds.Count -ne
        @($insectIncompatibleExerciseIds | Sort-Object -Unique).Count -or
    @($insectCompatibleExerciseIds | Where-Object {
            $_ -in $insectIncompatibleExerciseIds }).Count -gt 0 -or
    @(Compare-Object $retainedExerciseIds $reviewedInsectExerciseIds).Count -gt 0) {
    throw 'Every retained exercise must have exactly one insect-compatibility review.'
}

$reviewedSilenceExerciseIds = @(
    $silentExerciseIds + $nonSilentExerciseIds | Sort-Object -Unique)
if ($silentExerciseIds.Count -ne
        @($silentExerciseIds | Sort-Object -Unique).Count -or
    $nonSilentExerciseIds.Count -ne
        @($nonSilentExerciseIds | Sort-Object -Unique).Count -or
    @($silentExerciseIds | Where-Object {
            $_ -in $nonSilentExerciseIds }).Count -gt 0 -or
    @(Compare-Object $retainedExerciseIds $reviewedSilenceExerciseIds).Count -gt 0) {
    throw 'Every retained exercise must have exactly one silence review.'
}

$reviewedMirrorExerciseIds = @(
    $mirrorOnlyExerciseIds +
    $mirrorBenefitsGreatlyExerciseIds +
    $mirrorAgnosticExerciseIds |
        Sort-Object -Unique)
$allMirrorReviewEntries = @(
    $mirrorOnlyExerciseIds +
    $mirrorBenefitsGreatlyExerciseIds +
    $mirrorAgnosticExerciseIds)
if ($allMirrorReviewEntries.Count -ne
        @($allMirrorReviewEntries | Sort-Object -Unique).Count -or
    @(Compare-Object $retainedExerciseIds $reviewedMirrorExerciseIds).Count -gt 0) {
    throw 'Every retained exercise must have exactly one mirror-relationship review.'
}

if ($reviewedPosecodeIds.Count -ne 0 -or $reviewedSvgIds.Count -ne 0) {
    throw 'Synthetic, schematic, and 3D demonstrations cannot be retained.'
}

$invalidPosecodeMedia = @(
    $posecodeExerciseMedia.GetEnumerator() | Where-Object {
        $exerciseId = [int]$_.Key
        $media = $_.Value
        $exerciseId -lt 1 -or
        $exerciseId -notin $sourceExerciseIds -or
        $media -isnot [System.Collections.IDictionary] -or
        -not $media.ContainsKey('File') -or
        [string]::IsNullOrWhiteSpace([string]$media.File) -or
        -not $media.ContainsKey('Source') -or
        [string]::IsNullOrWhiteSpace([string]$media.Source)
    })
if ($invalidPosecodeMedia.Count -gt 0) {
    throw 'The reviewed Posecode-media map contains an invalid entry.'
}

if (@(
        $exactExerciseMediaCopies.GetEnumerator() | Where-Object {
            [int]$_.Key -lt 1 -or
            [int]$_.Key -notin $sourceExerciseIds -or
            [int]$_.Value -lt 1 -or
            [int]$_.Value -notin $sourceExerciseIds -or
            [int]$_.Key -eq [int]$_.Value
        }).Count -gt 0) {
    throw 'The exact-media copy map contains an invalid entry.'
}

if (@(
        $exactExerciseMediaTransforms.GetEnumerator() | Where-Object {
            $targetId = [int]$_.Key
            $transform = $_.Value
            $targetId -lt 1 -or
            $targetId -notin $sourceExerciseIds -or
            $transform -isnot [System.Collections.IDictionary] -or
            -not $transform.ContainsKey('Source') -or
            [int]$transform.Source -lt 1 -or
            [int]$transform.Source -notin $sourceExerciseIds -or
            $targetId -eq [int]$transform.Source -or
            ($transform.ContainsKey('StartFramePercent') -and
                ([int]$transform.StartFramePercent -lt 1 -or
                    [int]$transform.StartFramePercent -gt 99)) -or
            -not (
                ($transform.ContainsKey('ReverseFrames') -and
                    [bool]$transform.ReverseFrames) -or
                ($transform.ContainsKey('HorizontalMirror') -and
                    [bool]$transform.HorizontalMirror) -or
                ($transform.ContainsKey('DelayCentiseconds') -and
                    [int]$transform.DelayCentiseconds -gt 0)
            )
        }).Count -gt 0) {
    throw 'The exact-media transform map contains an invalid entry.'
}


$copyMappingMatches =
    (@($reviewedCopyIds | Sort-Object) -join ',') -eq
    (@($exactExerciseMediaCopies.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
$transformMappingMatches =
    (@($reviewedTransformIds | Sort-Object) -join ',') -eq
    (@($exactExerciseMediaTransforms.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
$nonHumanDerivativeSources = @(
    @($exactExerciseMediaCopies.Values | ForEach-Object { [int]$_ }) +
        @($exactExerciseMediaTransforms.Values |
            ForEach-Object { [int]$_.Source }) |
        Where-Object {
            $_ -notin $reviewedExternalIds -and
            $_ -notin $reviewedInternalAnatomyIds
        } |
        Sort-Object -Unique)
$duplicateReviewedIds = @(
    $reviewedExternalIds + $reviewedInternalAnatomyIds +
        $reviewedCopyIds + $reviewedTransformIds |
        Group-Object |
        Where-Object Count -ne 1)
if (-not $copyMappingMatches -or
    -not $transformMappingMatches -or
    $nonHumanDerivativeSources.Count -gt 0 -or
    $duplicateReviewedIds.Count -gt 0) {
    throw 'The retained copy and transform inventory must derive only from reviewed direct demonstrations.'
}

$regionColors = @(
    '#00A896',
    '#0096C7',
    '#7B61FF',
    '#E76F51',
    '#F4A261',
    '#2A9D8F',
    '#E9C46A',
    '#E63946',
    '#457B9D',
    '#6A994E'
)

function Get-Practice {
    param([string]$Name)

    $practiceName = $Name -replace '^(Alternating|Bilateral|Symmetric)\s+', ''

    switch -Regex ($practiceName) {
        'Balance' { return 'Balance training' }
        'Stretch|Chest Opener|Forward Fold|Groin-Hamstring' { return 'Stretching' }
        '^Tai Chi' { return 'Tai Chi' }
        '^Qigong|^Eight Brocades|^Five-Animals' { return 'Qigong' }
        '^Bagua' { return 'Baguazhang' }
        '^Xingyi' { return 'Xingyiquan' }
        '^Karate' { return 'Karate' }
        '^Wing Chun' { return 'Wing Chun' }
        '^Ninja .*Hand-Seal' { return 'Ninja hand-seal coordination' }
        '^Boxing|^Lead Hook|^Rear Hook|^Lead Uppercut|^Rear Uppercut|^Shovel Hook|^Overhand Punch|^Long-Guard|^High-Guard|^Peekaboo|^Body Jab|^Body Cross' { return 'Boxing' }
        '^Capoeira' { return 'Capoeira' }
        '^Taekwondo' { return 'Taekwondo' }
        '^Muay-Thai' { return 'Muay Thai' }
        '^Fencing' { return 'Fencing' }
        '^Kalaripayattu' { return 'Kalaripayattu' }
        '^Ballet|^Pirouette' { return 'Ballet' }
        '^Belly-Dance|^Egyptian-Dance' { return 'Belly dance' }
        '^Salsa|^Rumba|^Cha-Cha|^Mambo|^Bachata|^Merengue|^Samba|^Foxtrot|^Waltz|^Ballroom|^Tango' { return 'Partner-dance technique' }
        '^Flamenco' { return 'Flamenco' }
        '^Bharatanatyam' { return 'Bharatanatyam' }
        '^Odissi' { return 'Odissi' }
        '^Kathak' { return 'Kathak' }
        '^Graham' { return 'Graham technique' }
        '^Horton' { return 'Horton technique' }
        'Pilates' { return 'Pilates' }
        'Mudra$|Drishti$|Pose$|^Warrior|^Yoga|Garudasana|Gomukhasana|Upward Salute|Extended Mountain|Humble Warrior|Crescent Lunge|Half Moon|Dancer|Tree-Pose|Eagle-Pose|Goddess-Pose|One-Legged Mountain' { return 'Yoga' }
        'VOR|Saccade|Smooth Pursuit|Gaze Stabilization|Gaze Shift|Near-Far Focus|Convergence|Peripheral-Awareness' { return 'Oculomotor and vestibular rehabilitation' }
        'Self-Resisted|Pull-Apart Isometric|Palm-Press' { return 'Self-resistance' }
        'Tendon|Finger|Thumb|Wrist|Hand|Fist|Palm|Prayer' { return 'Hand therapy and mobility' }
        'Squat|Lunge|Kickback|Deadlift|Good Morning|Standing Jack|Crunch|Knee-Up|Knee Lift|Leg Raise|Side Bend|Torso Twist|Overhead Slam' { return 'Bodyweight conditioning' }
        default { return 'Standing mobility and movement practice' }
    }
}

function Get-MotionProfile {
    param(
        [string]$Region,
        [string]$Name
    )

    switch ($Region) {
        'FEET' {
            if ($Name -match 'Ankle.*(Circle|Rotation|Alphabet)') { return 'AnkleCircle' }
            if ($Name -match 'Heel Raise|Plantarflexion') { return 'HeelRaise' }
            if ($Name -match 'Forefoot|Dorsiflexion') { return 'ForefootRaise' }
            if ($Name -match '^Ankle') { return 'AnkleSweep' }
            if ($Name -match 'Balance|Stance|Reach|Pendulum') { return 'FootBalance' }
            if ($Name -match 'Weight Shift|Foot Rock|Tripod') { return 'WeightShift' }
            if ($Name -match 'Turn|Pivot|Circle Walk|Kou Bu|Bai Bu') { return 'TurnStep' }
            if ($Name -match 'Cross|Grapevine|Carioca|Pas de Bourree') { return 'CrossStep' }
            if ($Name -match 'Side|L-Step|T-Step') { return 'SideStep' }
            return 'WalkingStep'
        }
        'LEGS' {
            if ($Name -match 'Squat|Pli[eé]|Chair Pose|Goddess|Horse|Duck Walk') { return 'Squat' }
            if ($Name -match 'Hinge|Good Morning|Deadlift') { return 'HipHinge' }
            if ($Name -match 'Lunge|Warrior I|Warrior II|Side Angle') { return 'Lunge' }
            if ($Name -match 'Calf Raise') { return 'HeelRaise' }
            if ($Name -match 'Tibialis Raise') { return 'ForefootRaise' }
            if ($Name -match 'Hamstring Curl|Heel Flick') { return 'KneeCurl' }
            if ($Name -match 'Knee|March|Passe|Retire') { return 'KneeLift' }
            if ($Name -match 'Side|Abduction|Adduction|a la Seconde') { return 'LegSide' }
            if ($Name -match 'Back|Extension|Derriere|Arabesque|Attitude') { return 'LegBack' }
            if ($Name -match 'Kick|Front|Flexion|Devant|Tendu|Degage|Toy Soldier') { return 'LegFront' }
            return 'LegBalance'
        }
        'HANDS' {
            if ($Name -match 'Hand-Seal') { return 'HandSeal' }
            if ($Name -match 'Wrist|Pronation|Supination|Turnover') { return 'WristMotion' }
            if ($Name -match 'Thumb') { return 'ThumbMotion' }
            if ($Name -match 'Isometric|Press|Stretch|Pull-Apart') { return 'HandIsometric' }
            if ($Name -match 'Mudra$') { return 'Mudra' }
            if ($Name -match 'Boxing|Punch|Hook|Uppercut|Overhand|Wing Chun|Karate|Block|Claw|Beak|Fist Formation|Spear|Knife|Ridge') { return 'MartialHand' }
            return 'FingerMotion'
        }
        'ARMS' {
            if ($Name -match 'Circle|Figure Eight|Windmill') { return 'ArmCircle' }
            if ($Name -match 'Punch|Jab|Cross$|Zuki|Straight') { return 'StraightPunch' }
            if ($Name -match 'Hook|Uppercut|Empi|Uraken') { return 'BentArmStrike' }
            if ($Name -match 'Uke|Parry|Catch|Guard|Sau') { return 'ArmBlock' }
            if ($Name -match 'Tai Chi|Qigong') { return 'FlowingArms' }
            if ($Name -match 'Port de Bras|Allonge|Arabesque') { return 'PortDeBras' }
            if ($Name -match 'Backstroke|Freestyle|Breaststroke|Butterfly|Kayak|Ski|Skater') { return 'SportArmCycle' }
            if ($Name -match 'Raise|Reach|Overhead') { return 'ArmRaise' }
            if ($Name -match 'Curl|Flexion|Extension|Triceps') { return 'ElbowMotion' }
            return 'ArmSweep'
        }
        'HEAD' {
            if ($Name -match 'Slow Blink|Eye Squeeze') { return 'EyeBlink' }
            if ($Name -match 'Cross-Pattern Saccades') { return 'GazeCrossSaccade' }
            if ($Name -match 'Cross-Pattern') { return 'GazeCross' }
            if ($Name -match 'Figure-Eight Smooth|Infinity Gaze') { return 'GazeFigureEight' }
            if ($Name -match 'Near-Far Focus') { return 'GazeNearFar' }
            if ($Name -match 'Vertical Near-Far') { return 'GazeVerticalNearFar' }
            if ($Name -match 'Vertical Gaze Ladder') { return 'GazeVerticalLadder' }
            if ($Name -match 'Convergence|Nasagra|Bhrumadhya|Angusthamadhye') { return 'GazeConvergence' }
            if ($Name -match 'Horizontal Gaze Shift Between Thumbs|Kathak Alternating Side Gaze') { return 'GazeHorizontalSaccade' }
            if ($Name -match 'Peripheral-Awareness') { return 'GazePeripheral' }
            if ($Name -match 'Four-Corner Saccades|Square-Path Saccades') { return 'GazeCornerSaccade' }
            if ($Name -match 'Clock-Face Saccades') { return 'GazeClockSaccade' }
            if ($Name -match 'Triangle-Path Saccades') { return 'GazeTriangleSaccade' }
            if ($Name -match 'Horizontal Saccades') { return 'GazeHorizontalSaccade' }
            if ($Name -match 'Vertical Saccades') { return 'GazeVerticalSaccade' }
            if ($Name -match 'Horizontal Gaze Stabilization|Horizontal VOR x1') { return 'VorHorizontal' }
            if ($Name -match 'Vertical Gaze Stabilization|Vertical VOR x1') { return 'VorVertical' }
            if ($Name -match 'Four-Direction Gaze Stabilization|Four-Direction VOR') { return 'VorFourDirection' }
            if ($Name -match 'Horizontal VOR Cancellation') { return 'VorCancellationHorizontal' }
            if ($Name -match 'Vertical VOR Cancellation') { return 'VorCancellationVertical' }
            if ($Name -match 'Horizontal VOR x2') { return 'VorX2Horizontal' }
            if ($Name -match 'Vertical VOR x2') { return 'VorX2Vertical' }
            if ($Name -match 'Nose Square') { return 'HeadSquare' }
            if ($Name -match 'Four-Direction Head Tilt|Four-Corner Dance Head Accent|Jazz Head Isolation') { return 'HeadFourDirection' }
            if ($Name -match 'Half-Circle') { return 'HeadHalfCircle' }
            if ($Name -match 'Figure Eight|Infinity Sign') { return 'HeadFigureEight' }
            if ($Name -match 'Diamond Trace') { return 'HeadDiamond' }
            if ($Name -match 'Turn-and-Nod Sequence') { return 'HeadTurnNod' }
            if ($Name -match 'Neck Elongation|Sama Shiro') { return 'HeadNeutral' }
            if ($Name -match 'Chin-Tuck') { return 'HeadTranslate' }
            if ($Name -match 'Dragon Surveys the Sea|Head Survey Arc') { return 'HeadSurvey' }
            if ($Name -match 'Dhuta-Kampita') { return 'HeadTurnNod' }
            if ($Name -match 'Tiger Watches Prey|Dhuta Shiro') { return 'HeadTurn' }
            if ($Name -match 'Lateral|Side-Bend|Side-to-Side Dance Head Accent|Tilt|Ear-to|Griva|Parivahita') { return 'HeadTilt' }
            if ($Name -match 'Flexion|Extension|Nod|Kampita|Accent Front|Udvahita|Adhomukha') { return 'HeadNod' }
            if ($Name -match 'Rotation|Turn|Looks Back|Gazes Back|Paravritta|Spotting|Flick') { return 'HeadTurn' }
            if ($Name -match 'Circle|Figure Eight|Infinity|Alphabet|Alolita|Roll') { return 'HeadCircle' }
            if ($Name -match 'Translation|Slide|Protraction|Retraction|Turtle') { return 'HeadTranslate' }
            if ($Name -match 'Horizontal|Parsva') { return 'GazeHorizontal' }
            if ($Name -match 'Vertical|Urdhva|Nabi|Padayor') { return 'GazeVertical' }
            if ($Name -match 'Circular|Clock|Square|Triangle') { return 'GazeCircle' }
            return 'GazeDiagonal'
        }
        'SHOULDERS' {
            if ($Name -match 'Roll|Circle|Clock|Figure Eight|\bCAR\b') { return 'ShoulderCircle' }
            if ($Name -match 'Scapular|Shoulder-Blade|Serratus') { return 'ScapularGlide' }
            if ($Name -match 'Rotation|Cuban|Goalpost|Cactus') { return 'ShoulderRotation' }
            if ($Name -match 'Stretch|Eagle|Cow-Face|Garudasana|Gomukhasana|Prayer|Hands-Behind') { return 'ShoulderStretch' }
            if ($Name -match 'Shimmy|Accent|Epaulement|Natyarambhe|Chowka|Kathak') { return 'ShoulderDance' }
            if ($Name -match 'Backstroke|Freestyle|Breaststroke|Butterfly|Ski') { return 'ShoulderCycle' }
            return 'ShoulderRaise'
        }
        'HIPS' {
            if ($Name -match 'Circle|Clock|Figure Eight|Umi|Maya|Taqsim|CAR') { return 'HipCircle' }
            if ($Name -match 'Pelvic Tilt|Tuck|Undulation|Samba') { return 'PelvicTilt' }
            if ($Name -match 'Shift|Slide|Drop|Lift|Bump|Sway|Shimmy') { return 'HipShift' }
            if ($Name -match 'Hinge|Good Morning|Deadlift|Pigeon|Stretch|Penche') { return 'HipHinge' }
            if ($Name -match 'Leg Swing|Pendulum|Rond|Developpe|Attitude|Arabesque|Passe|Retire') { return 'HipLegArc' }
            if ($Name -match 'Rotation|Kua|Coil|Hip Snap|Hip Action|Cuban Motion') { return 'HipRotation' }
            return 'HipOpenClose'
        }
        'CHEST' {
            if ($Name -match 'Breath|Breathing|Expansion') { return 'ChestBreathing' }
            if ($Name -match 'Circle|Figure Eight|Ribcage Isolation|Slide') { return 'ChestCircle' }
            if ($Name -match 'Press|Strike|Punch|Push-Up|Squeeze|Svend') { return 'ChestPress' }
            if ($Name -match 'Fly|Hug|Wing|Crane') { return 'ChestFly' }
            if ($Name -match 'Open|Stretch|Prayer|Salute|Warrior|Mountain|Cactus|Goalpost|Cow-Face|Eagle') { return 'ChestOpen' }
            if ($Name -match 'Tai Chi|Qigong|Brocades') { return 'ChestFlow' }
            return 'ChestIsolation'
        }
        'BACK' {
            if ($Name -match 'Tai Chi Rollback') { return 'SpineRotation' }
            if ($Name -match 'Flexion|Roll|Fold|Contraction') { return 'SpineFlexion' }
            if ($Name -match 'Extension|Backbend|Cobra|Locust|Cambre Back|Release') { return 'SpineExtension' }
            if ($Name -match 'Side|Lateral|Banana|Crescent-Moon|Esquiva') { return 'SpineSideBend' }
            if ($Name -match 'Rotation|Twist|Turn|Coil|Rollback|Cloud|Shuttle') { return 'SpineRotation' }
            if ($Name -match 'Wave|Figure Eight|Circle|Cat-Cow|Dragon|Undulation') { return 'SpineWave' }
            if ($Name -match 'Hinge|Good Morning|Flat Back|Needle|Sea Bottom') { return 'BackHinge' }
            if ($Name -match 'Row|Pulldown|Fly|Scapular|Lat') { return 'BackArmPull' }
            if ($Name -match 'Slip|Roll|Weave|Lean|Cocorinha') { return 'BoxingEvasion' }
            return 'BackBalance'
        }
        'CORE' {
            if ($Name -match 'Crunch|Knee-to-Elbow|Knee Tuck|Knee Drive|Bicycle') { return 'CoreCrunch' }
            if ($Name -match 'Side Bend|Windmill|Side Crunch|Anti-Tilt') { return 'CoreSideBend' }
            if ($Name -match 'Chop|Canoe|Kayak|Russian|Rotation|Twist|Dantian|Waist') { return 'CoreRotation' }
            if ($Name -match 'Balance|Pose|Stance|Single-Leg|High-Knee|Knee Chamber') { return 'CoreBalance' }
            if ($Name -match 'Reach|Bird|Cross-Crawl|Mountain-Climber|Bear-Crawl|March') { return 'CoreCrossCrawl' }
            if ($Name -match 'Self-Resisted|Palm-to-Knee|Isometric') { return 'CoreIsometric' }
            return 'CoreBrace'
        }
    }
}

function New-HandExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [string]$MotionProfile,
        [int]$MovementIndex,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $open = if ($MotionProfile -eq 'FingerMotion') { (1 + $wave) / 2 } else { 0.75 }
    $shape = $MovementIndex % 6
    $gap = if ($MotionProfile -in @('HandIsometric', 'Mudra')) { 8 + (6 * [Math]::Abs($wave)) } else { 34 }
    $leftPalmX = 128 - $gap - 26
    $rightPalmX = 128 + $gap + 26
    $rotation = if ($MotionProfile -eq 'WristMotion') { Get-RoundedInt ($wave * 24) } else { 0 }
    $fingerLines = [System.Text.StringBuilder]::new()

    foreach ($side in @(-1, 1)) {
        $palmX = if ($side -eq -1) { $leftPalmX } else { $rightPalmX }
        for ($finger = 0; $finger -lt 4; $finger++) {
            $fingerOpen = $open
            if ($MotionProfile -eq 'FingerMotion' -and $shape -in @(1, 2, 4)) {
                $fingerOpen = (1 + [Math]::Sin($phase + ($finger * [Math]::PI / 2))) / 2
            }

            $baseX = $palmX + (($finger - 1.5) * 10)
            $spread = ($finger - 1.5) * (4 + (8 * $fingerOpen))
            $tipX = Get-RoundedInt ($baseX + $spread)
            $length = 25 + ($finger * 3) + (13 * $fingerOpen)
            $tipY = Get-RoundedInt (112 - $length)

            if ($MotionProfile -eq 'Mudra' -and $finger -eq ($shape % 4)) {
                $tipX = Get-RoundedInt ($palmX + ($side * 30))
                $tipY = 116
            }
            elseif ($MotionProfile -eq 'Mudra' -and $shape -eq 5 -and $finger -in @(1, 2)) {
                $tipX = Get-RoundedInt ($palmX + (($finger - 1.5) * 5))
                $tipY = 124
            }
            elseif ($MotionProfile -eq 'MartialHand' -and ($wave -lt 0 -or $finger -lt ($shape % 4))) {
                $tipX = Get-RoundedInt ($palmX + (($finger - 1.5) * 5))
                $tipY = 116
            }

            [void]$fingerLines.AppendLine(('<line x1="{0}" y1="116" x2="{1}" y2="{2}" stroke="{3}" stroke-width="8" stroke-linecap="round" />' -f $baseX, $tipX, $tipY, $Accent))
        }

        $thumbBaseX = $palmX + ($side * 22)
        $thumbWave = if ($MotionProfile -eq 'ThumbMotion') { $wave * 18 } else { $side * 10 }
        $thumbTipX = Get-RoundedInt ($thumbBaseX + ($side * 14) + $thumbWave)
        $thumbTipY = if ($MotionProfile -eq 'Mudra') { 116 } else { 136 }
        [void]$fingerLines.AppendLine(('<line x1="{0}" y1="130" x2="{1}" y2="{2}" stroke="{3}" stroke-width="9" stroke-linecap="round" />' -f $thumbBaseX, $thumbTipX, $thumbTipY, $Accent))
    }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Nomadic Method real hand exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  <g transform="rotate($rotation 128 132)">
    <rect x="$($leftPalmX - 25)" y="112" width="50" height="76" rx="22" fill="$Accent" opacity="0.88" />
    <rect x="$($rightPalmX - 25)" y="112" width="50" height="76" rx="22" fill="$Accent" opacity="0.88" />
    $fingerLines
  </g>
  <line x1="40" y1="220" x2="216" y2="220" stroke="#C9D7E3" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function New-HeadExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [string]$ExerciseName,
        [string]$MotionProfile,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    $headX = 128
    $headY = 104
    $rotation = 0
    $leftPupilX = 0
    $rightPupilX = 0
    $leftPupilY = 0
    $rightPupilY = 0
    $turn = 0.0
    $blink = 0.0
    $sideProfile = $false
    $showTarget = $false
    $targetX = 128
    $targetY = 38
    $targetRadius = 5
    $targetPath = ''
    $stepHorizontal = if ($wave -ge 0) { 1 } else { -1 }
    $stepVertical = if ($counterWave -ge 0) { 1 } else { -1 }

    switch ($MotionProfile) {
        'HeadNod' {
            $sideProfile = $true
            if ($ExerciseName -match 'Extension|Udvahita' -and $ExerciseName -notmatch 'Flexion-Extension') {
                $rotation = Get-RoundedInt (-[Math]::Max(0, $wave) * 18)
            }
            elseif ($ExerciseName -match 'Flexion-Extension') {
                $rotation = Get-RoundedInt ($wave * 18)
            }
            else {
                $rotation = Get-RoundedInt ([Math]::Max(0, $wave) * 16)
            }
        }
        'HeadTurn' {
            $headWave = $wave
            $eyeWave = $wave
            if ($ExerciseName -match 'Opposite-Direction') {
                $eyeWave = -$wave
            }
            elseif ($ExerciseName -match 'Eyes-Lead') {
                $eyeWave = [Math]::Sin($phase + ([Math]::PI / 4))
            }
            elseif ($ExerciseName -match 'Head-Lead') {
                $headWave = [Math]::Sin($phase + ([Math]::PI / 4))
            }

            $turn = $headWave
            $leftPupilX = Get-RoundedInt ($eyeWave * 5)
            $rightPupilX = $leftPupilX
        }
        'HeadTilt' { $rotation = Get-RoundedInt ($wave * 18) }
        'HeadCircle' {
            $circlePhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $circleDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $headX += Get-RoundedInt ([Math]::Sin($circleDirection * $circlePhase) * 10)
            $headY += Get-RoundedInt ([Math]::Cos($circleDirection * $circlePhase) * 7)
        }
        'HeadHalfCircle' {
            # Trace the chin from one collarbone to the other across the chest,
            # then return along the same safe forward semicircle.
            $halfPhase = [Math]::PI * ($FrameIndex % 8) / 7
            $halfDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $travelPhase = if ($halfDirection -eq 1) { $halfPhase } else { [Math]::PI - $halfPhase }
            $headX += Get-RoundedInt ([Math]::Cos($travelPhase) * 15)
            $headY += Get-RoundedInt ([Math]::Sin($travelPhase) * 12)
            $rotation = Get-RoundedInt (([Math]::Cos($travelPhase)) * 13)
        }
        'HeadFigureEight' {
            # The face remains forward while the nose traces both lobes of a
            # horizontal figure eight, reversing direction for symmetry.
            $figurePhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $figureDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $figureWave = [Math]::Sin($figureDirection * $figurePhase)
            $figureDoubleWave = [Math]::Sin(2 * $figureDirection * $figurePhase)
            $headX += Get-RoundedInt ($figureWave * 15)
            $headY += Get-RoundedInt ($figureDoubleWave * 9)
        }
        'HeadDiamond' {
            $diamondSequence = @(0, 1, 2, 3, 0, 3, 2, 1)
            $diamondIndex = $diamondSequence[[int][Math]::Floor($FrameIndex / 2)]
            $headX += @(0, 15, 0, -15)[$diamondIndex]
            $headY += @(-12, 0, 12, 0)[$diamondIndex]
        }
        'HeadSquare' {
            $squareSequence = @(0, 1, 2, 3, 0, 3, 2, 1)
            $squareIndex = $squareSequence[[int][Math]::Floor($FrameIndex / 2)]
            $headX += @(-11, 11, 11, -11)[$squareIndex]
            $headY += @(-8, -8, 8, 8)[$squareIndex]
        }
        'HeadFourDirection' {
            $directionIndex = [int][Math]::Floor($FrameIndex / 4)
            if ($directionIndex -eq 0) { $rotation = -16 }
            elseif ($directionIndex -eq 1) { $rotation = 16 }
            elseif ($directionIndex -eq 2) {
                $sideProfile = $true
                $rotation = -16
            }
            else {
                $sideProfile = $true
                $rotation = 16
            }
        }
        'HeadSurvey' {
            $surveyPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $surveyDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $turn = [Math]::Sin($surveyDirection * $surveyPhase)
            $rotation = Get-RoundedInt ([Math]::Cos($surveyDirection * $surveyPhase) * 8)
        }
        'HeadTurnNod' {
            if ($FrameIndex -lt 8) {
                $turn = [Math]::Sin(2 * [Math]::PI * $FrameIndex / 8)
            }
            else {
                $sideProfile = $true
                $rotation = Get-RoundedInt ([Math]::Sin(2 * [Math]::PI * ($FrameIndex - 8) / 8) * 16)
            }
        }
        'HeadNeutral' {
            $headY -= Get-RoundedInt ([Math]::Max(0, $wave) * 4)
            $blink = [Math]::Max(0, -$counterWave)
        }
        'HeadTranslate' {
            $sideProfile = $ExerciseName -notmatch 'Side-to-Side|Dance Head Slide'
            if ($ExerciseName -match 'Retraction|Chin-Tuck') {
                $headX -= Get-RoundedInt ([Math]::Max(0, $wave) * 18)
            }
            elseif ($ExerciseName -match 'Protraction') {
                $headX += Get-RoundedInt ([Math]::Max(0, $wave) * 18)
            }
            else {
                $headX += Get-RoundedInt ($wave * 18)
            }
        }
        'EyeBlink' {
            $blink = (1 - $counterWave) / 2
        }
        'GazeHorizontal' {
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($wave * 76))
            $targetY = $headY - 7
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $targetPath = '<line x1="52" y1="97" x2="204" y2="97" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeVertical' {
            $showTarget = $true
            $targetX = 128
            $targetY = Get-RoundedInt ($headY - 7 + ($wave * 64))
            $leftPupilY = Get-RoundedInt ($wave * 7)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="128" y1="33" x2="128" y2="161" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCircle' {
            $circlePhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $circleDirection = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $circleWave = [Math]::Sin($circleDirection * $circlePhase)
            $circleCounterWave = [Math]::Cos($circleDirection * $circlePhase)
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($circleWave * 68))
            $targetY = Get-RoundedInt ($headY - 7 + ($circleCounterWave * 55))
            $leftPupilX = Get-RoundedInt ($circleWave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($circleCounterWave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<ellipse cx="128" cy="97" rx="68" ry="55" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeDiagonal' {
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($wave * 66))
            $targetY = Get-RoundedInt ($headY - 7 + ($wave * 52))
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($wave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="62" y1="45" x2="194" y2="149" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCross' {
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $localWave = [Math]::Sin($localPhase)
            $diagonal = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($localWave * 66))
            $targetY = Get-RoundedInt ($headY - 7 + ($diagonal * $localWave * 52))
            $leftPupilX = Get-RoundedInt ($localWave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($diagonal * $localWave * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M62 45 L194 149 M194 45 L62 149" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeCrossSaccade' {
            $crossSequence = @(0, 2, 1, 3)
            $crossIndex = $crossSequence[[int][Math]::Floor($FrameIndex / 4)]
            $cornerX = @(-1, 1, 1, -1)[$crossIndex]
            $cornerY = @(-1, -1, 1, 1)[$crossIndex]
            $showTarget = $true
            $targetX = 128 + ($cornerX * 66)
            $targetY = $headY - 7 + ($cornerY * 52)
            $leftPupilX = $cornerX * 8
            $rightPupilX = $leftPupilX
            $leftPupilY = $cornerY * 6
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M62 45 L194 149 M194 45 L62 149" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeFigureEight' {
            $showTarget = $true
            $figureY = [Math]::Sin(2 * $phase)
            $targetX = Get-RoundedInt (128 + ($wave * 70))
            $targetY = Get-RoundedInt ($headY - 7 + ($figureY * 43))
            $leftPupilX = Get-RoundedInt ($wave * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($figureY * 6)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M58 97 C58 44 112 44 128 97 C144 150 198 150 198 97 C198 44 144 44 128 97 C112 150 58 150 58 97" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeNearFar' {
            $showTarget = $true
            $near = (1 + $wave) / 2
            $targetRadius = Get-RoundedInt (4 + ($near * 8))
            $targetY = Get-RoundedInt (45 + ($near * 22))
            $converge = Get-RoundedInt ($near * 5)
            $leftPupilX = $converge
            $rightPupilX = -$converge
        }
        'GazeVerticalNearFar' {
            $showTarget = $true
            $near = (1 + $wave) / 2
            $targetRadius = Get-RoundedInt (4 + ($near * 8))
            $targetY = Get-RoundedInt (38 + ($near * 105))
            $converge = Get-RoundedInt ($near * 5)
            $leftPupilX = $converge
            $rightPupilX = -$converge
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 10)
            $rightPupilY = $leftPupilY
            $targetPath = '<line x1="128" y1="38" x2="128" y2="143" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeVerticalLadder' {
            $ladderSequence = @(0, 1, 2, 3, 4, 3, 2, 1)
            $ladderIndex = $ladderSequence[[int][Math]::Floor($FrameIndex / 2)]
            $targetX = 128
            $targetY = 37 + ($ladderIndex * 29)
            $showTarget = $true
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 9)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M116 37 H140 M116 66 H140 M116 95 H140 M116 124 H140 M116 153 H140" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="4 5" opacity="0.45" />'
        }
        'GazePeripheral' {
            $peripheralIndex = [int][Math]::Floor($FrameIndex / 4)
            $targetX = @(62, 194, 194, 62)[$peripheralIndex]
            $targetY = @(45, 45, 149, 149)[$peripheralIndex]
            $showTarget = $true
            $targetPath = '<circle cx="62" cy="45" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="194" cy="45" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="194" cy="149" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" /><circle cx="62" cy="149" r="5" fill="none" stroke="#E63946" stroke-width="2" opacity="0.35" />'
        }
        'GazeConvergence' {
            $showTarget = $true
            $targetX = 128
            $targetY = if ($ExerciseName -match 'Bhrumadhya') { 76 } elseif ($ExerciseName -match 'Nasagra') { 126 } else { 105 }
            $converge = Get-RoundedInt ((1 + $wave) * 3)
            $leftPupilX = $converge
            $rightPupilX = -$converge
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 10)
            $rightPupilY = $leftPupilY
        }
        'GazeHorizontalSaccade' {
            $showTarget = $true
            $targetX = 128 + ($stepHorizontal * 76)
            $targetY = $headY - 7
            $leftPupilX = $stepHorizontal * 8
            $rightPupilX = $leftPupilX
        }
        'GazeVerticalSaccade' {
            $showTarget = $true
            $targetX = 128
            $targetY = $headY - 7 + ($stepHorizontal * 64)
            $leftPupilY = $stepHorizontal * 7
            $rightPupilY = $leftPupilY
        }
        'GazeCornerSaccade' {
            $cornerSequence = @(0, 1, 2, 3, 0, 3, 2, 1)
            $corner = $cornerSequence[[int][Math]::Floor($FrameIndex / 2)]
            $cornerX = @(-1, 1, 1, -1)[$corner]
            $cornerY = @(-1, -1, 1, 1)[$corner]
            $showTarget = $true
            $targetX = 128 + ($cornerX * 66)
            $targetY = $headY - 7 + ($cornerY * 52)
            $leftPupilX = $cornerX * 8
            $rightPupilX = $leftPupilX
            $leftPupilY = $cornerY * 6
            $rightPupilY = $leftPupilY
            $targetPath = '<rect x="62" y="45" width="132" height="104" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'GazeClockSaccade' {
            $clockIndex = if ($FrameIndex -lt 8) { $FrameIndex } else { 15 - $FrameIndex }
            $clockPhase = 2 * [Math]::PI * $clockIndex / 8
            $clockX = [Math]::Sin($clockPhase)
            $clockY = -[Math]::Cos($clockPhase)
            $showTarget = $true
            $targetX = Get-RoundedInt (128 + ($clockX * 68))
            $targetY = Get-RoundedInt ($headY - 7 + ($clockY * 55))
            $leftPupilX = Get-RoundedInt ($clockX * 8)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt ($clockY * 6)
            $rightPupilY = $leftPupilY
        }
        'GazeTriangleSaccade' {
            $triangleSequence = @(0, 0, 1, 1, 1, 2, 2, 2, 0, 0, 2, 2, 2, 1, 1, 1)
            $triangleIndex = $triangleSequence[$FrameIndex]
            $targetX = @(128, 62, 194)[$triangleIndex]
            $targetY = @(40, 149, 149)[$triangleIndex]
            $showTarget = $true
            $leftPupilX = Get-RoundedInt (($targetX - 128) / 8.25)
            $rightPupilX = $leftPupilX
            $leftPupilY = Get-RoundedInt (($targetY - ($headY - 7)) / 9)
            $rightPupilY = $leftPupilY
            $targetPath = '<path d="M128 40 L62 149 L194 149 Z" fill="none" stroke="#E63946" stroke-width="2" stroke-dasharray="5 6" opacity="0.45" />'
        }
        'VorHorizontal' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $turn = $wave
            $leftPupilX = Get-RoundedInt (-$wave * 6)
            $rightPupilX = $leftPupilX
        }
        'VorVertical' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $leftPupilY = Get-RoundedInt (-$wave * 5)
            $rightPupilY = $leftPupilY
        }
        'VorFourDirection' {
            $showTarget = $true
            $targetX = 128
            $targetY = 37
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $localWave = [Math]::Sin($localPhase)
            if ($FrameIndex -lt 8) {
                $turn = $localWave
                $leftPupilX = Get-RoundedInt (-$localWave * 6)
                $rightPupilX = $leftPupilX
            }
            else {
                $sideProfile = $true
                $rotation = Get-RoundedInt ($localWave * 12)
                $leftPupilY = Get-RoundedInt (-$localWave * 5)
                $rightPupilY = $leftPupilY
            }
        }
        'VorCancellationHorizontal' {
            $showTarget = $true
            $turn = $wave
            $targetX = Get-RoundedInt (128 + ($wave * 62))
            $targetY = 37
        }
        'VorCancellationVertical' {
            $showTarget = $true
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $targetX = 128
            $targetY = Get-RoundedInt (37 + ($wave * 48))
        }
        'VorX2Horizontal' {
            $showTarget = $true
            $turn = $wave
            $targetX = Get-RoundedInt (128 - ($wave * 62))
            $targetY = 37
            $leftPupilX = Get-RoundedInt (-$wave * 8)
            $rightPupilX = $leftPupilX
        }
        'VorX2Vertical' {
            $showTarget = $true
            $sideProfile = $true
            $rotation = Get-RoundedInt ($wave * 12)
            $targetX = 128
            $targetY = Get-RoundedInt (37 - ($wave * 48))
            $leftPupilY = Get-RoundedInt (-$wave * 6)
            $rightPupilY = $leftPupilY
        }
    }

    $eyeRy = [Math]::Max(1, (Get-RoundedInt (9 * (1 - $blink))))
    $pupilRadius = [Math]::Max(0, (Get-RoundedInt (4 * (1 - $blink))))
    $eyeSpacing = Get-RoundedInt (18 - ([Math]::Abs($turn) * 7))
    $faceShift = Get-RoundedInt ($turn * 8)
    $leftEyeX = $headX - $eyeSpacing + $faceShift
    $rightEyeX = $headX + $eyeSpacing + $faceShift
    $eyeY = $headY - 7

    if ($sideProfile) {
        $eyesSvg = @"
    <ellipse cx="$($headX + 13)" cy="$($headY - 8)" rx="11" ry="$eyeRy" fill="white" />
    <circle cx="$($headX + 16 + $rightPupilX)" cy="$($headY - 8 + $rightPupilY)" r="$pupilRadius" fill="#17324D" />
    <path d="M$($headX + 42) $($headY - 4) L$($headX + 55) $($headY + 2) L$($headX + 42) $($headY + 7)" fill="$Accent" stroke="#17324D" stroke-width="3" stroke-linejoin="round" />
"@
    }
    else {
        $eyesSvg = @"
    <ellipse cx="$leftEyeX" cy="$eyeY" rx="12" ry="$eyeRy" fill="white" />
    <ellipse cx="$rightEyeX" cy="$eyeY" rx="12" ry="$eyeRy" fill="white" />
    <circle cx="$($leftEyeX + $leftPupilX)" cy="$($eyeY + $leftPupilY)" r="$pupilRadius" fill="#17324D" />
    <circle cx="$($rightEyeX + $rightPupilX)" cy="$($eyeY + $rightPupilY)" r="$pupilRadius" fill="#17324D" />
"@
    }

    $turnNose = if ([Math]::Abs($turn) -gt 0.1) {
        $noseEndX = Get-RoundedInt ($headX + ($turn * 35))
        $noseEndY = $headY + 4
        '<line x1="{0}" y1="{1}" x2="{2}" y2="{3}" stroke="#17324D" stroke-width="3" stroke-linecap="round" />' -f ($headX + $faceShift), $headY, $noseEndX, $noseEndY
    }
    else { '' }

    $targetPathSvg = if ($showTarget) { $targetPath } else { '' }
    $targetDotSvg = if ($showTarget) {
        '<circle cx="{0}" cy="{1}" r="{2}" fill="#E63946" stroke="white" stroke-width="2" />' -f $targetX, $targetY, $targetRadius
    }
    else { '' }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Nomadic Method real head exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  $targetPathSvg
  <path d="M62 220 Q72 166 108 158 L148 158 Q184 166 194 220" fill="#17324D" />
  <line x1="128" y1="160" x2="$headX" y2="$($headY + 40)" stroke="#17324D" stroke-width="18" stroke-linecap="round" />
  <g transform="rotate($rotation $headX $headY)">
    <circle cx="$headX" cy="$headY" r="49" fill="$Accent" />
    $eyesSvg
    $turnNose
    <line x1="$($headX - 9)" y1="$($headY + 22)" x2="$($headX + 9)" y2="$($headY + 22)" stroke="#17324D" stroke-width="4" stroke-linecap="round" />
  </g>
  $targetDotSvg
  <line x1="40" y1="224" x2="216" y2="224" stroke="#C9D7E3" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function Get-RoundedInt {
    param([double]$Value)
    return [int][Math]::Round($Value, [MidpointRounding]::AwayFromZero)
}

function Publish-GeneratedFile {
    param(
        [Parameter(Mandatory)]
        [string]$SourcePath,
        [Parameter(Mandatory)]
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        $sourceHash = (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash
        $destinationHash = (
            Get-FileHash -LiteralPath $DestinationPath -Algorithm SHA256).Hash
        if ($sourceHash -eq $destinationHash) {
            return $false
        }
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            Copy-Item `
                -LiteralPath $SourcePath `
                -Destination $DestinationPath `
                -Force `
                -ErrorAction Stop
            return $true
        }
        catch [System.IO.IOException] {
            $lastError = $_
            if ($attempt -lt 20) {
                Start-Sleep -Milliseconds 250
            }
        }
    }

    throw "Could not publish generated asset '$DestinationPath' after waiting " +
        "for a mapped-file lock to clear: $($lastError.Exception.Message)"
}

function New-ExternalExerciseGif {
    param(
        [int]$ExerciseId,
        [string]$ExerciseName,
        [string]$SideSequence,
        [hashtable]$Media,
        [string]$GifPath,
        [string]$WorkingRoot
    )

    # Source downloads are much larger than the generated assets. Keep them in
    # a stable temporary cache so an interrupted review run can resume without
    # downloading the same long human-demonstration videos again.
    $sourceRoot = Join-Path (
        [IO.Path]::GetTempPath()) 'NomadicMethodExerciseSourceCache'
    $frameRoot = Join-Path $WorkingRoot ('external-frames-{0:D4}' -f $ExerciseId)
    New-Item -ItemType Directory -Force -Path $sourceRoot, $frameRoot | Out-Null

    $hasLocalSource = $Media.ContainsKey('LocalSourceFile')
    $sourcePath = if ($hasLocalSource) {
        Resolve-ReviewedSourceMedia -Media $Media -ToolsRoot $PSScriptRoot
    }
    else {
        Join-Path $sourceRoot ([string]$Media.File)
    }
    $sourceUrl = if ($Media.ContainsKey('ArchiveUrl')) {
        [string]$Media.ArchiveUrl
    }
    elseif ($Media.ContainsKey('Url')) {
        [string]$Media.Url
    }
    else {
        'https://raw.githubusercontent.com/hasaneyldrm/' +
            'exercises-dataset/main/videos/' + $Media.File
    }
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        if ($Media.ContainsKey('ArchiveUrl')) {
            $archivePath = $sourcePath + '.zip'
            if (-not (Test-Path -LiteralPath $archivePath)) {
                Invoke-WebRequest `
                    -Uri $sourceUrl `
                    -Headers @{ 'User-Agent' = 'Nomadic Method private exercise catalog/1.0' } `
                    -OutFile $archivePath
            }

            $archiveEntry = [string]$Media.ArchiveEntry
            $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
            try {
                $entry = $archive.Entries | Where-Object {
                    $_.FullName -eq $archiveEntry
                } | Select-Object -First 1
                if ($null -eq $entry) {
                    throw "Archive entry '$archiveEntry' is missing for $ExerciseName."
                }
                [IO.Compression.ZipFileExtensions]::ExtractToFile(
                    $entry,
                    $sourcePath,
                    $true)
            }
            finally {
                $archive.Dispose()
            }
        }
        elseif ($Media.ContainsKey('Youtube') -and [bool]$Media.Youtube) {
            Save-YouTubeExerciseSource -Url $sourceUrl -Destination $sourcePath -Media $Media
        }
        else {
            Invoke-WebRequest `
                -Uri $sourceUrl `
                -Headers @{ 'User-Agent' = 'Nomadic Method private exercise catalog/1.0' } `
                -OutFile $sourcePath
        }
    }

    if ($Media.ContainsKey('Video') -and [bool]$Media.Video) {
        Assert-ExerciseSourceVideo -Path $sourcePath -Media $Media
    }

    $framePattern = Join-Path $frameRoot 'frame_%04d.png'
    $sourceFrameDelays = @()
    if ($Media.ContainsKey('Video') -and [bool]$Media.Video) {
        $culture = [Globalization.CultureInfo]::InvariantCulture
        $framesPerSecond = if ($Media.ContainsKey('FramesPerSecond')) {
            [int]$Media.FramesPerSecond
        }
        else {
            10
        }
        $ffmpegArguments = @('-hide_banner', '-loglevel', 'error', '-y')

        if ($Media.ContainsKey('StartSeconds')) {
            $ffmpegArguments += @(
                '-ss',
                ([double]$Media.StartSeconds).ToString('0.###', $culture))
        }

        $ffmpegArguments += @('-i', $sourcePath)

        if ($Media.ContainsKey('DurationSeconds')) {
            $ffmpegArguments += @(
                '-t',
                ([double]$Media.DurationSeconds).ToString('0.###', $culture))
        }

        $cropFilter = if ($Media.ContainsKey('Crop')) {
            ([string]$Media.Crop).TrimEnd(',') + ','
        }
        else {
            ''
        }
        $maskFilter = if ($Media.ContainsKey('MaskTop') -and
            [bool]$Media.MaskTop) {
            'drawbox=x=0:y=0:w=iw:h=ih*0.28:color=0xF7FAFC:t=fill,'
        }
        else {
            ''
        }
        $videoFilter = $cropFilter + $maskFilter + "fps=$framesPerSecond," +
            'scale=256:256:force_original_aspect_ratio=decrease,' +
            'pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0xF7FAFC'
        $ffmpegArguments += @('-vf', $videoFilter, '-an', $framePattern)
        & ffmpeg @ffmpegArguments
    }
    else {
        # PNG normalization drops animation timing. Keep each source delay
        # alongside its frame, including through mirroring and ping-pong.
        $sourceFrameDelays = @(& magick identify -format "%T`n" $sourcePath)
        if ($LASTEXITCODE -ne 0 -or $sourceFrameDelays.Count -lt 2) {
            throw "External media for $ExerciseName is not an animated image."
        }
        $sourceFrameDelays = @($sourceFrameDelays | ForEach-Object {
            $delay = 0
            if (-not [int]::TryParse([string]$_, [ref]$delay) -or $delay -lt 1) {
                throw "External media for $ExerciseName has an invalid frame delay."
            }
            $delay
        })
        & magick $sourcePath `
            -coalesce `
            -resize '256x256' `
            -background '#F7FAFC' `
            -gravity center `
            -extent '256x256' `
            $framePattern
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Could not normalize external media for $ExerciseName."
    }

    $framePaths = @(
        Get-ChildItem -LiteralPath $frameRoot -Filter 'frame_*.png' |
            Sort-Object Name |
            Select-Object -ExpandProperty FullName)

    if ($framePaths.Count -lt 2) {
        throw "External media for $ExerciseName is not animated."
    }
    if ($sourceFrameDelays.Count -gt 0 -and
        $sourceFrameDelays.Count -ne $framePaths.Count) {
        throw "External media for $ExerciseName lost frames during normalization."
    }

    if ($Media.MirrorForAlternation -and $SideSequence -notin @(
            'ScreenLeftThenRight', 'ScreenRightThenLeft',
            'ScreenLeftLeadThenRightLead',
            'ScreenRightLeadThenLeftLead')) {
        $mirroredPaths = [System.Collections.Generic.List[string]]::new()
        for ($index = 0; $index -lt $framePaths.Count; $index++) {
            $mirroredPath = Join-Path $frameRoot ('mirror_{0:D4}.png' -f $index)
            & magick $framePaths[$index] -flop $mirroredPath
            if ($LASTEXITCODE -ne 0) {
                throw "Could not mirror external media for $ExerciseName."
            }
            $mirroredPaths.Add($mirroredPath)
        }
        $framePaths += @($mirroredPaths)
        if ($sourceFrameDelays.Count -gt 0) {
            $sourceFrameDelays += @($sourceFrameDelays)
        }
    }

    $isPingPong = $Media.ContainsKey('PingPong') -and [bool]$Media.PingPong
    if ($isPingPong) {
        $returnPaths = [System.Collections.Generic.List[string]]::new()
        $returnDelays = [System.Collections.Generic.List[int]]::new()
        for ($index = $framePaths.Count - 2; $index -ge 1; $index--) {
            $returnPaths.Add($framePaths[$index])
            if ($sourceFrameDelays.Count -gt 0) {
                $returnDelays.Add($sourceFrameDelays[$index])
            }
        }
        $framePaths += @($returnPaths)
        $sourceFrameDelays += @($returnDelays)
    }

    $frameDelay = if ($Media.ContainsKey('DelayCentiseconds')) {
        ([int]$Media.DelayCentiseconds).ToString()
    }
    elseif ($Media.ContainsKey('Video') -and [bool]$Media.Video) {
        # GIF stores integer centiseconds. Round cumulative frame boundaries,
        # not a single repeated delay: 8 fps needs alternating 13/12 cs and
        # 12 fps needs 8/9/8 cs. Repeating 12 or 8 cs accelerates both by 4.17%.
        # ImageMagick's t is the index in the complete (possibly mirrored)
        # sequence, so the total duration stays within half a centisecond.
        '%[fx:floor((t+1)*100/{0}+0.5)-floor(t*100/{0}+0.5)]' -f $framesPerSecond
    }
    else {
        $null
    }

    $gifInputPaths = if ($isPingPong) {
        @($framePaths)
    }
    else {
        $patterns = @((Join-Path $frameRoot 'frame_*.png'))
        if ($Media.MirrorForAlternation -and $SideSequence -notin @(
                'ScreenLeftThenRight', 'ScreenRightThenLeft',
                'ScreenLeftLeadThenRightLead',
                'ScreenRightLeadThenLeftLead')) {
            $patterns += (Join-Path $frameRoot 'mirror_*.png')
        }
        $patterns
    }

    $temporaryGifPath = Join-Path $WorkingRoot (
        'external-exercise-{0:D4}.gif' -f $ExerciseId)
    $gifArguments = if ($null -eq $frameDelay) {
        # A single repeated delay would silently accelerate or slow an
        # external GIF with a variable cadence.
        @(
            for ($index = 0; $index -lt $framePaths.Count; $index++) {
                '-delay'
                $sourceFrameDelays[$index].ToString()
                $framePaths[$index]
            }
        )
    }
    else {
        @($gifInputPaths) + @('-set', 'delay', $frameDelay)
    }
    $gifArguments += @(
        '-set', 'dispose', 'background',
        '-set', 'comment', "Nomadic Method reviewed exercise $ExerciseId - $ExerciseName",
        '-loop', '0',
        '-layers', 'Optimize',
        $temporaryGifPath)
    & magick @gifArguments

    if ($LASTEXITCODE -ne 0 -or
        -not (Test-Path -LiteralPath $temporaryGifPath)) {
        throw "Could not encode external media for $ExerciseName."
    }

    $null = Publish-GeneratedFile `
        -SourcePath $temporaryGifPath `
        -DestinationPath $GifPath
}

function Get-ShoulderArmPoints {
    param(
        [int]$ShoulderX,
        [int]$ShoulderY,
        [ValidateSet(-1, 1)]
        [int]$Side,
        [double]$AngleFromDown,
        [double]$ElbowBend = 0
    )

    $upperLength = 34
    $forearmLength = 34
    $upperRadians = $AngleFromDown * [Math]::PI / 180
    $forearmRadians = ($AngleFromDown - $ElbowBend) * [Math]::PI / 180
    $elbowX = Get-RoundedInt (
        $ShoulderX + ($Side * [Math]::Sin($upperRadians) * $upperLength))
    $elbowY = Get-RoundedInt (
        $ShoulderY + ([Math]::Cos($upperRadians) * $upperLength))

    return @{
        ElbowX = $elbowX
        ElbowY = $elbowY
        HandX = Get-RoundedInt (
            $elbowX + ($Side * [Math]::Sin($forearmRadians) * $forearmLength))
        HandY = Get-RoundedInt (
            $elbowY + ([Math]::Cos($forearmRadians) * $forearmLength))
    }
}

function New-ShoulderExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [string]$ExerciseName,
        [int]$FrameIndex,
        [string]$Accent
    )

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $progress = (1 - [Math]::Cos($phase)) / 2
    $pulse = (1 + [Math]::Sin(2 * $phase)) / 2
    $leftShoulderX = 98
    $rightShoulderX = 158
    $shoulderY = 82
    $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 0
    $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 0
    $pathSvg = ''
    $scapulaSvg = ''
    $rearView = $false
    $handsJoined = $false
    $joinedHandX = 128
    $joinedHandY = 100
    $sideView = $false
    $secondSideArm = $false

    switch -Regex ($ExerciseName) {
        '^Shoulder Extension$|^Bilateral Shoulder Extension Pulse$' {
            $sideView = $true
            $angle = if ($ExerciseName -match 'Pulse') {
                -(30 + ($pulse * 18))
            }
            else { -($progress * 52) }
            $pathSvg = '<path d="M120 151 Q87 125 75 86" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.7" />'
        }
        '^Scapular Depression$' {
            $drop = Get-RoundedInt ($progress * 13)
            $shoulderY += $drop
            $leftArm.ElbowY += $drop
            $leftArm.HandY += $drop
            $rightArm.ElbowY += $drop
            $rightArm.HandY += $drop
            $pathSvg = '<path d="M82 65 V102 M174 65 V102 M76 94 L82 102 L88 94 M168 94 L174 102 L180 94" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.7" />'
        }
        '^Scapular Upward Rotation$|^Scapular Downward Rotation$' {
            $rearView = $true
            $direction = if ($ExerciseName -match 'Upward') { 1 } else { -1 }
            $rotation = Get-RoundedInt ($direction * $progress * 32)
            $scapulaSvg = @"
  <path d="M104 92 Q95 111 108 131 Q120 114 116 94 Z" fill="$Accent" opacity="0.75" transform="rotate($(-$rotation) 110 105)" />
  <path d="M152 92 Q161 111 148 131 Q136 114 140 94 Z" fill="$Accent" opacity="0.75" transform="rotate($rotation 146 105)" />
  <path d="M110 76 Q82 104 92 137 M146 76 Q174 104 164 137" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />
"@
        }
        '^Bent-Elbow Bidirectional Shoulder Rolls$|^Elbow Circle$' {
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $direction = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $circleX = Get-RoundedInt ([Math]::Sin($direction * $localPhase) * 12)
            $circleY = Get-RoundedInt ([Math]::Cos($direction * $localPhase) * 12)
            $leftArm = @{ ElbowX = 74 + $circleX; ElbowY = 82 + $circleY; HandX = 98; HandY = 110 }
            $rightArm = @{ ElbowX = 182 - $circleX; ElbowY = 82 - $circleY; HandX = 158; HandY = 110 }
            $pathSvg = '<ellipse cx="74" cy="82" rx="17" ry="17" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.65" /><ellipse cx="182" cy="82" rx="17" ry="17" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.65" />'
        }
        '^Straight-Arm Bidirectional Shoulder Rolls$' {
            $localFrame = $FrameIndex % 8
            $direction = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $angle = 90 + ($direction * $localFrame * 45)
            $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 $angle
            $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 $angle
            $pathSvg = '<circle cx="98" cy="82" r="68" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 6" opacity="0.55" /><circle cx="158" cy="82" r="68" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 6" opacity="0.55" />'
        }
        '^Bent-Elbow Shoulder Figure Eight$' {
            $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
            $direction = if ($FrameIndex -lt 8) { 1 } else { -1 }
            $angle = 70 + ([Math]::Sin($direction * $localPhase) * 48)
            $bend = [Math]::Sin(2 * $direction * $localPhase) * 42
            $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 $angle $bend
            $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 $angle $bend
            $pathSvg = '<path d="M42 84 C42 50 82 50 82 84 C82 118 122 118 122 84 C122 50 82 50 82 84 M134 84 C134 50 174 50 174 84 C174 118 214 118 214 84 C214 50 174 50 174 84" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.5" />'
        }
        '^Scapular Clock$|^Scapular Figure Eight$' {
            $rearView = $true
            if ($ExerciseName -match 'Clock') {
                $scapX = Get-RoundedInt ([Math]::Sin($phase) * 10)
                $scapY = Get-RoundedInt ([Math]::Cos($phase) * 13)
                $pathSvg = '<ellipse cx="109" cy="108" rx="10" ry="13" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.6" /><ellipse cx="147" cy="108" rx="10" ry="13" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.6" /><text x="106" y="87" font-size="10" fill="#E63946">12</text><text x="106" y="137" font-size="10" fill="#E63946">6</text>'
            }
            else {
                $scapX = Get-RoundedInt ([Math]::Sin($phase) * 11)
                $scapY = Get-RoundedInt ([Math]::Sin(2 * $phase) * 10)
                $pathSvg = '<path d="M91 108 C91 90 109 90 109 108 C109 126 127 126 127 108 C127 90 109 90 109 108 M129 108 C129 90 147 90 147 108 C147 126 165 126 165 108 C165 90 147 90 147 108" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="4 5" opacity="0.55" />'
            }
            $scapulaSvg = @"
  <path d="M$($scapX + 102) $($scapY + 92) Q$($scapX + 92) $($scapY + 110) $($scapX + 107) $($scapY + 128) Q$($scapX + 120) $($scapY + 108) $($scapX + 114) $($scapY + 94) Z" fill="$Accent" opacity="0.76" />
  <path d="M$($scapX + 154) $($scapY + 92) Q$($scapX + 164) $($scapY + 110) $($scapX + 149) $($scapY + 128) Q$($scapX + 136) $($scapY + 108) $($scapX + 142) $($scapY + 94) Z" fill="$Accent" opacity="0.76" />
"@
        }
        '^Standing [WYT] Raise$|^Bilateral Shoulder (Flexion|Abduction) Pulse$' {
            if ($ExerciseName -match 'Flexion') {
                $sideView = $true
                $angle = 66 + ($pulse * 20)
                $pathSvg = '<path d="M121 151 Q170 128 194 83" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.65" />'
            }
            else {
                $targetAngle = if ($ExerciseName -match 'W Raise') { 58 } elseif ($ExerciseName -match 'Y Raise') { 145 } else { 90 }
                $angle = if ($ExerciseName -match 'Pulse') { 78 + ($pulse * 18) } else { $progress * $targetAngle }
                $bend = if ($ExerciseName -match 'W Raise') { -92 * $progress } else { 0 }
                $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 $angle $bend
                $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 $angle $bend
                $pathSvg = if ($ExerciseName -match 'Y Raise') {
                    '<path d="M98 150 L55 25 M158 150 L201 25" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
                }
                elseif ($ExerciseName -match 'T Raise|Abduction') {
                    '<path d="M98 150 Q65 112 30 82 M158 150 Q191 112 226 82" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
                }
                else { '' }
            }
        }
        '^Goalpost Open-and-Close$' {
            $leftArm = @{ ElbowX = 62; ElbowY = 82; HandX = Get-RoundedInt (112 - (50 * $progress)); HandY = Get-RoundedInt (82 - (38 * $progress)) }
            $rightArm = @{ ElbowX = 194; ElbowY = 82; HandX = Get-RoundedInt (144 + (50 * $progress)); HandY = Get-RoundedInt (82 - (38 * $progress)) }
            $pathSvg = '<path d="M112 82 Q86 53 62 44 M144 82 Q170 53 194 44" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
        }
        '^Cactus-Arm Overhead Press$' {
            $leftArm = @{ ElbowX = Get-RoundedInt (62 + (31 * $progress)); ElbowY = Get-RoundedInt (82 - (28 * $progress)); HandX = Get-RoundedInt (62 + (50 * $progress)); HandY = Get-RoundedInt (44 - (30 * $progress)) }
            $rightArm = @{ ElbowX = Get-RoundedInt (194 - (31 * $progress)); ElbowY = Get-RoundedInt (82 - (28 * $progress)); HandX = Get-RoundedInt (194 - (50 * $progress)); HandY = Get-RoundedInt (44 - (30 * $progress)) }
        }
        '^Cactus-to-Y Flow$' {
            $leftArm = @{ ElbowX = Get-RoundedInt (62 + (11 * $progress)); ElbowY = Get-RoundedInt (82 - (28 * $progress)); HandX = Get-RoundedInt (62 - (7 * $progress)); HandY = Get-RoundedInt (44 - (19 * $progress)) }
            $rightArm = @{ ElbowX = Get-RoundedInt (194 - (11 * $progress)); ElbowY = Get-RoundedInt (82 - (28 * $progress)); HandX = Get-RoundedInt (194 + (7 * $progress)); HandY = Get-RoundedInt (44 - (19 * $progress)) }
            $pathSvg = '<path d="M62 44 L55 25 M194 44 L201 25" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
        }
        '^Standing W External Rotation$' {
            $leftArm = @{ ElbowX = 105; ElbowY = 118; HandX = Get-RoundedInt (116 - (43 * $progress)); HandY = Get-RoundedInt (84 + (18 * (1 - $progress))) }
            $rightArm = @{ ElbowX = 151; ElbowY = 118; HandX = Get-RoundedInt (140 + (43 * $progress)); HandY = Get-RoundedInt (84 + (18 * (1 - $progress))) }
        }
        '^Cuban Shoulder Rotation$' {
            $sequence = if ($FrameIndex -le 8) { $FrameIndex / 8.0 } else { (16 - $FrameIndex) / 8.0 }
            if ($sequence -lt 0.5) {
                $step = $sequence * 2
                $leftArm = @{ ElbowX = Get-RoundedInt (98 - (36 * $step)); ElbowY = Get-RoundedInt (116 - (34 * $step)); HandX = Get-RoundedInt (98 - (12 * $step)); HandY = Get-RoundedInt (150 - (34 * $step)) }
                $rightArm = @{ ElbowX = Get-RoundedInt (158 + (36 * $step)); ElbowY = Get-RoundedInt (116 - (34 * $step)); HandX = Get-RoundedInt (158 + (12 * $step)); HandY = Get-RoundedInt (150 - (34 * $step)) }
            }
            else {
                $step = ($sequence - 0.5) * 2
                $leftArm = @{ ElbowX = 62; ElbowY = 82; HandX = 86; HandY = Get-RoundedInt (116 - (72 * $step)) }
                $rightArm = @{ ElbowX = 194; ElbowY = 82; HandX = 170; HandY = Get-RoundedInt (116 - (72 * $step)) }
            }
            $pathSvg = '<path d="M98 150 Q62 134 62 82 L86 44 M158 150 Q194 134 194 82 L170 44" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.5" />'
        }
        '^(External|Internal) Rotation at Ninety Degrees$' {
            $rotationProgress = if ($ExerciseName -match '^External') { $progress } else { 1 - $progress }
            $leftArm = @{ ElbowX = 62; ElbowY = 82; HandX = 62; HandY = Get-RoundedInt (116 - (72 * $rotationProgress)) }
            $rightArm = @{ ElbowX = 194; ElbowY = 82; HandX = 194; HandY = Get-RoundedInt (116 - (72 * $rotationProgress)) }
            $pathSvg = '<path d="M62 116 A36 36 0 0 1 62 44 M194 116 A36 36 0 0 0 194 44" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.6" />'
        }
        '^Shoulder Halo$' {
            $joinedHandX = Get-RoundedInt (128 + ([Math]::Sin($phase) * 46))
            $joinedHandY = Get-RoundedInt (62 + ([Math]::Cos($phase) * 28))
            $leftArm = @{ ElbowX = 90; ElbowY = 77; HandX = $joinedHandX; HandY = $joinedHandY }
            $rightArm = @{ ElbowX = 166; ElbowY = 77; HandX = $joinedHandX; HandY = $joinedHandY }
            $handsJoined = $true
            $pathSvg = '<ellipse cx="128" cy="62" rx="46" ry="28" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.65" />'
        }
        '^Bilateral Shoulder Pendulum$|^Nordic-Ski Shoulder Swing$' {
            $sideView = $true
            $angle = $wave * 46
            $secondSideArm = $true
            $secondAngle = if ($ExerciseName -match 'Nordic') { -$angle } else { $angle }
            $pathSvg = '<path d="M76 137 Q122 72 188 137" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
        }
        '^Alternating Overhead Elbow Reach$' {
            $local = (1 - [Math]::Cos(2 * [Math]::PI * ($FrameIndex % 8) / 8)) / 2
            $leftActive = if ($FrameIndex -lt 8) { $local } else { 0 }
            $rightActive = if ($FrameIndex -ge 8) { $local } else { 0 }
            $leftArm = @{ ElbowX = Get-RoundedInt (98 - (20 * $leftActive)); ElbowY = Get-RoundedInt (116 - (88 * $leftActive)); HandX = Get-RoundedInt (98 + (30 * $leftActive)); HandY = Get-RoundedInt (150 - (42 * $leftActive)) }
            $rightArm = @{ ElbowX = Get-RoundedInt (158 + (20 * $rightActive)); ElbowY = Get-RoundedInt (116 - (88 * $rightActive)); HandX = Get-RoundedInt (158 - (30 * $rightActive)); HandY = Get-RoundedInt (150 - (42 * $rightActive)) }
        }
        '^Prayer-to-Overhead Flow$' {
            $joinedHandY = Get-RoundedInt (110 - (86 * $progress))
            $leftArm = @{ ElbowX = Get-RoundedInt (98 - (18 * $progress)); ElbowY = Get-RoundedInt (116 - (48 * $progress)); HandX = 128; HandY = $joinedHandY }
            $rightArm = @{ ElbowX = Get-RoundedInt (158 + (18 * $progress)); ElbowY = Get-RoundedInt (116 - (48 * $progress)); HandX = 128; HandY = $joinedHandY }
            $handsJoined = $true
            $pathSvg = '<path d="M128 110 V24" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.6" />'
        }
        '^Reverse-Prayer Shoulder Hold$' {
            $rearView = $true
            $holdProgress = [Math]::Min(1, $FrameIndex / 7.0)
            $joinedHandY = Get-RoundedInt (145 - (38 * $holdProgress))
            $leftArm = @{ ElbowX = Get-RoundedInt (98 - (18 * $holdProgress)); ElbowY = 116; HandX = 128; HandY = $joinedHandY }
            $rightArm = @{ ElbowX = Get-RoundedInt (158 + (18 * $holdProgress)); ElbowY = 116; HandX = 128; HandY = $joinedHandY }
            $handsJoined = $true
        }
        '^Hands-Behind-Back Shoulder Lift$' {
            $rearView = $true
            $joinedHandY = Get-RoundedInt (150 - (48 * $progress))
            $leftArm = @{ ElbowX = 90; ElbowY = 121; HandX = 128; HandY = $joinedHandY }
            $rightArm = @{ ElbowX = 166; ElbowY = 121; HandX = 128; HandY = $joinedHandY }
            $handsJoined = $true
        }
        '^Standing Scapular Push$' {
            $sideView = $true
            $angle = 90
            $sideReach = $progress * 27
            $pathSvg = '<path d="M122 82 H149 M190 92 H220" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.6" />'
        }
        '^Alternating Bear-Hug Shoulder Sweep$' {
            $topOffset = if ($FrameIndex -lt 8) { -8 } else { 8 }
            $leftArm = @{ ElbowX = Get-RoundedInt (64 + (52 * $progress)); ElbowY = Get-RoundedInt (82 + ($topOffset * $progress)); HandX = Get-RoundedInt (30 + (126 * $progress)); HandY = Get-RoundedInt (82 + ($topOffset * $progress)) }
            $rightArm = @{ ElbowX = Get-RoundedInt (192 - (52 * $progress)); ElbowY = Get-RoundedInt (82 - ($topOffset * $progress)); HandX = Get-RoundedInt (226 - (126 * $progress)); HandY = Get-RoundedInt (82 - ($topOffset * $progress)) }
        }
        '^Standing Butterfly Shoulder Recovery$' {
            $angle = 180 * $progress
            $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 $angle
            $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 $angle
            $pathSvg = '<path d="M98 150 Q24 85 98 14 M158 150 Q232 85 158 14" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 5" opacity="0.55" />'
        }
        '^Bilateral Shoulder Controlled Articular Rotation$|^Alternating Overhead Shoulder-CAR Flow$' {
            if ($ExerciseName -match 'Alternating') {
                $leftAngle = if ($FrameIndex -lt 8) { ($FrameIndex / 7.0) * 360 } else { 0 }
                $rightAngle = if ($FrameIndex -ge 8) { (($FrameIndex - 8) / 7.0) * 360 } else { 0 }
            }
            else {
                $leftAngle = ($FrameIndex / 15.0) * 360
                $rightAngle = $leftAngle
            }
            $leftArm = Get-ShoulderArmPoints $leftShoulderX $shoulderY -1 $leftAngle
            $rightArm = Get-ShoulderArmPoints $rightShoulderX $shoulderY 1 $rightAngle
            $pathSvg = '<circle cx="98" cy="82" r="68" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 6" opacity="0.55" /><circle cx="158" cy="82" r="68" fill="none" stroke="#E63946" stroke-width="3" stroke-dasharray="5 6" opacity="0.55" />'
        }
    }

    $body = '#17324D'
    $muted = '#C9D7E3'
    if ($sideView) {
        $sideShoulderX = if ($ExerciseName -eq 'Standing Scapular Push') {
            Get-RoundedInt (122 + ($sideReach * 0.45))
        }
        else { 122 }
        $sideShoulderY = 82
        $elbowX = Get-RoundedInt ($sideShoulderX + ([Math]::Sin($angle * [Math]::PI / 180) * 36))
        $elbowY = Get-RoundedInt ($sideShoulderY + ([Math]::Cos($angle * [Math]::PI / 180) * 36))
        $handX = Get-RoundedInt ($sideShoulderX + ([Math]::Sin($angle * [Math]::PI / 180) * 72) + $sideReach)
        $handY = Get-RoundedInt ($sideShoulderY + ([Math]::Cos($angle * [Math]::PI / 180) * 72))
        $secondArmSvg = if ($secondSideArm) {
            $secondElbowX = Get-RoundedInt (122 + ([Math]::Sin($secondAngle * [Math]::PI / 180) * 34))
            $secondElbowY = Get-RoundedInt (82 + ([Math]::Cos($secondAngle * [Math]::PI / 180) * 34))
            $secondHandX = Get-RoundedInt (122 + ([Math]::Sin($secondAngle * [Math]::PI / 180) * 68))
            $secondHandY = Get-RoundedInt (82 + ([Math]::Cos($secondAngle * [Math]::PI / 180) * 68))
            '<polyline points="122,82 {0},{1} {2},{3}" fill="none" stroke="{4}" stroke-width="8" stroke-linecap="round" stroke-linejoin="round" opacity="0.68" />' -f $secondElbowX, $secondElbowY, $secondHandX, $secondHandY, $Accent
        }
        else { '' }
        return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Nomadic Method exact shoulder exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  $pathSvg
  <line x1="38" y1="224" x2="218" y2="224" stroke="$muted" stroke-width="4" stroke-linecap="round" />
  <polyline points="114,154 104,190 96,224" fill="none" stroke="$body" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="126,154 138,190 151,224" fill="none" stroke="$body" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <line x1="120" y1="82" x2="120" y2="156" stroke="$body" stroke-width="17" stroke-linecap="round" />
  <circle cx="120" cy="48" r="20" fill="#F4A261" />
  <path d="M124 48 L143 54 L124 59" fill="$Accent" stroke="$body" stroke-width="3" stroke-linejoin="round" />
  <circle cx="$sideShoulderX" cy="82" r="9" fill="$Accent" />
  $secondArmSvg
  <polyline points="$sideShoulderX,82 $elbowX,$elbowY $handX,$handY" fill="none" stroke="$body" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <circle cx="$handX" cy="$handY" r="7" fill="#F4A261" />
</svg>
"@
    }

    $faceDetails = if ($rearView) {
        '<path d="M112 48 Q128 35 144 48" fill="none" stroke="#17324D" stroke-width="5" stroke-linecap="round" />'
    }
    else {
        '<circle cx="121" cy="48" r="3" fill="#17324D" /><circle cx="135" cy="48" r="3" fill="#17324D" /><line x1="122" y1="62" x2="134" y2="62" stroke="#17324D" stroke-width="3" stroke-linecap="round" />'
    }
    $joinedHandsSvg = if ($handsJoined) {
        '<circle cx="{0}" cy="{1}" r="9" fill="#F4A261" stroke="{2}" stroke-width="3" />' -f $joinedHandX, $joinedHandY, $Accent
    }
    else { '' }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Nomadic Method exact shoulder exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$Accent" opacity="0.08" />
  $pathSvg
  <line x1="38" y1="224" x2="218" y2="224" stroke="$muted" stroke-width="4" stroke-linecap="round" />
  <polyline points="116,154 102,190 92,224" fill="none" stroke="$body" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="140,154 154,190 164,224" fill="none" stroke="$body" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <path d="M98 $shoulderY Q128 72 158 $shoulderY L142 157 L114 157 Z" fill="$body" />
  $scapulaSvg
  <line x1="$leftShoulderX" y1="$shoulderY" x2="$rightShoulderX" y2="$shoulderY" stroke="$Accent" stroke-width="13" stroke-linecap="round" />
  <circle cx="$leftShoulderX" cy="$shoulderY" r="9" fill="$Accent" />
  <circle cx="$rightShoulderX" cy="$shoulderY" r="9" fill="$Accent" />
  <polyline points="$leftShoulderX,$shoulderY $($leftArm.ElbowX),$($leftArm.ElbowY) $($leftArm.HandX),$($leftArm.HandY)" fill="none" stroke="$body" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="$rightShoulderX,$shoulderY $($rightArm.ElbowX),$($rightArm.ElbowY) $($rightArm.HandX),$($rightArm.HandY)" fill="none" stroke="$body" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <circle cx="$($leftArm.HandX)" cy="$($leftArm.HandY)" r="7" fill="#F4A261" />
  <circle cx="$($rightArm.HandX)" cy="$($rightArm.HandY)" r="7" fill="#F4A261" />
  $joinedHandsSvg
  <circle cx="128" cy="49" r="21" fill="#F4A261" />
  $faceDetails
</svg>
"@
}

function New-ExerciseFrameSvg {
    param(
        [int]$ExerciseId,
        [int]$RegionIndex,
        [string]$ExerciseName,
        [string]$MotionProfile,
        [int]$MovementIndex,
        [int]$FrameIndex
    )

    $accent = $regionColors[$RegionIndex]

    if ($regions[$RegionIndex] -eq 'HANDS') {
        return New-HandExerciseFrameSvg `
            -ExerciseId $ExerciseId `
            -MotionProfile $MotionProfile `
            -MovementIndex $MovementIndex `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    if ($regions[$RegionIndex] -eq 'HEAD') {
        return New-HeadExerciseFrameSvg `
            -ExerciseId $ExerciseId `
            -ExerciseName $ExerciseName `
            -MotionProfile $MotionProfile `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    if ($regions[$RegionIndex] -eq 'SHOULDERS') {
        return New-ShoulderExerciseFrameSvg `
            -ExerciseId $ExerciseId `
            -ExerciseName $ExerciseName `
            -FrameIndex $FrameIndex `
            -Accent $accent
    }

    $phase = 2 * [Math]::PI * $FrameIndex / 16
    $wave = [Math]::Sin($phase)
    $counterWave = [Math]::Cos($phase)
    if ($ExerciseName -match 'Bidirectional') {
        $localPhase = 2 * [Math]::PI * ($FrameIndex % 8) / 8
        $direction = if ($FrameIndex -lt 8) { 1 } else { -1 }
        $wave = [Math]::Sin($direction * $localPhase)
        $counterWave = [Math]::Cos($direction * $localPhase)
    }
    $leftAction = [Math]::Max(0, $wave)
    $rightAction = [Math]::Max(0, -$wave)
    $alternatingAction = [Math]::Abs($wave)
    $amplitude = 7.0 + (($MovementIndex % 4) * 1.4)

    $headX = 128
    $headY = 43
    $noseOffsetX = 15
    $noseOffsetY = 2
    $leftShoulderX = 102
    $leftShoulderY = 79
    $rightShoulderX = 154
    $rightShoulderY = 79
    $leftElbowX = 91
    $leftElbowY = 116
    $rightElbowX = 165
    $rightElbowY = 116
    $leftHandX = 85
    $leftHandY = 151
    $rightHandX = 171
    $rightHandY = 151
    $hipX = 128
    $hipY = 134
    $leftKneeX = 106
    $leftKneeY = 176
    $rightKneeX = 150
    $rightKneeY = 176
    $leftFootX = 92
    $leftFootY = 220
    $rightFootX = 164
    $rightFootY = 220

    switch ($regions[$RegionIndex]) {
        'FEET' {
            switch ($MotionProfile) {
                'AnkleCircle' {
                    $sideFrame = $FrameIndex % 8
                    $sidePhase = 2 * [Math]::PI * $sideFrame / 8
                    $sideWave = [Math]::Sin($sidePhase)
                    $sideCounterWave = [Math]::Cos($sidePhase)
                    if ($FrameIndex -lt 8) {
                        $leftFootX += Get-RoundedInt ($sideWave * $amplitude)
                        $leftFootY -= Get-RoundedInt ((1 - $sideCounterWave) * $amplitude * 0.35)
                    }
                    else {
                        $rightFootX -= Get-RoundedInt ($sideWave * $amplitude)
                        $rightFootY -= Get-RoundedInt ((1 - $sideCounterWave) * $amplitude * 0.35)
                    }
                }
                'HeelRaise' {
                    $rise = [Math]::Max(0, $wave) * $amplitude
                    $hipY -= Get-RoundedInt $rise
                    $leftKneeY -= Get-RoundedInt $rise
                    $rightKneeY -= Get-RoundedInt $rise
                    $leftFootY -= Get-RoundedInt ($rise * 0.15)
                    $rightFootY -= Get-RoundedInt ($rise * 0.15)
                }
                'ForefootRaise' {
                    $leftFootY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 0.65)
                    $rightFootY -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude * 0.65)
                }
                'AnkleSweep' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude * 1.2)
                    $rightFootX -= Get-RoundedInt ($wave * $amplitude * 1.2)
                }
                'FootBalance' {
                    $leftLift = $leftAction * $amplitude * 2.4
                    $rightLift = $rightAction * $amplitude * 2.4
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.85)
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.85)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude)
                }
                'WeightShift' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 0.7)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 0.7)
                }
                'TurnStep' {
                    $leftFootX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightFootX += Get-RoundedInt ($counterWave * $amplitude * 1.2)
                    $hipX += Get-RoundedInt ($wave * $amplitude * 0.6)
                }
                'CrossStep' {
                    $leftFootX += Get-RoundedInt ((1 + $wave) * $amplitude * 1.7)
                    $rightFootX -= Get-RoundedInt ((1 - $wave) * $amplitude * 1.1)
                }
                'SideStep' {
                    $leftFootX -= Get-RoundedInt ((1 + $wave) * $amplitude)
                    $rightFootX += Get-RoundedInt ((1 - $wave) * $amplitude)
                }
                default {
                    $leftLift = [Math]::Max(0, $wave) * $amplitude * 1.25
                    $rightLift = [Math]::Max(0, -$wave) * $amplitude * 1.25
                    $leftFootY -= Get-RoundedInt $leftLift
                    $rightFootY -= Get-RoundedInt $rightLift
                    $leftFootX += Get-RoundedInt ($wave * $amplitude)
                    $rightFootX -= Get-RoundedInt ($wave * $amplitude)
                }
            }
        }
        'LEGS' {
            switch ($MotionProfile) {
                'Squat' {
                    $bend = [Math]::Max(0, $wave) * $amplitude * 2.1
                    $hipY += Get-RoundedInt $bend
                    $leftKneeX -= Get-RoundedInt ($bend * 0.55)
                    $rightKneeX += Get-RoundedInt ($bend * 0.55)
                    $leftKneeY += Get-RoundedInt ($bend * 0.28)
                    $rightKneeY += Get-RoundedInt ($bend * 0.28)
                }
                'HeelRaise' {
                    $baseBend = $amplitude * 0.8
                    $rise = [Math]::Max(0, $wave) * $amplitude * 1.5
                    $hipY += Get-RoundedInt ($baseBend - $rise)
                    $leftKneeX -= Get-RoundedInt ($baseBend * 0.45)
                    $rightKneeX += Get-RoundedInt ($baseBend * 0.45)
                    $leftKneeY += Get-RoundedInt (($baseBend * 0.25) - $rise)
                    $rightKneeY += Get-RoundedInt (($baseBend * 0.25) - $rise)
                    $leftFootY -= Get-RoundedInt ($rise * 0.15)
                    $rightFootY -= Get-RoundedInt ($rise * 0.15)
                }
                'Lunge' {
                    $spread = $alternatingAction * $amplitude * 2.2
                    $leftFootX -= Get-RoundedInt ($leftAction * $spread)
                    $rightFootX += Get-RoundedInt ($leftAction * $spread)
                    $leftFootX += Get-RoundedInt ($rightAction * $spread)
                    $rightFootX -= Get-RoundedInt ($rightAction * $spread)
                    $leftKneeY += Get-RoundedInt ($rightAction * $amplitude * 1.35)
                    $rightKneeY += Get-RoundedInt ($leftAction * $amplitude * 1.35)
                    $hipY += Get-RoundedInt ($alternatingAction * $amplitude * 0.75)
                }
                'KneeCurl' {
                    $leftLift = $leftAction * $amplitude * 2.5
                    $rightLift = $rightAction * $amplitude * 2.5
                    $leftFootY -= Get-RoundedInt $leftLift
                    $leftFootX += Get-RoundedInt ($leftLift * 0.45)
                    $rightFootY -= Get-RoundedInt $rightLift
                    $rightFootX -= Get-RoundedInt ($rightLift * 0.45)
                }
                'KneeLift' {
                    $leftLift = $leftAction * $amplitude * 3.2
                    $rightLift = $rightAction * $amplitude * 3.2
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.72)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 0.7)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.72)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 0.7)
                }
                'LegSide' {
                    $leftFootX -= Get-RoundedInt ($leftAction * $amplitude * 3.1)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.25)
                    $leftKneeX -= Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $rightFootX += Get-RoundedInt ($rightAction * $amplitude * 3.1)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.25)
                    $rightKneeX += Get-RoundedInt ($rightAction * $amplitude * 1.6)
                }
                'LegBack' {
                    $leftFootX -= Get-RoundedInt ($leftAction * $amplitude * 2.1)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.55)
                    $rightFootX += Get-RoundedInt ($rightAction * $amplitude * 2.1)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.55)
                    $hipX += Get-RoundedInt (($leftAction - $rightAction) * $amplitude * 0.65)
                }
                'LegFront' {
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude * 2.8)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 2.1)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 1.4)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude * 2.8)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 2.1)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 1.4)
                }
                default {
                    $leftLift = $leftAction * $amplitude * 2.6
                    $rightLift = $rightAction * $amplitude * 2.6
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.8)
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.8)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude)
                }
            }
        }
        'HANDS' {
            $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $leftHandY += Get-RoundedInt ($wave * $amplitude)
            $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 1.5)
            $rightHandY -= Get-RoundedInt ($wave * $amplitude)
        }
        'ARMS' {
            switch ($MotionProfile) {
                'ArmCircle' {
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 3)
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 3)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3)
                }
                'StraightPunch' {
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 3.3)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 0.7)
                    $leftElbowX += Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 3.3)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 0.7)
                    $rightElbowX -= Get-RoundedInt ($rightAction * $amplitude * 1.6)
                }
                'BentArmStrike' {
                    $leftElbowY -= Get-RoundedInt ($leftAction * $amplitude * 2.5)
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 1.8)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $rightElbowY -= Get-RoundedInt ($rightAction * $amplitude * 2.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 1.8)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
                }
                'ArmBlock' {
                    $leftElbowX += Get-RoundedInt ($leftAction * $amplitude * 1.4)
                    $leftElbowY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 2.2)
                    $leftHandY -= Get-RoundedInt ($leftAction * $amplitude * 2.7)
                    $rightElbowX -= Get-RoundedInt ($rightAction * $amplitude * 1.4)
                    $rightElbowY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 2.2)
                    $rightHandY -= Get-RoundedInt ($rightAction * $amplitude * 2.7)
                }
                'FlowingArms' {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2.4)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 2)
                    $rightHandX += Get-RoundedInt ($wave * $amplitude * 1.8)
                    $rightHandY += Get-RoundedInt ($counterWave * $amplitude * 1.5)
                }
                'PortDeBras' {
                    $leftHandX -= Get-RoundedInt ($wave * $amplitude * 1.7)
                    $leftHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 2.7)
                    $rightHandX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 2.7)
                }
                'SportArmCycle' {
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 2.7)
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3.2)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 2.7)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3.2)
                }
                'ArmRaise' {
                    $raise = (1 + $wave) * $amplitude * 2.5
                    $leftHandY -= Get-RoundedInt $raise
                    $rightHandY -= Get-RoundedInt $raise
                    $leftElbowY -= Get-RoundedInt ($raise * 0.5)
                    $rightElbowY -= Get-RoundedInt ($raise * 0.5)
                }
                'ElbowMotion' {
                    $leftHandY -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.5)
                    $rightHandY -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude * 2.5)
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude)
                    $rightHandX -= Get-RoundedInt ([Math]::Max(0, -$wave) * $amplitude)
                }
                default {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2.2)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 2.5)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 2.2)
                    $rightHandY += Get-RoundedInt ($wave * $amplitude * 2.5)
                }
            }
        }
        'HEAD' {
            $headX += Get-RoundedInt ($wave * $amplitude * 1.2)
            $headY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
            $noseOffsetX = Get-RoundedInt (15 + ($counterWave * 3))
            $noseOffsetY = Get-RoundedInt ($wave * 7)
        }
        'SHOULDERS' {
            switch ($MotionProfile) {
                'ShoulderCircle' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $leftShoulderY += Get-RoundedInt ($counterWave * $amplitude)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderY -= Get-RoundedInt ($counterWave * $amplitude)
                }
                'ScapularGlide' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude)
                    $leftElbowX += Get-RoundedInt ($wave * $amplitude)
                    $rightElbowX -= Get-RoundedInt ($wave * $amplitude)
                }
                'ShoulderRotation' {
                    $leftElbowY -= Get-RoundedInt ($wave * $amplitude * 1.6)
                    $rightElbowY -= Get-RoundedInt ($wave * $amplitude * 1.6)
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 1.8)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 1.8)
                }
                'ShoulderStretch' {
                    $leftHandX += Get-RoundedInt ((1 + $wave) * $amplitude * 1.3)
                    $leftHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.8)
                    $rightHandX -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.3)
                    $rightHandY -= Get-RoundedInt ((1 + $wave) * $amplitude * 1.8)
                }
                'ShoulderDance' {
                    $leftShoulderY += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderY -= Get-RoundedInt ($wave * $amplitude)
                    $leftElbowY += Get-RoundedInt ($wave * $amplitude)
                    $rightElbowY -= Get-RoundedInt ($wave * $amplitude)
                }
                'ShoulderCycle' {
                    $leftHandY += Get-RoundedInt ($counterWave * $amplitude * 3)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 3)
                    $leftHandX += Get-RoundedInt ($wave * $amplitude * 2)
                    $rightHandX -= Get-RoundedInt ($wave * $amplitude * 2)
                }
                default {
                    $raise = (1 + $wave) * $amplitude * 2
                    $leftHandY -= Get-RoundedInt $raise
                    $rightHandY -= Get-RoundedInt $raise
                    $leftElbowY -= Get-RoundedInt ($raise * 0.55)
                    $rightElbowY -= Get-RoundedInt ($raise * 0.55)
                }
            }
        }
        'HIPS' {
            switch ($MotionProfile) {
                'HipCircle' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.7)
                    $hipY += Get-RoundedInt ($counterWave * $amplitude * 0.85)
                }
                'PelvicTilt' {
                    $hipY += Get-RoundedInt ($wave * $amplitude)
                    $torsoShift = Get-RoundedInt ($wave * $amplitude * 0.45)
                    $leftShoulderX -= $torsoShift
                    $rightShoulderX -= $torsoShift
                }
                'HipShift' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.9)
                    $leftKneeX += Get-RoundedInt ($wave * $amplitude * 0.75)
                    $rightKneeX += Get-RoundedInt ($wave * $amplitude * 0.75)
                }
                'HipHinge' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.2)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += $lean
                    $hipX -= Get-RoundedInt ($lean * 0.35)
                }
                'HipLegArc' {
                    $leftFootX += Get-RoundedInt ($leftAction * $amplitude * 2.5)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.15)
                    $leftKneeX += Get-RoundedInt ($leftAction * $amplitude * 1.25)
                    $rightFootX -= Get-RoundedInt ($rightAction * $amplitude * 2.5)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.15)
                    $rightKneeX -= Get-RoundedInt ($rightAction * $amplitude * 1.25)
                }
                'HipRotation' {
                    $hipX += Get-RoundedInt ($wave * $amplitude * 1.2)
                    $leftShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $rightShoulderX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                    $leftKneeX += Get-RoundedInt ($counterWave * $amplitude * 0.75)
                    $rightKneeX -= Get-RoundedInt ($counterWave * $amplitude * 0.75)
                }
                default {
                    $leftKneeX -= Get-RoundedInt ($leftAction * $amplitude * 1.6)
                    $leftKneeY -= Get-RoundedInt ($leftAction * $amplitude * 1.3)
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude)
                    $rightKneeX += Get-RoundedInt ($rightAction * $amplitude * 1.6)
                    $rightKneeY -= Get-RoundedInt ($rightAction * $amplitude * 1.3)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude)
                }
            }
        }
        'CHEST' {
            switch ($MotionProfile) {
                'ChestBreathing' {
                    $spread = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.25)
                    $leftShoulderX -= $spread
                    $rightShoulderX += $spread
                    $leftElbowX -= Get-RoundedInt ($spread * 0.6)
                    $rightElbowX += Get-RoundedInt ($spread * 0.6)
                }
                'ChestCircle' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $leftShoulderY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
                    $rightShoulderY += Get-RoundedInt ($counterWave * $amplitude * 0.55)
                }
                'ChestPress' {
                    $leftHandX += Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.4)
                    $rightHandX -= Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.4)
                    $leftHandY -= Get-RoundedInt ($amplitude * 0.7)
                    $rightHandY -= Get-RoundedInt ($amplitude * 0.7)
                }
                'ChestFly' {
                    $spread = Get-RoundedInt ($wave * $amplitude * 1.8)
                    $leftHandX -= $spread
                    $rightHandX += $spread
                    $leftElbowX -= Get-RoundedInt ($spread * 0.7)
                    $rightElbowX += Get-RoundedInt ($spread * 0.7)
                }
                'ChestOpen' {
                    $spread = Get-RoundedInt ((1 + $wave) * $amplitude)
                    $leftShoulderX -= $spread
                    $rightShoulderX += $spread
                    $leftElbowX -= $spread
                    $rightElbowX += $spread
                    $leftHandY -= Get-RoundedInt ($spread * 0.7)
                    $rightHandY -= Get-RoundedInt ($spread * 0.7)
                }
                'ChestFlow' {
                    $leftHandX += Get-RoundedInt ($counterWave * $amplitude * 2)
                    $leftHandY -= Get-RoundedInt ($wave * $amplitude * 1.7)
                    $rightHandX -= Get-RoundedInt ($counterWave * $amplitude * 2)
                    $rightHandY += Get-RoundedInt ($wave * $amplitude * 1.7)
                }
                default {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude)
                }
            }
        }
        'BACK' {
            switch ($MotionProfile) {
                'SpineFlexion' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.25)
                    $leftShoulderY += Get-RoundedInt ($lean * 0.35)
                    $rightShoulderY += Get-RoundedInt ($lean * 0.35)
                }
                'SpineExtension' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 1.45)
                    $leftShoulderX -= $lean
                    $rightShoulderX -= $lean
                    $headX -= Get-RoundedInt ($lean * 1.2)
                    $leftHandY -= $lean
                    $rightHandY -= $lean
                }
                'SpineSideBend' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.6)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.25)
                    $leftHandY += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $rightHandY -= Get-RoundedInt ($wave * $amplitude * 1.5)
                }
                'SpineRotation' {
                    $twist = Get-RoundedInt ($wave * $amplitude * 1.35)
                    $leftShoulderX += $twist
                    $rightShoulderX += $twist
                    $leftHandX += Get-RoundedInt ($twist * 1.4)
                    $rightHandX += Get-RoundedInt ($twist * 1.4)
                }
                'SpineWave' {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 1.3)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 1.3)
                    $headX += Get-RoundedInt ($counterWave * $amplitude)
                    $hipX -= Get-RoundedInt ($wave * $amplitude * 0.8)
                }
                'BackHinge' {
                    $lean = Get-RoundedInt ([Math]::Max(0, $wave) * $amplitude * 2.5)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += $lean
                    $hipX -= Get-RoundedInt ($lean * 0.4)
                }
                'BackArmPull' {
                    $leftElbowX -= Get-RoundedInt ($wave * $amplitude * 1.5)
                    $rightElbowX += Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftHandY -= Get-RoundedInt ($counterWave * $amplitude * 1.6)
                    $rightHandY -= Get-RoundedInt ($counterWave * $amplitude * 1.6)
                }
                'BoxingEvasion' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.8)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.3)
                    $hipY += Get-RoundedInt ([Math]::Abs($wave) * $amplitude)
                }
                default {
                    $leftFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.7)
                    $rightFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.7)
                    $leftHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 2)
                    $rightHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 2)
                }
            }
        }
        'CORE' {
            switch ($MotionProfile) {
                'CoreCrunch' {
                    $rightLift = $leftAction * $amplitude * 2.5
                    $leftLift = $rightAction * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.75)
                    $rightKneeX -= Get-RoundedInt ($rightLift * 0.38)
                    $leftElbowY += Get-RoundedInt ($rightLift * 0.5)
                    $leftElbowX += Get-RoundedInt ($rightLift * 0.35)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.75)
                    $leftKneeX += Get-RoundedInt ($leftLift * 0.38)
                    $rightElbowY += Get-RoundedInt ($leftLift * 0.5)
                    $rightElbowX -= Get-RoundedInt ($leftLift * 0.35)
                }
                'CoreSideBend' {
                    $lean = Get-RoundedInt ($wave * $amplitude * 1.5)
                    $leftShoulderX += $lean
                    $rightShoulderX += $lean
                    $headX += Get-RoundedInt ($lean * 1.2)
                    $hipX -= Get-RoundedInt ($lean * 0.5)
                }
                'CoreRotation' {
                    $twist = Get-RoundedInt ($wave * $amplitude * 1.4)
                    $leftShoulderX += $twist
                    $rightShoulderX += $twist
                    $leftHandX += Get-RoundedInt ($twist * 1.5)
                    $rightHandX += Get-RoundedInt ($twist * 1.5)
                    $hipX -= Get-RoundedInt ($twist * 0.6)
                }
                'CoreBalance' {
                    $rightLift = $leftAction * $amplitude * 2.5
                    $leftLift = $rightAction * $amplitude * 2.5
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.8)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.8)
                    $leftHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 0.7)
                    $rightHandY -= Get-RoundedInt ($alternatingAction * $amplitude * 0.7)
                }
                'CoreCrossCrawl' {
                    $rightLift = [Math]::Max(0, $wave) * $amplitude * 2.3
                    $leftLift = [Math]::Max(0, -$wave) * $amplitude * 2.3
                    $rightKneeY -= Get-RoundedInt $rightLift
                    $rightFootY -= Get-RoundedInt ($rightLift * 0.7)
                    $leftKneeY -= Get-RoundedInt $leftLift
                    $leftFootY -= Get-RoundedInt ($leftLift * 0.7)
                    $leftHandY += Get-RoundedInt ($rightLift * 0.55)
                    $rightHandY += Get-RoundedInt ($leftLift * 0.55)
                }
                'CoreIsometric' {
                    $leftHandX += Get-RoundedInt ($leftAction * $amplitude * 2.8)
                    $rightKneeY -= Get-RoundedInt ($leftAction * $amplitude * 2.4)
                    $rightFootY -= Get-RoundedInt ($leftAction * $amplitude * 1.5)
                    $rightHandX -= Get-RoundedInt ($rightAction * $amplitude * 2.8)
                    $leftKneeY -= Get-RoundedInt ($rightAction * $amplitude * 2.4)
                    $leftFootY -= Get-RoundedInt ($rightAction * $amplitude * 1.5)
                }
                default {
                    $leftShoulderX += Get-RoundedInt ($wave * $amplitude * 0.55)
                    $rightShoulderX += Get-RoundedInt ($wave * $amplitude * 0.55)
                    $hipX -= Get-RoundedInt ($wave * $amplitude * 0.35)
                }
            }
        }
    }

    $torsoTopX = Get-RoundedInt (($leftShoulderX + $rightShoulderX) / 2)
    $torsoTopY = Get-RoundedInt (($leftShoulderY + $rightShoulderY) / 2)
    $leftHipX = $hipX - 12
    $rightHipX = $hipX + 12
    $body = '#17324D'
    $skin = '#F4A261'
    $muted = '#C9D7E3'

    $feetColor = if ($RegionIndex -eq 0) { $accent } else { $body }
    $legsColor = if ($RegionIndex -eq 1) { $accent } else { $body }
    $handsColor = if ($RegionIndex -eq 2) { $accent } else { $skin }
    $armsColor = if ($RegionIndex -eq 3) { $accent } else { $body }
    $headColor = if ($RegionIndex -eq 4) { $accent } else { $skin }
    $shoulderColor = if ($RegionIndex -eq 5) { $accent } else { $body }
    $hipColor = if ($RegionIndex -eq 6) { $accent } else { $body }
    $chestColor = if ($RegionIndex -eq 7) { $accent } else { $body }
    $backColor = if ($RegionIndex -eq 8) { $accent } else { $body }
    $coreColor = if ($RegionIndex -eq 9) { $accent } else { $body }

    return @"
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <metadata>Nomadic Method exercise $ExerciseId frame $FrameIndex</metadata>
  <rect width="256" height="256" rx="28" fill="#F7FAFC" />
  <circle cx="128" cy="126" r="102" fill="$accent" opacity="0.08" />
  <line x1="40" y1="224" x2="216" y2="224" stroke="$muted" stroke-width="4" stroke-linecap="round" />
  <polyline points="$leftHipX,$hipY $leftKneeX,$leftKneeY $leftFootX,$leftFootY" fill="none" stroke="$legsColor" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="$rightHipX,$hipY $rightKneeX,$rightKneeY $rightFootX,$rightFootY" fill="none" stroke="$legsColor" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
  <line x1="$($leftFootX - 12)" y1="$leftFootY" x2="$($leftFootX + 9)" y2="$leftFootY" stroke="$feetColor" stroke-width="9" stroke-linecap="round" />
  <line x1="$($rightFootX - 12)" y1="$rightFootY" x2="$($rightFootX + 9)" y2="$rightFootY" stroke="$feetColor" stroke-width="9" stroke-linecap="round" />
  <line x1="$torsoTopX" y1="$torsoTopY" x2="$hipX" y2="$hipY" stroke="$backColor" stroke-width="15" stroke-linecap="round" />
  <line x1="$torsoTopX" y1="$($torsoTopY + 15)" x2="$hipX" y2="$($hipY - 8)" stroke="$coreColor" stroke-width="9" stroke-linecap="round" opacity="0.95" />
  <line x1="$leftShoulderX" y1="$leftShoulderY" x2="$rightShoulderX" y2="$rightShoulderY" stroke="$chestColor" stroke-width="13" stroke-linecap="round" />
  <circle cx="$leftShoulderX" cy="$leftShoulderY" r="8" fill="$shoulderColor" />
  <circle cx="$rightShoulderX" cy="$rightShoulderY" r="8" fill="$shoulderColor" />
  <polyline points="$leftShoulderX,$leftShoulderY $leftElbowX,$leftElbowY $leftHandX,$leftHandY" fill="none" stroke="$armsColor" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <polyline points="$rightShoulderX,$rightShoulderY $rightElbowX,$rightElbowY $rightHandX,$rightHandY" fill="none" stroke="$armsColor" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
  <circle cx="$leftHandX" cy="$leftHandY" r="7" fill="$handsColor" />
  <circle cx="$rightHandX" cy="$rightHandY" r="7" fill="$handsColor" />
  <circle cx="$hipX" cy="$hipY" r="10" fill="$hipColor" />
  <line x1="$torsoTopX" y1="$($torsoTopY - 3)" x2="$headX" y2="$($headY + 16)" stroke="$body" stroke-width="8" stroke-linecap="round" />
  <circle cx="$headX" cy="$headY" r="18" fill="$headColor" />
  <line x1="$headX" y1="$headY" x2="$($headX + $noseOffsetX)" y2="$($headY + $noseOffsetY)" stroke="$body" stroke-width="4" stroke-linecap="round" />
</svg>
"@
}

function New-HoldFrameImage {
    param(
        [string]$GifPath,
        [string]$OutputPath,
        [ValidateRange(1, 99)]
        [int]$FramePercent,
        [switch]$Overwrite
    )

    if ((Test-Path -LiteralPath $OutputPath) -and -not $Overwrite) {
        return
    }

    $frameCountLines = @(& magick identify -format "%n`n" $GifPath)
    if ($LASTEXITCODE -ne 0 -or $frameCountLines.Count -eq 0) {
        throw "ImageMagick could not inspect hold animation $GifPath."
    }

    $frameCount = [int]$frameCountLines[0]
    if ($frameCount -lt 1) {
        throw "Hold animation $GifPath has no frames."
    }

    $frameIndex = [Math]::Min(
        $frameCount - 1,
        [Math]::Floor(($frameCount - 1) * ($FramePercent / 100.0)))
    $temporaryOutputPath = Join-Path ([IO.Path]::GetTempPath()) (
        'nomadic-method-hold-{0}-{1}.png' -f $PID, [Guid]::NewGuid().ToString('N'))
    try {
        $magickArguments = @($GifPath, '-coalesce')
        if ($frameIndex -gt 0) {
            $magickArguments += @('-delete', "0-$($frameIndex - 1)")
        }
        $magickArguments += @('-delete', '1--1', '-strip', $temporaryOutputPath)

        & magick @magickArguments
        if ($LASTEXITCODE -ne 0 -or
            -not (Test-Path -LiteralPath $temporaryOutputPath)) {
            throw "ImageMagick failed while rendering hold frame $OutputPath."
        }
        $null = Publish-GeneratedFile `
            -SourcePath $temporaryOutputPath `
            -DestinationPath $OutputPath
    }
    finally {
        Remove-Item -LiteralPath $temporaryOutputPath -Force -ErrorAction SilentlyContinue
    }
}

function New-ExerciseMp4 {
    param(
        [string]$GifPath,
        [string]$VideoPath,
        [ValidateRange(0, 99)]
        [int]$HoldFramePercent = 0,
        [switch]$Overwrite
    )

    if ((Test-Path -LiteralPath $VideoPath) -and -not $Overwrite) {
        return
    }

    $filters = [System.Collections.Generic.List[string]]::new()
    if ($HoldFramePercent -gt 0) {
        $frameCountLines = @(& magick identify -format "%n`n" $GifPath)
        if ($LASTEXITCODE -ne 0 -or $frameCountLines.Count -eq 0) {
            throw "ImageMagick could not inspect hold animation $GifPath."
        }

        $frameCount = [int]$frameCountLines[0]
        $targetFrame = [Math]::Min(
            $frameCount - 1,
            [Math]::Floor(($frameCount - 1) * ($HoldFramePercent / 100.0)))
        $filters.Add("trim=end_frame=$($targetFrame + 1)")
        $filters.Add('setpts=PTS-STARTPTS')
        # A short cloned tail makes the reviewed target pose the unambiguous last frame.
        $filters.Add('tpad=stop_mode=clone:stop_duration=0.35')
    }

    $filters.Add('fps=20')
    $filters.Add('scale=256:256:force_original_aspect_ratio=decrease:flags=lanczos')
    $filters.Add('pad=256:256:(ow-iw)/2:(oh-ih)/2:color=0xF7FAFC')
    $filters.Add('format=yuv420p')

    $temporaryVideoPath = Join-Path ([IO.Path]::GetTempPath()) (
        'nomadic-method-video-{0}-{1}.mp4' -f $PID, [Guid]::NewGuid().ToString('N'))
    $ffmpegArguments = @(
        '-hide_banner', '-loglevel', 'error', '-y',
        '-i', $GifPath,
        '-vf', ($filters -join ','),
        '-an',
        '-map_metadata', '-1',
        '-c:v', 'libx264',
        '-profile:v', 'baseline',
        '-level', '3.0',
        '-preset', 'medium',
        '-crf', '24',
        '-movflags', '+faststart',
        $temporaryVideoPath)

    try {
        & ffmpeg @ffmpegArguments
        if ($LASTEXITCODE -ne 0 -or
            -not (Test-Path -LiteralPath $temporaryVideoPath)) {
            throw "FFmpeg failed while encoding $VideoPath."
        }
        $null = Publish-GeneratedFile `
            -SourcePath $temporaryVideoPath `
            -DestinationPath $VideoPath
    }
    finally {
        Remove-Item -LiteralPath $temporaryVideoPath -Force -ErrorAction SilentlyContinue
    }
}

function New-DirectionSequenceMp4 {
    param(
        [string]$SourceVideoPath,
        [string]$OutputPath,
        [ValidateSet('HorizontalMirror', 'TemporalReverse', 'ExactExercise')]
        [string]$Transform,
        [string]$ExactSecondVideoPath = ''
    )

    # Each asset contains a complete natural loop. The base asset supplies the
    # first direction; this asset supplies only the opposite direction.
    $inputPath = $SourceVideoPath
    $filter = switch ($Transform) {
        'HorizontalMirror' { 'hflip' }
        'TemporalReverse' { 'reverse' }
        'ExactExercise' {
            if ([string]::IsNullOrWhiteSpace($ExactSecondVideoPath) -or
                -not (Test-Path -LiteralPath $ExactSecondVideoPath)) {
                throw "The exact opposite-direction source is missing for $OutputPath."
            }
            $inputPath = $ExactSecondVideoPath
            'null'
        }
    }
    & ffmpeg `
        -hide_banner `
        -loglevel error `
        -y `
        -i $inputPath `
        -vf "$filter,fps=20,format=yuv420p" `
        -an `
        -map_metadata -1 `
        -c:v libx264 `
        -profile:v baseline `
        -level 3.0 `
        -preset medium `
        -crf 24 `
        -movflags +faststart `
        $OutputPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputPath)) {
        throw "FFmpeg could not build the opposite-direction demonstration $OutputPath."
    }
}

function New-ExerciseSequenceBlocks {
    param(
        [int]$ExerciseId,
        [string]$SideSequence,
        [string]$DirectionSequence
    )

    $sideBlocks = @(switch ($SideSequence) {
        'ScreenLeftThenRight' {
            @(
                @{ SideCue = 'ScreenLeft'; MirrorMedia = $false }
                @{ SideCue = 'ScreenRight'; MirrorMedia = $true }
            )
        }
        'ScreenRightThenLeft' {
            @(
                @{ SideCue = 'ScreenRight'; MirrorMedia = $false }
                @{ SideCue = 'ScreenLeft'; MirrorMedia = $true }
            )
        }
        'ScreenLeftLeadThenRightLead' {
            @(
                @{ SideCue = 'ShownLeadStance'; MirrorMedia = $false }
                @{ SideCue = 'OppositeLeadStance'; MirrorMedia = $true }
            )
        }
        'ScreenRightLeadThenLeftLead' {
            @(
                @{ SideCue = 'ShownLeadStance'; MirrorMedia = $false }
                @{ SideCue = 'OppositeLeadStance'; MirrorMedia = $true }
            )
        }
        default {
            @(@{ SideCue = 'None'; MirrorMedia = $false })
        }
    })

    $directionCues = @(switch ($DirectionSequence) {
        'ForwardThenBackward' { @('Forward', 'Backward') }
        'BackwardThenForward' { @('Backward', 'Forward') }
        'ClockwiseThenCounterclockwise' {
            @('Clockwise', 'Counterclockwise')
        }
        'CounterclockwiseThenClockwise' {
            @('Counterclockwise', 'Clockwise')
        }
        'InwardThenOutward' { @('Inward', 'Outward') }
        'OutwardThenInward' { @('Outward', 'Inward') }
        default { @('None') }
    })

    $blocks = @()
    foreach ($sideBlock in $sideBlocks) {
        for ($directionIndex = 0;
             $directionIndex -lt $directionCues.Count;
             $directionIndex++) {
            $blocks += [ordered]@{
                exerciseId = $ExerciseId
                sideCue = [string]$sideBlock.SideCue
                directionCue = [string]$directionCues[$directionIndex]
                mirrorMedia = [bool]$sideBlock.MirrorMedia
                mediaSegment = if ($directionCues.Count -eq 1) {
                    'Full'
                }
                elseif ($directionIndex -eq 0) {
                    'FirstDirection'
                }
                else {
                    'SecondDirection'
                }
            }
        }
    }

    return @($blocks)
}

$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$gifOutputRoot = Join-Path $resolvedOutputRoot 'exercise_gifs'
$videoOutputRoot = Join-Path $resolvedOutputRoot 'exercise_videos'
$directionVideoOutputRoot = Join-Path $resolvedOutputRoot 'exercise_direction_videos'
$holdFrameOutputRoot = Join-Path $resolvedOutputRoot 'exercise_hold_frames'
$catalogPath = Join-Path $resolvedOutputRoot 'exercises.json'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("NomadicMethodExerciseFrames-" + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $gifOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $videoOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $directionVideoOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $holdFrameOutputRoot | Out-Null
New-Item -ItemType Directory -Path $tempRoot | Out-Null

$records = [System.Collections.Generic.List[object]]::new($expectedExerciseCount)

foreach ($definition in $sourceDefinitions) {
    $exerciseId = [int]$definition.Id
    if ($exerciseId -notin $retainedExerciseIds) { continue }
    $region = [string]$definition.Region
    $sourceExerciseName = [string]$definition.Name
    $baselineExerciseName = if ($bilateralExerciseNames.ContainsKey($exerciseId)) {
        [string]$bilateralExerciseNames[$exerciseId]
    }
    else {
        $sourceExerciseName
    }
    $replacement = if ($catalogExerciseReplacements.ContainsKey($exerciseId)) {
        $catalogExerciseReplacements[$exerciseId]
    }
    else {
        $null
    }
    if ($null -ne $replacement) {
        $baselineSideSequence = if (
            $baselineExerciseSideSequences.ContainsKey($exerciseId)) {
            [string]$baselineExerciseSideSequences[$exerciseId]
        }
        elseif ($exerciseId -in $baselineReviewedContinuousExerciseIds -or
            $exerciseDirectionSequences.ContainsKey($exerciseId) -or
            $exerciseId -in $retiredDirectionOnlyExerciseIds) {
            'Continuous'
        }
        else {
            throw "Exercise $exerciseId has no baseline side-sequence decision."
        }
        $retiredBaselineName = $baselineExerciseName
        if ($baselineSideSequence -ne 'Continuous' -and
            $retiredBaselineName.StartsWith(
                'Alternating ',
                [StringComparison]::Ordinal)) {
            $retiredBaselineName = $retiredBaselineName.Substring(
                'Alternating '.Length)
        }
        if ([string]$replacement.RetiredName -ne $retiredBaselineName) {
            throw "Replacement $exerciseId expects retired exercise '$($replacement.RetiredName)' but the baseline name is '$retiredBaselineName'."
        }
    }
    $exerciseSideSequence = if ($exerciseSideSequences.ContainsKey($exerciseId)) {
        [string]$exerciseSideSequences[$exerciseId]
    }
    elseif ($exerciseId -in $alternatingExerciseIds) {
        'Alternating'
    }
    elseif ($exerciseId -in $reviewedContinuousExerciseIds -or
        $exerciseDirectionSequences.ContainsKey($exerciseId)) {
        'Continuous'
    }
    else {
        throw "Exercise $exerciseId is missing a reviewed side-sequence decision."
    }
    $exerciseDirectionSequence = if (
        $exerciseDirectionSequences.ContainsKey($exerciseId)) {
        [string]$exerciseDirectionSequences[$exerciseId]
    }
    else {
        'None'
    }
    $exerciseName = if ($null -ne $replacement) {
        [string]$replacement.Name
    }
    else {
        $baselineExerciseName
    }
    if ($exerciseSideSequence -in @(
            'ScreenLeftThenRight', 'ScreenRightThenLeft') -and
        $exerciseName.StartsWith(
            'Alternating ',
            [StringComparison]::Ordinal)) {
        $exerciseName = $exerciseName.Substring('Alternating '.Length)
    }
    $effectiveRegion = if ($exerciseRegionOverrides.ContainsKey($exerciseId)) {
        [string]$exerciseRegionOverrides[$exerciseId]
    }
    else {
        $region
    }
    $practice = if ($null -ne $replacement) {
        [string]$replacement.Practice
    }
    elseif ($definition.Additional) {
        [string]$definition.Practice
    }
    elseif ($exercisePracticeOverrides.ContainsKey($exerciseId)) {
        [string]$exercisePracticeOverrides[$exerciseId]
    }
    else {
        Get-Practice -Name $exerciseName
    }
    $motionProfile = if ($null -ne $replacement) {
        [string]$replacement.MotionProfile
    }
    elseif ($definition.Additional) {
        [string]$definition.MotionProfile
    }
    else {
        Get-MotionProfile `
            -Region $effectiveRegion `
            -Name $exerciseName
    }
    $canonicalAssignment = $exerciseCanonicalGroups[$exerciseId]
    $primaryCanonicalGroup = [string]$canonicalAssignment.Primary
    $secondaryCanonicalGroups = @(
        $canonicalAssignment.Secondary | ForEach-Object { [string]$_ })
    $isHold = $holdExerciseFrames.ContainsKey($exerciseId)
    $exerciseMode = if ($isHold) { 'Hold' } else { 'Repetition' }
    $exercisePresentation = if (
        $stillExercisePresentations.ContainsKey($exerciseId)) {
        'Still'
    }
    else {
        'Motion'
    }
    $holdFramePercent = if ($isHold) {
        [int]$holdExerciseFrames[$exerciseId]
    }
    else {
        0
    }
    $gifFileName = 'exercise_{0:D4}.gif' -f $exerciseId
    $videoFileName = 'exercise_{0:D4}.mp4' -f $exerciseId
    $videoRelativePath = "exercise_videos/$videoFileName"

    $record = [ordered]@{
        id = $exerciseId
        name = $exerciseName
        retiredName = if ($null -ne $replacement) {
            [string]$replacement.RetiredName
        }
        else {
            $null
        }
        video = $videoRelativePath
        primaryCanonicalGroup = $primaryCanonicalGroup
        secondaryCanonicalGroups = $secondaryCanonicalGroups
        practice = $practice
        motionProfile = $motionProfile
        mode = $exerciseMode
        presentation = $exercisePresentation
        holdFramePercent = $holdFramePercent
        sideSequence = $exerciseSideSequence
        directionSequence = $exerciseDirectionSequence
        sequenceBlocks = @()
        insectCompatibility = if (
            $exerciseId -in $insectCompatibleExerciseIds) {
            'Compatible'
        }
        else {
            'Incompatible'
        }
        hardFloorCompatibility = if (
            $exerciseId -in $hardFloorCompatibleExerciseIds) {
            'Compatible'
        }
        else {
            'Incompatible'
        }
        upperBodyClothingRequirement = if (
            $exerciseId -in $upperBodyClothingRequiredExerciseIds) {
            'ClothingRequired'
        }
        elseif ($exerciseId -in $bareUpperBodyRequiredExerciseIds) {
            'BareUpperBodyRequired'
        }
        else {
            'Agnostic'
        }
        shyCompatibility = if (
            $exerciseId -in $shyCompatibleExerciseIds) {
            'Compatible'
        }
        else {
            'Incompatible'
        }
        wallRequired = $exerciseId -in $wallRequiredExerciseIds
        soleWallContactRequired =
            $exerciseId -in $soleWallContactRequiredExerciseIds
        mirrorRelationship = if (
            $exerciseId -in $mirrorOnlyExerciseIds) {
            'MirrorOnly'
        }
        elseif ($exerciseId -in $mirrorBenefitsGreatlyExerciseIds) {
            'BenefitsGreatly'
        }
        else {
            'Agnostic'
        }
        minimumMirrorCoverage = if (
            $mirrorCoverageByExerciseId.ContainsKey($exerciseId)) {
            [string]$mirrorCoverageByExerciseId[$exerciseId]
        }
        else {
            'None'
        }
        score = 0
        muscularDemand = [int]$muscularDemandByExerciseId[$exerciseId]
        onlyFeetTouchGround = $true
        shoeAgnostic = $true
        maxSpaceMeters = 2
        equipment = if ($exerciseId -in $mirrorOnlyExerciseIds) {
            'Mirror'
        }
        else {
            'None'
        }
        silent = $exerciseId -in $silentExerciseIds
    }
    if ($sessionMovementByExerciseId.ContainsKey($exerciseId)) {
        $record['sessionMovementId'] =
            [int]$sessionMovementByExerciseId[$exerciseId]
    }
    $records.Add($record)

    $isSelected = if ($ExerciseIds.Count -gt 0) {
        $ExerciseIds -contains $exerciseId
    }
    else {
        $exerciseId -ge $StartExercise -and
            ($MaxExercises -eq 0 -or $exerciseId -le $MaxExercises)
    }

    if (-not $isSelected) {
        continue
    }

    $gifPath = Join-Path $gifOutputRoot $gifFileName
    $videoPath = Join-Path $videoOutputRoot $videoFileName
    $holdFramePath = Join-Path $holdFrameOutputRoot (
        'exercise_{0:D4}.png' -f $exerciseId)

    if ($exactExerciseMediaTransforms.ContainsKey($exerciseId)) {
        $transform = $exactExerciseMediaTransforms[$exerciseId]
        $sourceExerciseId = [int]$transform.Source
        $sourceGifPath = Join-Path $gifOutputRoot (
            'exercise_{0:D4}.gif' -f $sourceExerciseId)
        if (-not (Test-Path -LiteralPath $sourceGifPath)) {
            throw "Exact transform source GIF $sourceExerciseId is missing for $exerciseName."
        }

        $transformedGifPath = Join-Path $tempRoot (
            'transformed_{0:D4}.gif' -f $exerciseId)
        New-ExactExerciseGif `
            -SourceGifPath $sourceGifPath `
            -OutputPath $transformedGifPath `
            -Transform $transform `
            -Comment "Nomadic Method reviewed transformed exercise $exerciseId - $exerciseName"

        $transformChanged = $Force -or -not (Test-Path -LiteralPath $gifPath)
        if (-not $transformChanged) {
            $newHash = (Get-FileHash -LiteralPath $transformedGifPath -Algorithm SHA256).Hash
            $oldHash = (Get-FileHash -LiteralPath $gifPath -Algorithm SHA256).Hash
            $transformChanged = $newHash -ne $oldHash
        }
        if ($transformChanged) {
            $transformChanged = Publish-GeneratedFile `
                -SourcePath $transformedGifPath `
                -DestinationPath $gifPath
        }

        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent `
                -Overwrite:($Force -or $transformChanged)
        }
        New-ExerciseMp4 `
            -GifPath $gifPath `
            -VideoPath $videoPath `
            -HoldFramePercent $holdFramePercent `
            -Overwrite:($Force -or $transformChanged)
        continue
    }

    if ($exactExerciseMediaCopies.ContainsKey($exerciseId)) {
        $sourceExerciseId = [int]$exactExerciseMediaCopies[$exerciseId]
        $sourceGifPath = Join-Path $gifOutputRoot (
            'exercise_{0:D4}.gif' -f $sourceExerciseId)
        if (-not (Test-Path -LiteralPath $sourceGifPath)) {
            throw "Exact source GIF $sourceExerciseId is missing for $exerciseName."
        }

        $copyRequired = -not (Test-Path -LiteralPath $gifPath)
        if (-not $copyRequired) {
            $sourceHash = (Get-FileHash -LiteralPath $sourceGifPath -Algorithm SHA256).Hash
            $targetHash = (Get-FileHash -LiteralPath $gifPath -Algorithm SHA256).Hash
            $copyRequired = $sourceHash -ne $targetHash
        }

        if ($copyRequired) {
            $copyRequired = Publish-GeneratedFile `
                -SourcePath $sourceGifPath `
                -DestinationPath $gifPath
        }

        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent `
                -Overwrite:($Force -or $copyRequired)
        }
        New-ExerciseMp4 `
            -GifPath $gifPath `
            -VideoPath $videoPath `
            -HoldFramePercent $holdFramePercent `
            -Overwrite:($Force -or $copyRequired)
        continue
    }

    if ((Test-Path -LiteralPath $gifPath) -and -not $Force) {
        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent
        }
        New-ExerciseMp4 `
            -GifPath $gifPath `
            -VideoPath $videoPath `
            -HoldFramePercent $holdFramePercent
        continue
    }

    if ($exerciseId -in $reviewedPosecodeIds) {
        if (-not (Test-Path -LiteralPath $gifPath)) {
            throw "The reviewed Posecode asset is missing for $exerciseName."
        }

        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent `
                -Overwrite:$Force
        }
        New-ExerciseMp4 `
            -GifPath $gifPath `
            -VideoPath $videoPath `
            -HoldFramePercent $holdFramePercent `
            -Overwrite:$Force
        continue
    }

    if ($exerciseId -in $reviewedExternalIds -or
        $exerciseId -in $reviewedInternalAnatomyIds) {
        New-ExternalExerciseGif `
            -ExerciseId $exerciseId `
            -ExerciseName $exerciseName `
            -SideSequence $exerciseSideSequence `
            -Media $externalExerciseMedia[$exerciseId] `
            -GifPath $gifPath `
            -WorkingRoot $tempRoot
        if ($isHold) {
            New-HoldFrameImage `
                -GifPath $gifPath `
                -OutputPath $holdFramePath `
                -FramePercent $holdFramePercent `
                -Overwrite:$Force
        }
        New-ExerciseMp4 `
            -GifPath $gifPath `
            -VideoPath $videoPath `
            -HoldFramePercent $holdFramePercent `
            -Overwrite:$Force
        continue
    }

    throw "No reviewed demonstration is assigned to $exerciseName."
}

$recordsById = @{}
foreach ($record in $records) {
    $recordsById[[int]$record.id] = $record
}
foreach ($exerciseId in $retainedExerciseIds) {
    if ($sequenceRootByExerciseId.ContainsKey($exerciseId) -and
        [int]$sequenceRootByExerciseId[$exerciseId] -ne $exerciseId) {
        continue
    }

    $memberIds = if ($sequenceMembersByRootId.ContainsKey($exerciseId)) {
        @($sequenceMembersByRootId[$exerciseId])
    }
    else {
        @($exerciseId)
    }
    $sequenceBlocks = @()
    foreach ($memberId in $memberIds) {
        $member = $recordsById[$memberId]
        $sequenceBlocks += @(New-ExerciseSequenceBlocks `
            -ExerciseId $memberId `
            -SideSequence ([string]$member.sideSequence) `
            -DirectionSequence ([string]$member.directionSequence))
    }
    $recordsById[$exerciseId].sequenceBlocks = @($sequenceBlocks)
}

foreach ($entry in $exerciseDirectionSequences.GetEnumerator()) {
    $exerciseId = [int]$entry.Key
    $sourceVideoPath = Join-Path $videoOutputRoot (
        'exercise_{0:D4}.mp4' -f $exerciseId)
    $directionVideoPath = Join-Path $directionVideoOutputRoot (
        'exercise_{0:D4}.mp4' -f $exerciseId)
    if (-not (Test-Path -LiteralPath $sourceVideoPath)) {
        throw "Direction-sequence source video $exerciseId is missing."
    }
    $transform = $exerciseDirectionMediaTransforms[$exerciseId]
    $exactSecondVideoPath = if (
        [string]$transform.Mode -eq 'ExactExercise') {
        Join-Path $videoOutputRoot (
            'exercise_{0:D4}.mp4' -f [int]$transform.SecondExerciseId)
    }
    else {
        ''
    }
    # A selected media batch cannot change an unrelated direction sequence.
    # Rebuild when either source is selected, or when its output is missing.
    if ($ExerciseIds.Count -gt 0 -and
        $exerciseId -notin $ExerciseIds -and
        ([string]$transform.Mode -ne 'ExactExercise' -or
            [int]$transform.SecondExerciseId -notin $ExerciseIds) -and
        (Test-Path -LiteralPath $directionVideoPath)) {
        continue
    }
    New-DirectionSequenceMp4 `
        -SourceVideoPath $sourceVideoPath `
        -OutputPath $directionVideoPath `
        -Transform ([string]$transform.Mode) `
        -ExactSecondVideoPath $exactSecondVideoPath
}

if ($records.Count -ne $expectedExerciseCount) {
    throw "Expected $expectedExerciseCount exercise records but generated $($records.Count)."
}

$duplicateNames = $records | Group-Object { $_['name'] } | Where-Object Count -ne 1
$duplicateVideos = $records | Group-Object { $_['video'] } | Where-Object Count -ne 1
$duplicateIds = $records | Group-Object { $_['id'] } | Where-Object Count -ne 1
$constraintViolations = $records | Where-Object {
    -not $_['onlyFeetTouchGround'] -or
    -not $_['shoeAgnostic'] -or
    $_['maxSpaceMeters'] -le 0 -or
    $_['maxSpaceMeters'] -gt 2 -or
    ($_['mirrorRelationship'] -eq 'MirrorOnly' -and
        $_['equipment'] -ne 'Mirror') -or
    ($_['mirrorRelationship'] -ne 'MirrorOnly' -and
        $_['equipment'] -ne 'None') -or
    $_['mirrorRelationship'] -notin @(
        'MirrorOnly', 'BenefitsGreatly', 'Agnostic') -or
    ($_['mirrorRelationship'] -in @('MirrorOnly', 'BenefitsGreatly') -and
        $_['minimumMirrorCoverage'] -notin @('UpperBody', 'FullBody')) -or
    ($_['mirrorRelationship'] -eq 'Agnostic' -and
        $_['minimumMirrorCoverage'] -ne 'None') -or
    $_['hardFloorCompatibility'] -notin @(
        'Compatible', 'Incompatible') -or
    $_['upperBodyClothingRequirement'] -notin @(
        'ClothingRequired', 'BareUpperBodyRequired', 'Agnostic') -or
    $_['shyCompatibility'] -notin @(
        'Compatible', 'Incompatible') -or
    -not ($_['wallRequired'] -is [bool]) -or
    -not ($_['soleWallContactRequired'] -is [bool]) -or
    ([bool]$_['soleWallContactRequired'] -and
        -not [bool]$_['wallRequired']) -or
    -not ($_['silent'] -is [bool]) -or
    [string]::IsNullOrWhiteSpace($_['primaryCanonicalGroup']) -or
    $_['primaryCanonicalGroup'] -notin $canonicalGroupKeys -or
    @($_['secondaryCanonicalGroups'] | Sort-Object -Unique).Count -ne
        @($_['secondaryCanonicalGroups']).Count -or
    $_['primaryCanonicalGroup'] -in @($_['secondaryCanonicalGroups']) -or
    @($_['secondaryCanonicalGroups'] | Where-Object {
            $_ -notin $canonicalGroupKeys
        }).Count -gt 0 -or
    [string]::IsNullOrWhiteSpace($_['practice']) -or
    [string]::IsNullOrWhiteSpace($_['motionProfile']) -or
    $_['mode'] -notin @('Repetition', 'Hold') -or
    $_['sideSequence'] -notin @(
        'Continuous',
        'Alternating',
        'ScreenLeftThenRight',
        'ScreenRightThenLeft',
        'ScreenLeftLeadThenRightLead',
        'ScreenRightLeadThenLeftLead') -or
    $_['directionSequence'] -notin @(
        'None',
        'ForwardThenBackward',
        'BackwardThenForward',
        'ClockwiseThenCounterclockwise',
        'CounterclockwiseThenClockwise',
        'InwardThenOutward',
        'OutwardThenInward') -or
    ($null -ne $_['sessionMovementId'] -and (
        $_['sessionMovementId'] -isnot [int] -or
        $_['sessionMovementId'] -lt 0)) -or
    ($_['directionSequence'] -ne 'None' -and (
        $_['mode'] -ne 'Repetition' -or $_['presentation'] -ne 'Motion')) -or
    ($_['mode'] -eq 'Repetition' -and $_['holdFramePercent'] -ne 0) -or
    ($_['mode'] -eq 'Hold' -and (
        $_['holdFramePercent'] -lt 1 -or $_['holdFramePercent'] -gt 99)) -or
    ($_['presentation'] -eq 'Still' -and $_['mode'] -ne 'Hold') -or
    $_['presentation'] -notin @('Motion', 'Still') -or
    $_['score'] -ne 0 -or
    $_['muscularDemand'] -isnot [int] -or
    $_['muscularDemand'] -lt 0 -or
    $_['muscularDemand'] -gt 2
}
$recordsById = @{}
foreach ($record in $records) {
    $recordsById[[int]$record['id']] = $record
}
$validSequenceSideCues = @(
    'None', 'ScreenLeft', 'ScreenRight', 'ShownLeadStance',
    'OppositeLeadStance')
$validSequenceDirectionCues = @(
    'None', 'Forward', 'Backward', 'Clockwise',
    'Counterclockwise', 'Inward', 'Outward')
$validSequenceMediaSegments = @(
    'Full', 'FirstDirection', 'SecondDirection')
$sequenceOwnersByExerciseId = @{}
$sequenceViolations = [System.Collections.Generic.List[int]]::new()
foreach ($root in $records | Where-Object { @($_['sequenceBlocks']).Count -gt 0 }) {
    $rootId = [int]$root['id']
    $blocks = @($root['sequenceBlocks'])
    if ([int]$blocks[0].exerciseId -ne $rootId) {
        $sequenceViolations.Add($rootId)
    }
    foreach ($block in $blocks) {
        $memberId = [int]$block.exerciseId
        if (-not $recordsById.ContainsKey($memberId) -or
            [string]$block.sideCue -notin $validSequenceSideCues -or
            [string]$block.directionCue -notin $validSequenceDirectionCues -or
            $block.mirrorMedia -isnot [bool] -or
            [string]$block.mediaSegment -notin $validSequenceMediaSegments) {
            $sequenceViolations.Add($rootId)
            continue
        }
        if (-not $sequenceOwnersByExerciseId.ContainsKey($memberId)) {
            $sequenceOwnersByExerciseId[$memberId] = $rootId
        }
        elseif ([int]$sequenceOwnersByExerciseId[$memberId] -ne $rootId) {
            $sequenceViolations.Add($rootId)
        }
    }

    $members = @($blocks | ForEach-Object { [int]$_.exerciseId } |
        Sort-Object -Unique)
    if (@($members | ForEach-Object {
                [string]$recordsById[$_]['mode']
            } | Sort-Object -Unique).Count -ne 1) {
        $sequenceViolations.Add($rootId)
    }
    foreach ($memberId in $members) {
        $member = $recordsById[$memberId]
        if ($memberId -ne $rootId -and @($member['sequenceBlocks']).Count -ne 0) {
            $sequenceViolations.Add($rootId)
        }
        if ([bool]$member['wallRequired'] -ne [bool]$root['wallRequired']) {
            $sequenceViolations.Add($rootId)
        }
        if ([bool]$member['soleWallContactRequired'] -ne
                [bool]$root['soleWallContactRequired']) {
            $sequenceViolations.Add($rootId)
        }
        if ([string]$member['upperBodyClothingRequirement'] -ne
                [string]$root['upperBodyClothingRequirement']) {
            $sequenceViolations.Add($rootId)
        }
        if ([string]$member['shyCompatibility'] -ne
                [string]$root['shyCompatibility']) {
            $sequenceViolations.Add($rootId)
        }
    }
}
if (@(Compare-Object `
        @($retainedExerciseIds | Sort-Object) `
        @($sequenceOwnersByExerciseId.Keys | Sort-Object)).Count -gt 0) {
    $sequenceViolations.Add(0)
}
$sessionMovementViolations = @($records | Where-Object {
    $movementId = [int]$_['sessionMovementId']
    if ($movementId -eq 0) {
        return $false
    }
    if (-not $recordsById.ContainsKey($movementId)) {
        return $true
    }
    $root = $recordsById[$movementId]
    $family = @($records | Where-Object {
            [int]$_['sessionMovementId'] -eq $movementId })
    return $family.Count -lt 2 -or
        [int]$root['sessionMovementId'] -ne [int]$root['id'] -or
        @($family | Where-Object {
                $rootGroups = @(
                    [string]$root['primaryCanonicalGroup']
                    @($root['secondaryCanonicalGroups']))
                $memberGroups = @(
                    [string]$_['primaryCanonicalGroup']
                    @($_['secondaryCanonicalGroups']))
                @($memberGroups | Where-Object {
                        $_ -in $rootGroups }).Count -eq 0
            }).Count -gt 0
})
$syntheticNames = $records | Where-Object {
    $_['name'] -match ' — ' -or
    $_['name'] -match 'Slow Tempo|Four-Count Tempo|End-Range Pause|Half Range|Full Range|Left Lead|Right Lead|Precision Repetitions|Continuous Flow'
}

if ($duplicateNames -or $duplicateVideos -or $duplicateIds -or
    $constraintViolations -or
    $sequenceViolations.Count -gt 0 -or
    $sessionMovementViolations -or
    $syntheticNames) {
    $failureDetails = @(
        if ($duplicateNames) {
            'duplicate names: ' + (@($duplicateNames.Name) -join ', ')
        }
        if ($duplicateVideos) {
            'duplicate videos: ' + (@($duplicateVideos.Name) -join ', ')
        }
        if ($duplicateIds) {
            'duplicate IDs: ' + (@($duplicateIds.Name) -join ', ')
        }
        if ($constraintViolations) {
            'constraint IDs: ' + (@($constraintViolations.id) -join ', ')
        }
        if ($sequenceViolations.Count -gt 0) {
            'sequence roots: ' +
                (@($sequenceViolations | Sort-Object -Unique) -join ', ')
        }
        if ($sessionMovementViolations) {
            'session-movement IDs: ' +
                (@($sessionMovementViolations.id) -join ', ')
        }
        if ($syntheticNames) {
            'synthetic names: ' + (@($syntheticNames.name) -join ', ')
        }
    ) -join '; '
    throw "The generated catalog failed validation: $failureDetails."
}

if ($MaxExercises -eq 0 -and $ExerciseIds.Count -eq 0) {
    $mediaDirectories = @(
        @{
            Path = $gifOutputRoot
            Extension = 'gif'
            ExpectedIds = $retainedExerciseIds
        },
        @{
            Path = $videoOutputRoot
            Extension = 'mp4'
            ExpectedIds = $retainedExerciseIds
        },
        @{
            Path = $holdFrameOutputRoot
            Extension = 'png'
            ExpectedIds = @(
                $holdExerciseFrames.Keys |
                    ForEach-Object { [int]$_ } |
                    Where-Object { $_ -in $retainedExerciseIds })
        },
        @{
            Path = $directionVideoOutputRoot
            Extension = 'mp4'
            ExpectedIds = @(
                $exerciseDirectionSequences.Keys |
                    ForEach-Object { [int]$_ })
        })
    foreach ($mediaDirectory in $mediaDirectories) {
        $resolvedMediaDirectory = [IO.Path]::GetFullPath([string]$mediaDirectory.Path)
        if (-not $resolvedMediaDirectory.StartsWith(
                $resolvedOutputRoot + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Refusing to prune media outside the configured output root.'
        }

        Get-ChildItem -LiteralPath $resolvedMediaDirectory -File |
            Where-Object {
                $_.Name -match ('^exercise_(?<id>\d{4,})\.' +
                    [regex]::Escape([string]$mediaDirectory.Extension) + '$') -and
                [int]$Matches.id -notin @($mediaDirectory.ExpectedIds)
            } |
            ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }

    $missingVideos = $records | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $resolvedOutputRoot $_['video']))
    }

    if ($missingVideos) {
        throw 'At least one catalog record is missing its MP4 asset.'
    }

    $missingHoldFrames = $records | Where-Object {
        $_['mode'] -eq 'Hold' -and
        -not (Test-Path -LiteralPath (Join-Path $holdFrameOutputRoot (
                    'exercise_{0:D4}.png' -f $_['id'])))
    }

    if ($missingHoldFrames) {
        throw 'At least one hold record is missing its static countdown frame.'
    }

    $missingDirectionVideos = $records | Where-Object {
        $_['directionSequence'] -ne 'None' -and
        -not (Test-Path -LiteralPath (Join-Path $directionVideoOutputRoot (
                    'exercise_{0:D4}.mp4' -f $_['id'])))
    }
    if ($missingDirectionVideos) {
        throw 'At least one directional record is missing its two-direction MP4 asset.'
    }
}

$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $catalogPath -Encoding utf8

$resolvedTempRoot = [IO.Path]::GetFullPath($tempRoot)
$systemTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $resolvedTempRoot.StartsWith(
        $systemTempRoot,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to remove a generator working directory outside the system temp folder.'
}
Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force

Write-Output "Catalog: $catalogPath"
Write-Output "Records: $($records.Count)"
Write-Output "MP4 directory: $videoOutputRoot"
Write-Output "Direction MP4 directory: $directionVideoOutputRoot"
