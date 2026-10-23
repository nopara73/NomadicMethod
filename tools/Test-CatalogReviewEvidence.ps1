param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CatalogReviewEvidence.ps1')
$testKey = 'nomadic-method-review-evidence-' + [Guid]::NewGuid().ToString('N')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) $testKey
New-Item -ItemType Directory -Force -Path (Join-Path $testRoot 'exercise_videos') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $testRoot 'exercise_hold_frames') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $testRoot 'exercise_direction_videos') | Out-Null
try {
    # These are hash fixtures, not decodable exercise media.
    $videoPath = Join-Path $testRoot 'exercise_videos/exercise_0001.mp4'
    [IO.File]::WriteAllText($videoPath, 'video fixture')
    $exercise = [pscustomobject][ordered]@{
        id = 1; name = 'Test action'; video = 'exercise_videos/exercise_0001.mp4'
        mode = 'Repetition'; directionSequence = 'None'
    }
    $missing = Get-CatalogReviewResult -Exercise $exercise -AssetsRoot $testRoot
    if ($missing.Status -ne 'UNREVIEWED' -or 'PASS' -in @($missing.Dimensions.Values)) {
        throw 'Missing evidence must never produce a PASS.'
    }
    $checks = @{}
    foreach ($dimension in $catalogReviewDimensions) {
        $checks[$dimension] = @{ status = 'PASS'; reason = 'Explicit test verdict.' }
    }
    $checks.HoldFrame.status = 'NOT_APPLICABLE'
    $review = @{
        id = 1; metadataSha256 = $missing.MetadataSha256; assets = $missing.AssetSha256
        reviewedAt = '2026-09-05T00:00:00Z'; method = 'Test fixture'
        observedAction = 'Explicit test observation'; evidence = @('fixture-reference')
        checks = $checks
    }
    function Assert-ReviewStatus([string]$Expected) {
        $result = Get-CatalogReviewResult -Exercise $exercise -AssetsRoot $testRoot -Review $review
        if ($result.Status -ne $Expected) { throw "Expected $Expected, received $($result.Status): $($result.Reason)" }
    }
    Assert-ReviewStatus 'PASS'
    $checks.Metadata.status = 'FAIL'
    Assert-ReviewStatus 'FAIL'
    $checks.Metadata.status = 'PASS'
    $checks.Remove('Workout')
    Assert-ReviewStatus 'UNREVIEWED'
    $checks.Workout = @{ status = 'PASS'; reason = '' }
    Assert-ReviewStatus 'UNREVIEWED'
    $checks.Workout.reason = 'Explicit workout test verdict.'
    $exercise.name = 'A different action'
    Assert-ReviewStatus 'STALE'
    $exercise.name = 'Test action'
    [IO.File]::WriteAllText($videoPath, 'changed video fixture')
    Assert-ReviewStatus 'STALE'
    [IO.File]::WriteAllText($videoPath, 'video fixture')
    Assert-ReviewStatus 'PASS'

    $exercise.mode = 'Hold'
    [IO.File]::WriteAllText((Join-Path $testRoot 'exercise_hold_frames/exercise_0001.png'), 'hold fixture')
    $review.metadataSha256 = Get-CatalogMetadataHash -Exercise $exercise
    Assert-ReviewStatus 'STALE'
    $review.assets = Get-CatalogAssetHashes -Exercise $exercise -AssetsRoot $testRoot
    Assert-ReviewStatus 'UNREVIEWED' # HoldFrame cannot be marked inapplicable for a hold.
    $checks.HoldFrame.status = 'PASS'
    Assert-ReviewStatus 'PASS'
    [IO.File]::WriteAllText((Join-Path $testRoot 'exercise_hold_frames/exercise_0001.png'), 'changed hold')
    Assert-ReviewStatus 'STALE'

    $exercise.directionSequence = 'ClockwiseThenCounterclockwise'
    [IO.File]::WriteAllText((Join-Path $testRoot 'exercise_direction_videos/exercise_0001.mp4'), 'direction fixture')
    $review.metadataSha256 = Get-CatalogMetadataHash -Exercise $exercise
    Assert-ReviewStatus 'STALE'
    $review.assets = Get-CatalogAssetHashes -Exercise $exercise -AssetsRoot $testRoot
    Assert-ReviewStatus 'PASS'
    [IO.File]::WriteAllText((Join-Path $testRoot 'exercise_direction_videos/exercise_0001.mp4'), 'changed direction')
    Assert-ReviewStatus 'STALE'
    $exercise.mode = 'Repetition'
    $exercise.directionSequence = 'None'
    $exercise | Add-Member -NotePropertyName practice -NotePropertyValue 'Reaction training'
    foreach ($directory in @('reaction_videos', 'reaction_previews')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $testRoot $directory) | Out-Null
    }
    $actions = @(1..4 | ForEach-Object {
        $video = "reaction_videos/response-$_.mp4"
        $preview = "reaction_previews/response-$_.png"
        [IO.File]::WriteAllText((Join-Path $testRoot $video), 'response hash fixture')
        [IO.File]::WriteAllText((Join-Path $testRoot $preview), 'preview hash fixture')
        @{ video = $video; preview = $preview }
    })
    $definition = @{ version = 1; families = @(@{ id = 'test'; actions = $actions });
        plans = @(@{ exerciseId = 1; familyId = 'test'; protocol = 'TwoChoice' }) } |
        ConvertTo-Json -Depth 10 -Compress
    $definitionPath = Join-Path $testRoot 'reaction_drills.json'
    [IO.File]::WriteAllText($definitionPath, $definition)
    $review.metadataSha256 = Get-CatalogMetadataHash -Exercise $exercise
    $review.assets = Get-CatalogAssetHashes -Exercise $exercise -AssetsRoot $testRoot
    $checks.HoldFrame.status = 'NOT_APPLICABLE'
    Assert-ReviewStatus 'PASS'
    foreach ($asset in @('reaction_videos/response-1.mp4', 'reaction_previews/response-2.png', 'reaction_drills.json')) {
        $path = Join-Path $testRoot $asset
        $original = [IO.File]::ReadAllText($path)
        [IO.File]::WriteAllText($path, "$original ")
        Assert-ReviewStatus 'STALE'
        [IO.File]::WriteAllText($path, $original)
        Assert-ReviewStatus 'PASS'
    }
    # JSON newline conversion must survive Windows/Linux checkouts, while a
    # real protocol-text or binary change still invalidates the review.
    $multiline = "{`n" + $definition.Substring(1)
    [IO.File]::WriteAllText($definitionPath, $multiline)
    $review.assets = Get-CatalogAssetHashes -Exercise $exercise -AssetsRoot $testRoot
    [IO.File]::WriteAllText($definitionPath, $multiline.Replace("`n", "`r`n"))
    Assert-ReviewStatus 'PASS'
    [IO.File]::WriteAllText($definitionPath, $multiline.Replace('TwoChoice', 'StopOnly'))
    Assert-ReviewStatus 'STALE'
    [IO.File]::WriteAllText($definitionPath, $multiline)
    Assert-ReviewStatus 'PASS'
    Write-Output 'Review evidence tests passed: missing, partial, failed, changed metadata, video, hold, direction, reaction response, preview and protocol assets.'
}
finally {
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedRoot -ne [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $testKey)) -or
        [IO.Path]::GetFileName($resolvedRoot) -ne $testKey) {
        throw 'Refusing to clean an unexpected review-test directory.'
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
