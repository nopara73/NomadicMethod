# Only explicit review records can establish approval. Unchanged metadata and
# successful decoding do not establish visual or anatomical correctness.
. (Join-Path $PSScriptRoot 'ExerciseDisplayNames.ps1')
$catalogReviewDimensions = @(
    'Metadata', 'FullLoop', 'Crop', 'LoopSeam', 'PlaybackSpeed',
    'Mirroring', 'Travel', 'HoldFrame', 'Equipment', 'Workout')

function Get-CatalogMetadataHash {
    param([Parameter(Mandatory)]$Exercise)
    # Dated physical reviews keep their original identity wording. Only exact
    # reviewed display aliases normalize; other metadata and unknown names do not.
    $reviewed = $Exercise | ConvertTo-Json -Depth 100 | ConvertFrom-Json
    $reviewed.name = Get-ExerciseIdentityName -ExerciseId ([int]$reviewed.id) -Name ([string]$reviewed.name)
    $json = ConvertTo-Json -InputObject $reviewed -Depth 100 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-CatalogAssetHashes {
    param([Parameter(Mandatory)]$Exercise, [Parameter(Mandatory)][string]$AssetsRoot)
    $id = [int]$Exercise.id
    $expectedVideo = 'exercise_videos/exercise_{0:D4}.mp4' -f $id
    if ([string]$Exercise.video -cne $expectedVideo) {
        throw "Unexpected runtime video path for exercise $id."
    }
    $paths = @($expectedVideo)
    if ([string]$Exercise.mode -eq 'Hold') {
        $paths += 'exercise_hold_frames/exercise_{0:D4}.png' -f $id
    }
    if ([string]$Exercise.directionSequence -ne 'None') {
        $paths += 'exercise_direction_videos/exercise_{0:D4}.mp4' -f $id
    }
    if ([string]$Exercise.practice -eq 'Reaction training') {
        $definition = 'reaction_drills.json'
        $drills = Get-Content -LiteralPath (Join-Path $AssetsRoot $definition) -Raw | ConvertFrom-Json
        $plans = @($drills.plans | Where-Object { [int]$_.exerciseId -eq $id })
        if ($plans.Count -ne 1) { throw "Missing exact reaction plan for exercise $id." }
        $families = @($drills.families | Where-Object { $_.id -ceq $plans[0].familyId })
        $count = switch ([string]$plans[0].protocol) {
            'Initiation' { 1 }; 'StopOnly' { 1 }; 'TwoChoice' { 2 }; 'FourChoice' { 4 }; 'ContextChoice' { 4 }
            default { throw "Invalid reaction protocol for exercise $id." }
        }
        if ($families.Count -ne 1 -or @($families[0].actions).Count -ne 4) {
            throw "Missing exact response family for exercise $id."
        }
        $paths += $definition
        foreach ($action in @($families[0].actions | Select-Object -First $count)) {
            $paths += [string]$action.video
            $paths += [string]$action.preview
        }
    }
    $hashes = [ordered]@{}
    foreach ($relative in $paths) {
        $path = Join-Path $AssetsRoot $relative
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing runtime asset for exercise ${id}: $relative"
        }
        if ($relative -eq 'reaction_drills.json') {
            # Checkouts may change JSON newlines. Bind the exact UTF-8 text
            # after LF normalization; all binary movie/frame hashes stay raw.
            $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
            $text = Restore-ReviewedReactionLabels -Text $text
            $bytes = [Text.Encoding]::UTF8.GetBytes($text)
            $hashes[$relative] = [Convert]::ToHexString(
                [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        } else {
            $hashes[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
        }
    }
    return $hashes
}

function Get-CatalogReviewResult {
    param(
        [Parameter(Mandatory)]$Exercise,
        [Parameter(Mandatory)][string]$AssetsRoot,
        [System.Collections.IDictionary]$Review
    )
    $metadataHash = Get-CatalogMetadataHash -Exercise $Exercise
    $assetHashes = Get-CatalogAssetHashes -Exercise $Exercise -AssetsRoot $AssetsRoot
    # Exact, separately reviewed metadata amendments may retain earlier evidence.
    # Both fingerprints must match; unknown metadata or asset drift stays stale.
    if ($null -ne $Review -and $metadataHash -cne [string]$Review.metadataSha256) {
        $amendmentPath = Join-Path $PSScriptRoot '../docs/catalog-audit/post-filter-variety-review-2026-10-31.json'
        if (Test-Path -LiteralPath $amendmentPath) {
            $amendments = (Get-Content -LiteralPath $amendmentPath -Raw | ConvertFrom-Json).metadataAmendments
            $matched = @($amendments | Where-Object {
                [int]$_.id -eq [int]$Exercise.id -and
                $_.metadataSha256 -ceq $metadataHash -and
                $_.previousMetadataSha256 -ceq [string]$Review.metadataSha256
            })
            if ($matched.Count -eq 1) { $metadataHash = [string]$matched[0].previousMetadataSha256 }
        }
    }
    $dimensions = [ordered]@{}
    foreach ($name in $catalogReviewDimensions) { $dimensions[$name] = 'UNREVIEWED' }
    $result = [ordered]@{
        Status = 'UNREVIEWED'
        Reason = 'No explicit review is recorded for this exercise.'
        MetadataSha256 = $metadataHash
        AssetSha256 = $assetHashes
        ActualFinalDemonstration = 'Not established by this comparison.'
        Dimensions = $dimensions
    }
    if ($null -eq $Review) { return [pscustomobject]$result }

    if ([string]$Review.metadataSha256 -cne $metadataHash -or
        $Review.assets -isnot [System.Collections.IDictionary] -or
        @($Review.assets.Keys).Count -ne @($assetHashes.Keys).Count -or
        @($assetHashes.Keys | Where-Object {
            [string]$Review.assets[$_] -cne [string]$assetHashes[$_]
        }).Count -gt 0) {
        $result.Status = 'STALE'
        $result.Reason = 'The recorded review does not match the current metadata and every runtime asset.'
        return [pscustomobject]$result
    }

    $reviewedAt = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse([string]$Review.reviewedAt, [ref]$reviewedAt) -or
        [string]::IsNullOrWhiteSpace([string]$Review.method) -or
        [string]::IsNullOrWhiteSpace([string]$Review.observedAction) -or
        @($Review.evidence | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count -eq 0 -or
        $Review.checks -isnot [System.Collections.IDictionary]) {
        $result.Reason = 'The review lacks its date, method, observed action, evidence, or explicit checks.'
        return [pscustomobject]$result
    }
    $result.ActualFinalDemonstration = [string]$Review.observedAction
    foreach ($name in $catalogReviewDimensions) {
        $check = $Review.checks[$name]
        if ($check -isnot [System.Collections.IDictionary] -or
            [string]::IsNullOrWhiteSpace([string]$check.reason)) { continue }
        $status = [string]$check.status
        if ($status -cin @('PASS', 'FAIL')) { $dimensions[$name] = $status }
        elseif ($status -ceq 'NOT_APPLICABLE' -and $name -eq 'HoldFrame' -and
            [string]$Exercise.mode -ne 'Hold') { $dimensions[$name] = $status }
    }
    if ('FAIL' -in @($dimensions.Values)) {
        $result.Status = 'FAIL'
        $result.Reason = 'The explicit review records one or more failed checks.'
    }
    elseif ('UNREVIEWED' -notin @($dimensions.Values)) {
        $result.Status = 'PASS'
        $result.Reason = 'Every required check has an explicit verdict bound to the current metadata and runtime assets.'
    }
    else {
        $result.Reason = 'One or more required review dimensions remain unreviewed.'
    }
    return [pscustomobject]$result
}

function Read-CatalogReviewEvidence {
    param([string]$Path)
    $byId = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) { return $byId }
    $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    if ($document.schemaVersion -ne 1 -or $document.entries -isnot [array]) {
        throw 'Catalog review evidence must use schemaVersion 1 and an entries array.'
    }
    foreach ($entry in $document.entries) {
        $id = [int]$entry.id
        if ($id -lt 1 -or $byId.ContainsKey($id)) {
            throw 'Catalog review evidence contains an invalid or repeated exercise ID.'
        }
        $byId[$id] = $entry
    }
    return $byId
}
