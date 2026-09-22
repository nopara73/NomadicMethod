param(
    [string]$ReviewPath = (
        Join-Path $PSScriptRoot 'ExerciseMirrorRelationships.psd1'),
    [string]$CatalogPath = (
        [System.IO.Path]::Combine(
            $PSScriptRoot,
            '..',
            'NomadicMethod',
            'Assets',
            'exercises.json'))
)

$ErrorActionPreference = 'Stop'

$requiredBenefitsGreatlyCriteria = @(
    'TechnicalMartialArts'
    'DanceAndAlignmentSensitivePoses'
    'ComplexSingleLegAlignment'
    'LivePlaneOrSymmetryCorrection'
    'GazeStabilityFeedback'
    'SubtlePelvicPositionFeedback'
)
$requiredCoverageCategories = @('UpperBody', 'FullBody')

$review = Import-PowerShellDataFile -LiteralPath $ReviewPath -SkipLimitCheck
$benefitsGreatlyByCriterion = $review.BenefitsGreatlyByCriterion
if ($benefitsGreatlyByCriterion -isnot [System.Collections.IDictionary] -or
    @(Compare-Object `
            $requiredBenefitsGreatlyCriteria `
            @($benefitsGreatlyByCriterion.Keys)).Count -gt 0) {
    throw 'BenefitsGreatly exercises must use exactly the approved narrow audit criteria.'
}

$benefitsGreatlyIds = @(
    foreach ($criterion in $requiredBenefitsGreatlyCriteria) {
        $criterionIds = @(
            $benefitsGreatlyByCriterion[$criterion] |
                ForEach-Object { [int]$_ })
        $criterionIds
    })

$mirrorOnlyByCoverage = $review.MirrorOnlyByCoverage
$benefitsGreatlyByCoverage = $review.BenefitsGreatlyByCoverage
foreach ($coverageReview in @(
        $mirrorOnlyByCoverage,
        $benefitsGreatlyByCoverage)) {
    if ($coverageReview -isnot [System.Collections.IDictionary] -or
        @(Compare-Object `
                $requiredCoverageCategories `
                @($coverageReview.Keys)).Count -gt 0) {
        throw 'Mirror coverage reviews must use exactly UpperBody and FullBody.'
    }
}

$mirrorOnlyIds = @(
    foreach ($coverage in $requiredCoverageCategories) {
        $coverageIds = @(
            $mirrorOnlyByCoverage[$coverage] |
                ForEach-Object { [int]$_ })
        $coverageIds
    })
$benefitsGreatlyCoverageIds = @(
    foreach ($coverage in $requiredCoverageCategories) {
        $coverageIds = @(
            $benefitsGreatlyByCoverage[$coverage] |
                ForEach-Object { [int]$_ })
        $coverageIds
    })
if (@(Compare-Object `
        @($benefitsGreatlyIds | Sort-Object) `
        @($benefitsGreatlyCoverageIds | Sort-Object)).Count -gt 0) {
    throw 'BenefitsGreatly coverage must exactly partition the criterion audit.'
}

$reviewedIdsByRelationship = [ordered]@{
    MirrorOnly = $mirrorOnlyIds
    BenefitsGreatly = $benefitsGreatlyIds
    Agnostic = @($review.Agnostic | ForEach-Object { [int]$_ })
}
$allReviewedIds = @(
    $reviewedIdsByRelationship.Values |
        ForEach-Object { $_ })
if ($allReviewedIds.Count -ne @($allReviewedIds | Sort-Object -Unique).Count) {
    throw 'Every exercise must appear exactly once in the mirror audit.'
}

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$catalogIds = @($catalog | ForEach-Object { [int]$_.id })
if (@(Compare-Object `
        @($allReviewedIds | Sort-Object) `
        @($catalogIds | Sort-Object)).Count -gt 0) {
    throw 'The mirror audit and packaged exercise catalog do not cover the same IDs.'
}

foreach ($relationship in $reviewedIdsByRelationship.Keys) {
    $reviewedRelationshipIds = @(
        $reviewedIdsByRelationship[$relationship] | Sort-Object)
    $catalogRelationshipIds = @(
        $catalog |
            Where-Object { $_.mirrorRelationship -eq $relationship } |
            ForEach-Object { [int]$_.id } |
            Sort-Object)
    if (@(Compare-Object `
            $reviewedRelationshipIds `
            $catalogRelationshipIds).Count -gt 0) {
        throw "The '$relationship' review does not match the packaged catalog."
    }
}

foreach ($relationshipReview in @(
        [pscustomobject]@{
            Relationship = 'MirrorOnly'
            ByCoverage = $mirrorOnlyByCoverage
        },
        [pscustomobject]@{
            Relationship = 'BenefitsGreatly'
            ByCoverage = $benefitsGreatlyByCoverage
        })) {
    foreach ($coverage in $requiredCoverageCategories) {
        $reviewedCoverageIds = @(
            $relationshipReview.ByCoverage[$coverage] |
                ForEach-Object { [int]$_ } |
                Sort-Object)
        $catalogCoverageIds = @(
            $catalog |
                Where-Object {
                    $_.mirrorRelationship -eq $relationshipReview.Relationship -and
                    $_.minimumMirrorCoverage -eq $coverage
                } |
                ForEach-Object { [int]$_.id } |
                Sort-Object)
        if (@(Compare-Object `
                $reviewedCoverageIds `
                $catalogCoverageIds).Count -gt 0) {
            throw "$($relationshipReview.Relationship) + $coverage does not match the packaged catalog."
        }
    }
}

$agnosticWithCoverage = @(
    $catalog | Where-Object {
        $_.mirrorRelationship -eq 'Agnostic' -and
        $_.minimumMirrorCoverage -ne 'None'
    })
if ($agnosticWithCoverage.Count -gt 0) {
    throw 'Agnostic exercises must not declare mirror coverage.'
}

$invalidEquipment = @(
    $catalog | Where-Object {
        ($_.mirrorRelationship -eq 'MirrorOnly' -and $_.equipment -ne 'Mirror') -or
        ($_.mirrorRelationship -ne 'MirrorOnly' -and $_.equipment -ne 'None')
    })
if ($invalidEquipment.Count -gt 0) {
    throw 'Mirror equipment and relationship metadata contradict each other.'
}

Write-Output (
    ('Mirror audit valid: MirrorOnly {0} upper-body + {1} full-body; ' +
    'BenefitsGreatly {2} upper-body + {3} full-body; {4} Agnostic.') -f
        @($mirrorOnlyByCoverage.UpperBody).Count,
        @($mirrorOnlyByCoverage.FullBody).Count,
        @($benefitsGreatlyByCoverage.UpperBody).Count,
        @($benefitsGreatlyByCoverage.FullBody).Count,
        $reviewedIdsByRelationship.Agnostic.Count)
