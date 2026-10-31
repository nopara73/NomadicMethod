$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$root = Split-Path $PSScriptRoot
$assets = Join-Path $root 'NomadicMethod/Assets'
$path = Join-Path $root 'docs/catalog-audit/context-response-review-2026-10-31.json'
$document = Get-Content $path -Raw | ConvertFrom-Json -AsHashtable
$reviews = Read-CatalogReviewEvidence $path
$cases = Get-Content (Join-Path $root 'web/tests/fixtures/context-response-cases.json') -Raw | ConvertFrom-Json
$catalog = @(Get-Content (Join-Path $assets 'exercises.json') -Raw | ConvertFrom-Json)
$drills = Get-Content (Join-Path $assets 'reaction_drills.json') -Raw | ConvertFrom-Json
$ratings = Get-Content (Join-Path $assets 'fitness_ratings.json') -Raw | ConvertFrom-Json -AsHashtable
$copies = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'ExactExerciseMediaCopies.psd1') -SkipLimitCheck
$verified = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
if ($document.schemaVersion -ne 1 -or $document.catalogRevision -ne 107 -or $document.status -cne 'PASS' -or
    (@($reviews.Keys | Sort-Object) -join ',') -cne '1741,1742,1743,1744') {
    throw 'Four completed context-response workout reviews are required.'
}
# An append-only admission cannot silently rebind an older protocol review.
$null = Get-HistoricalReactionReviewText ([IO.File]::ReadAllText((Join-Path $assets 'reaction_drills.json'))) 1602
foreach ($case in $cases.families) {
    $id = [int]$case.id
    $exercise = $catalog | Where-Object id -eq $id
    $entry = $reviews[$id]
    $result = Get-CatalogReviewResult -Exercise $exercise -AssetsRoot $assets -Review $entry
    if ($result.Status -cne 'PASS' -or (Get-CatalogMetadataHash $exercise) -cne $entry.metadataSha256) {
        throw "Context exercise $id requires its exact current review: $($result.Status)."
    }
    foreach ($file in $entry.evidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "Missing evidence $file." }
    }
    if (-not $entry.workoutReview.syntheticFixture -or $entry.workoutReview.minutes -ne 3 -or
        $entry.workoutReview.modifiers -ne 0 -or $entry.workoutReview.errors.Count -ne 0 -or
        ($entry.workoutReview.phases -join ',') -cne 'Ready,Preparation,Movement,Rest') {
        throw "Missing real product phase review for $id."
    }
    $plan = @($drills.plans | Where-Object exerciseId -eq $id)
    $family = @($drills.families | Where-Object id -ceq $case.family)
    if ($plan.Count -ne 1 -or $plan[0].protocol -cne 'ContextChoice' -or $plan[0].familyId -cne $case.family -or
        $family.Count -ne 1 -or ($family[0].actions.sourceId -join ',') -cne ($case.sources -join ',') -or
        [int]$exercise.sessionMovementId -ne $case.movementId -or [int]$copies[$id] -ne $case.sources[0] -or
        $id -notin $verified.ReviewedExactCopies -or $exercise.insectCompatibility -cne 'Incompatible') {
        throw "Context exercise $id changed its reviewed physical identity or protocol."
    }
    $claims = @($exercise.primaryCanonicalGroup) + @($exercise.secondaryCanonicalGroups)
    foreach ($action in $family[0].actions) {
        $source = $catalog | Where-Object id -eq $action.sourceId
        if ($source.id -notin $verified.ReviewedExternal -and $source.id -notin $verified.ReviewedExactCopies) {
            throw 'Every response must retain its approved human source.'
        }
        if ($source.mode -cne 'Repetition' -or $source.sideSequence -cnotin @('Continuous', 'Alternating') -or
            $source.directionSequence -cne 'None' -or $source.sequenceBlocks.Count -ne 1 -or
            $source.sequenceBlocks[0].exerciseId -ne $source.id) {
            throw 'Every response must contain its complete natural action without a missing timed side or direction.'
        }
        $sourceClaims = @($source.primaryCanonicalGroup) + @($source.secondaryCanonicalGroups)
        if (@($claims | Where-Object { $_ -notin $sourceClaims }).Count) { throw 'A response lost a claimed trained muscle.' }
        foreach ($field in @('onlyFeetTouchGround', 'floorContactPoints', 'shoeAgnostic',
                'wallRequired', 'soleWallContactRequired', 'elevationRequired', 'jumpPlatformRequired',
                'locomotionSpace', 'hardFloorCompatibility', 'silent', 'shyCompatibility',
                'upperBodyClothingRequirement', 'mirrorRelationship', 'minimumMirrorCoverage')) {
            if ($source.$field -cne $exercise.$field) { throw "A response changed its physical restriction: $field." }
        }
        $movie = Join-Path $assets $source.video
        $seconds = [double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $movie)
        if ($LASTEXITCODE -ne 0 -or $action.sourceStartSeconds -ne 0 -or $action.durationSeconds -gt 10 -or
            [Math]::Abs($action.durationSeconds - $seconds) -gt 0.06 -or
            (Get-FileHash $movie -Algorithm SHA256).Hash.ToLowerInvariant() -cne $action.sourceVideoSha256) {
            throw 'A response no longer retains its complete natural source loop.'
        }
    }
    $levels = $ratings.ratings[[string]$id]
    $mask = $ratings.trainingCharacteristics[[string]$id]
    for ($axis = 0; $axis -lt 10; $axis++) {
        $expectedLevels = if ($axis -eq 6) { '2' } elseif ($axis -eq 8) { '1,3,4,5' } else { '1' }
        if (($levels[$axis] -join ',') -cne $expectedLevels) { throw "Changed conservative rating for $id, axis $axis." }
    }
    if (($mask -join ',') -cne 'False,False,True,False,False,False,True,False,True,False') {
        throw 'Context response ratings must retain their exact conservative physical claims.'
    }
}
if (($document.metadataAmendments.id -join ',') -cne '146,804') { throw 'Unexpected historical metadata amendments.' }
foreach ($amendment in $document.metadataAmendments) {
    $exercise = $catalog | Where-Object id -eq $amendment.id
    if ((Get-CatalogMetadataHash $exercise) -cne $amendment.metadataSha256 -or
        [int]$exercise.sessionMovementId -ne [int]$exercise.id) { throw 'Stale explicit self-identity declaration.' }
    $prior = $exercise | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    $prior.PSObject.Properties.Remove('sessionMovementId')
    if ((Get-CatalogMetadataHash $prior) -cne $amendment.previousMetadataSha256) { throw 'Existing physical metadata changed.' }
}
Write-Output 'Context admissions: four distinct physical families, sixteen approved complete human responses, exact ratings and product-phase evidence verified.'
