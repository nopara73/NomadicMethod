param([ValidateSet('precise-admissions-2026-10-23', 'ability-admissions-2026-10-23', 'zopd-admissions-2026-10-24')]
    [string]$Admission = 'precise-admissions-2026-10-23')

function Assert-PreciseAdmissionFitnessRatings {
    param([int]$Id, [hashtable]$Entry, [hashtable]$Profile,
        [object[]]$Dimensions, [hashtable]$Ratings)
    for ($axis = 0; $axis -lt $Dimensions.Count; $axis++) {
        $dimension = $Dimensions[$axis]
        $trainedLevels = $Entry.trainedLevels[$dimension]
        if ($null -eq $trainedLevels -or $trainedLevels.Count -gt 1 -or
            ($trainedLevels.Count -eq 1 -and
                (($trainedLevels[0] -isnot [int] -and $trainedLevels[0] -isnot [long]) -or
                    $trainedLevels[0] -lt 1 -or $trainedLevels[0] -gt 5))) {
            throw "Exercise $Id requires its exact reviewed $dimension training claim."
        }
        $trained = $trainedLevels.Count -eq 1
        # Historical trainedLevels records describe training claims. Their empty
        # claims now require an explicit minimum rating of [1] and false flags;
        # neither the source nor the runtime rating may be empty or defaulted.
        [object[]]$expectedLevels = if ($trained) { $trainedLevels } else { @(1) }
        if ($Profile.trainingCharacteristics[$dimension] -isnot [bool] -or
            $Profile.trainingCharacteristics[$dimension] -cne $trained -or
            $Ratings.trainingCharacteristics[[string]$Id][$axis] -isnot [bool] -or
            $Ratings.trainingCharacteristics[[string]$Id][$axis] -cne $trained -or
            (ConvertTo-Json -InputObject $expectedLevels -Compress) -cne
                (ConvertTo-Json -InputObject $Profile.levels[$dimension] -Compress) -or
            (ConvertTo-Json -InputObject $expectedLevels -Compress) -cne
                (ConvertTo-Json -InputObject $Ratings.ratings[[string]$Id][$axis] -Compress)) {
            throw "Exercise $Id has a range or a changed $dimension assessment; review the admission explicitly."
        }
    }
}

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'NomadicMethod/Assets'
$catalog = @(Get-Content (Join-Path $assets 'exercises.json') -Raw | ConvertFrom-Json)
$audit = Get-Content (Join-Path $root "docs/catalog-audit/$Admission.json") -Raw | ConvertFrom-Json -AsHashtable
$expectedCount = if ($Admission -eq 'ability-admissions-2026-10-23') { 1 } elseif ($Admission -eq 'zopd-admissions-2026-10-24') { 2 } else { 16 }
$reviews = Read-CatalogReviewEvidence -Path (Join-Path $root $audit.reviewEvidence)
$anatomyReviews = Read-CatalogReviewEvidence -Path (Join-Path $root 'docs/catalog-audit/anatomical-region-review-2026-10-26.json')
$assessment = Get-Content (Join-Path $PSScriptRoot 'ExerciseFitnessCeilingScores.json') -Raw | ConvertFrom-Json -AsHashtable
$ratings = Get-Content (Join-Path $assets 'fitness_ratings.json') -Raw | ConvertFrom-Json -AsHashtable
if ($audit.newRecordCount -ne $expectedCount -or @($audit.entries).Count -ne $expectedCount -or $reviews.Count -ne $expectedCount) {
    throw "Expected $expectedCount explicitly reviewed admissions for $Admission."
}
foreach ($entry in $audit.entries) {
    $id = [int]$entry.id
    $exercise = @($catalog | Where-Object id -EQ $id)
    $movementId = if ($exercise.Count -eq 1 -and [int]$exercise[0].sessionMovementId -gt 0) {
        [int]$exercise[0].sessionMovementId
    } else { $id }
    if ($exercise.Count -ne 1 -or
        (Get-ExerciseIdentityName -ExerciseId $id -Name $exercise[0].name) -cne $entry.name -or
        $movementId -ne [int]$entry.sessionMovementId) {
        throw "Admission identity changed: $id."
    }
    $review = $reviews[$id]
    if ($anatomyReviews.ContainsKey($id)) {
        # A later anatomy review may supersede only the exact admitted metadata.
        # The full current media and all ten verdicts still pass the normal gate.
        $updatedReview = $anatomyReviews[$id]
        if ([string]$updatedReview.previousMetadataSha256 -cne [string]$review.metadataSha256) {
            throw "Exercise $id anatomy review does not match its previous admission review."
        }
        $review = $updatedReview
    }
    $result = Get-CatalogReviewResult -Exercise $exercise[0] -AssetsRoot $assets -Review $review
    if ($result.Status -ne 'PASS') { throw "Exercise $id requires a new explicit media/metadata review: $($result.Status)." }
    $profiles = @($assessment.profiles.Values | Where-Object { $id -in $_.exerciseIds })
    if ($profiles.Count -ne 1 -or $entry.trainedLevels.Count -ne 10) {
        throw "Exercise $id requires exactly one complete ten-characteristic assessment."
    }
    Assert-PreciseAdmissionFitnessRatings -Id $id -Entry $entry -Profile $profiles[0] `
        -Dimensions $assessment.dimensions -Ratings $ratings
}
Write-Output "$expectedCount precise admissions match their explicit reviews, training claims, canonical identities and all ten complete runtime rating cells."
