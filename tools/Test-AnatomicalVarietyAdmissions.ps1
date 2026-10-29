$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$repositoryRoot = Split-Path $PSScriptRoot
$assetsRoot = Join-Path $repositoryRoot 'NomadicMethod/Assets'
$documentPath = Join-Path $repositoryRoot 'docs/catalog-audit/anatomical-variety-review-2026-10-26.json'
$document = Get-Content -LiteralPath $documentPath -Raw | ConvertFrom-Json -AsHashtable
$reviews = Read-CatalogReviewEvidence -Path $documentPath
$expectedIds = @(1069, 1071, 1209, 1216, 1292) + @(1720..1729)
if ($document.catalogRevision -ne 98 -or $document.status -cne 'PASS' -or
    (@($reviews.Keys | Sort-Object) -join ',') -cne (@($expectedIds | Sort-Object) -join ',')) {
    throw 'Every anatomical-variety admission and correction needs an explicit completed review.'
}
$catalog = @(Get-Content -LiteralPath (Join-Path $assetsRoot 'exercises.json') -Raw | ConvertFrom-Json)
$protocolHistory = Get-Content -LiteralPath (Join-Path $assetsRoot 'catalog_protocol_history.json') -Raw | ConvertFrom-Json
$poseReview = Get-Content -LiteralPath (Join-Path $repositoryRoot 'docs/catalog-audit/fixed-position-review-2026-10-29.json') -Raw | ConvertFrom-Json
$media = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck
$verified = Import-PowerShellDataFile -LiteralPath (Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
foreach ($id in $expectedIds) {
    $exercise = @($catalog | Where-Object id -eq $id)
    if ($exercise.Count -ne 1) { throw "Missing admitted exercise $id." }
    $entry = $reviews[$id]
    $reviewedExercise = $exercise[0]
    $protocol = @($protocolHistory.exercises | Where-Object id -eq $id)
    if ($protocol.Count -gt 0) {
        # Preserve the original anatomy evidence while requiring an explicit,
        # hash-bound later review of the changed execution geometry.
        $amendment = @($poseReview.entries | Where-Object id -eq $id)
        if ($protocol.Count -ne 1 -or $amendment.Count -ne 1 -or
            (Get-CatalogMetadataHash -Exercise $exercise[0]) -cne $amendment[0].metadataSha256) {
            throw "Exercise $id lacks its exact later execution review."
        }
        $reviewedExercise = $exercise[0] | ConvertTo-Json -Depth 100 | ConvertFrom-Json
        foreach ($field in @('sideSequence', 'directionSequence', 'sequenceBlocks')) {
            $reviewedExercise.$field = $protocol[0].$field
        }
        if ((Get-CatalogMetadataHash -Exercise $reviewedExercise) -cne $amendment[0].previousMetadataSha256) {
            throw "Exercise $id changed more than its reviewed execution geometry."
        }
    }
    $result = Get-CatalogReviewResult -Exercise $reviewedExercise -AssetsRoot $assetsRoot -Review $entry
    if ($result.Status -cne 'PASS') { throw "Exercise ${id}: $($result.Status) - $($result.Reason)" }
    foreach ($evidence in $entry.evidence) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $evidence) -PathType Leaf)) {
            throw "Missing visual evidence for exercise ${id}: $evidence"
        }
    }
    if ($id -lt 1720) { continue }
    if ($id -notin $verified.ReviewedExternal -or -not $media.ContainsKey($id) -or
        -not $media[$id].Human -or -not $media[$id].Video -or
        $media[$id].PingPong -or $media[$id].MirrorForAlternation -or
        $entry.source.localFile -cne $media[$id].LocalSourceFile -or
        $entry.source.localSha256 -cne $media[$id].LocalSourceSha256 -or
        $entry.source.originalSha256 -cne $media[$id].OriginalSourceSha256 -or
        $entry.source.page -cne $media[$id].SourcePage -or
        $entry.source.video -cne $media[$id].Url -or
        $entry.source.originalStartSeconds -ne $media[$id].OriginalSourceStartSeconds -or
        $entry.source.durationSeconds -ne $media[$id].DurationSeconds -or
        $entry.source.crop -cne $media[$id].Crop -or
        $entry.source.framesPerSecond -ne $media[$id].FramesPerSecond -or
        $entry.source.sourceWidth -ne $media[$id].SourceWidth -or
        $entry.source.sourceHeight -ne $media[$id].SourceHeight) {
        throw "Exercise $id lacks its reviewed human source."
    }
    $sourcePath = Join-Path (Join-Path $PSScriptRoot 'ReviewedSourceMedia') $entry.source.localFile
    if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.source.localSha256) {
        throw "Exercise $id reviewed source bytes have changed."
    }
}
Write-Output 'Anatomical variety: ten distinct new admissions and five anatomy corrections match their completed reviews and exact assets.'
