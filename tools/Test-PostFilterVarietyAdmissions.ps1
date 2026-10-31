$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$root = Split-Path $PSScriptRoot
$assets = Join-Path $root 'NomadicMethod/Assets'
$path = Join-Path $root 'docs/catalog-audit/post-filter-variety-review-2026-10-31.json'
$document = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
$reviews = Read-CatalogReviewEvidence -Path $path
$catalog = @(Get-Content -LiteralPath (Join-Path $assets 'exercises.json') -Raw | ConvertFrom-Json)
$media = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck
$verified = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
$assessment = Get-Content (Join-Path $PSScriptRoot 'ExerciseFitnessCeilingScores.json') -Raw | ConvertFrom-Json -AsHashtable
$ratings = Get-Content (Join-Path $assets 'fitness_ratings.json') -Raw | ConvertFrom-Json -AsHashtable
$expected = @(1069) + @(1730..1740)
if ($document.schemaVersion -ne 1 -or $document.catalogRevision -ne 105 -or $document.status -cne 'PASS' -or
    (@($reviews.Keys | Sort-Object) -join ',') -cne (@($expected | Sort-Object) -join ',')) {
    throw 'Every post-filter admission and physical correction needs its completed exact review.'
}
foreach ($id in $expected) {
    $exercise = @($catalog | Where-Object id -eq $id)
    if ($exercise.Count -ne 1) { throw "Missing post-filter exercise $id." }
    $entry = $reviews[$id]
    $result = Get-CatalogReviewResult -Exercise $exercise[0] -AssetsRoot $assets -Review $entry
    # New evidence must bind the current metadata directly, without historical normalization.
    if ($result.Status -cne 'PASS' -or (Get-CatalogMetadataHash $exercise[0]) -cne $entry.metadataSha256) {
        throw "Exercise $id requires a new exact workout review: $($result.Status)."
    }
    foreach ($file in $entry.evidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "Missing visual evidence $file." }
    }
    if (-not $entry.workoutReview.syntheticFixture -or $entry.workoutReview.minutes -ne 45 -or
        $entry.workoutReview.modifiers -ne 5232 -or $entry.workoutReview.errors.Count -ne 0 -or
        ($entry.workoutReview.phases -join ',') -cne 'Ready,Preparation,Movement,Rest') {
        throw "Exercise $id lacks its actual product phase review."
    }
    if ($id -eq 1069) { continue }
    $source = $media[$id]
    if ($id -notin $verified.ReviewedExternal -or -not $source.Human -or -not $source.Video -or
        $source.PingPong -or $source.MirrorForAlternation) { throw "Exercise $id lacks exact human media." }
    foreach ($field in @('LocalSourceFile','LocalSourceSha256','OriginalSourceSha256','OriginalSourceStartSeconds',
            'Url','SourcePage','SourceWidth','SourceHeight','DurationSeconds','FramesPerSecond','Crop')) {
        if ($entry.source[$field] -cne $source[$field]) { throw "Exercise $id source field changed: $field." }
    }
    $sourcePath = Join-Path (Join-Path $PSScriptRoot 'ReviewedSourceMedia') $source.LocalSourceFile
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $source.LocalSourceSha256) {
        throw "Exercise $id source bytes changed."
    }
    $runtime = Join-Path $assets $exercise[0].video
    $seconds = [double](& ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $runtime)
    # Hold previews retain the natural lead-in to the exact hold frame and a
    # cloned tail; movement shows the separately hash-bound still for 45 seconds.
    $timingValid = if ($exercise[0].mode -eq 'Hold') {
        $seconds -gt 0 -and $seconds -le $source.DurationSeconds + 0.35
    } else { [Math]::Abs($seconds - $source.DurationSeconds) -le 0.06 }
    if ($LASTEXITCODE -ne 0 -or -not $timingValid) {
        throw "Exercise $id no longer preserves its reviewed source timing."
    }
    $profiles = @($assessment.profiles.Values | Where-Object { $id -in $_.exerciseIds })
    if ($profiles.Count -ne 1) { throw "Exercise $id requires one explicit ten-characteristic assessment." }
    for ($axis = 0; $axis -lt 10; $axis++) {
        $dimension = $assessment.dimensions[$axis]
        $levels = $profiles[0].levels[$dimension]
        $claim = $profiles[0].trainingCharacteristics[$dimension]
        if ($levels.Count -eq 0 -or $claim -isnot [bool] -or
            (ConvertTo-Json -InputObject $levels -Compress) -cne
                (ConvertTo-Json -InputObject $ratings.ratings[[string]$id][$axis] -Compress) -or
            $claim -cne $ratings.trainingCharacteristics[[string]$id][$axis]) {
            throw "Exercise $id has a changed or incomplete $dimension rating/claim."
        }
    }
}
if (($document.metadataAmendments.id | Sort-Object) -join ',' -cne '740,784,1069,1522') {
    throw 'The exact four reviewed metadata amendments must remain explicit.'
}
foreach ($amendment in $document.metadataAmendments) {
    $exercise = $catalog | Where-Object id -eq $amendment.id
    if ((Get-CatalogMetadataHash $exercise) -cne $amendment.metadataSha256) { throw "Stale amendment $($amendment.id)." }
    $prior = $exercise | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    foreach ($field in $amendment.currentFields.Keys) {
        if ($exercise.$field -cne $amendment.currentFields[$field]) { throw 'Amended field changed.' }
    }
    foreach ($field in $amendment.previousFields.Keys) { $prior.$field = $amendment.previousFields[$field] }
    foreach ($field in $amendment.previousAbsentFields) { $prior.PSObject.Properties.Remove($field) }
    if ((Get-CatalogMetadataHash $prior) -cne $amendment.previousMetadataSha256) {
        throw "Exercise $($amendment.id) changed beyond its exact reviewed field."
    }
    if ($amendment.id -ne 1069 -and [int]$exercise.sessionMovementId -ne [int]$exercise.id) {
        throw 'An explicit family-root declaration must preserve its existing effective movement ID.'
    }
}
Write-Output 'Post-filter variety: eleven human demonstrations, one physical correction and three equivalent family declarations match exact source, rating and workout evidence.'

