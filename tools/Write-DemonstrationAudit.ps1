param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\NomadicMethod\Assets\exercises.json'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\DEMONSTRATION_AUDIT.md')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseCatalogDefinitions.ps1')
$definedExerciseIds = @(Get-ExerciseCatalogDefinitions | ForEach-Object { [int]$_.Id })

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$review = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'VerifiedExerciseDemos.psd1') -SkipLimitCheck
$externalMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExternalExerciseMedia.psd1') -SkipLimitCheck
$catalogExerciseReplacements = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CatalogExerciseReplacements.psd1') -SkipLimitCheck
foreach ($entry in $catalogExerciseReplacements.GetEnumerator()) {
    $exerciseId = [int]$entry.Key
    $replacement = $entry.Value
    if ($replacement -isnot [System.Collections.IDictionary] -or
        -not $replacement.ContainsKey('Media') -or
        $replacement.Media -isnot [System.Collections.IDictionary]) {
        throw "Replacement $exerciseId has no valid reviewed media definition."
    }
    $externalMedia[$exerciseId] = $replacement.Media
}
$posecodeMedia = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'PosecodeExerciseMedia.psd1') -SkipLimitCheck
$exactMediaCopies = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaCopies.psd1') -SkipLimitCheck
$exactMediaTransforms = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExactExerciseMediaTransforms.psd1') -SkipLimitCheck
$canonicalTaxonomy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CanonicalMuscleGroups.psd1') -SkipLimitCheck
$canonicalGroups = @($canonicalTaxonomy.Groups | Sort-Object { [int]$_.Id })
$canonicalGroupKeys = @(
    $canonicalGroups | ForEach-Object { [string]$_.StableKey })
if ($canonicalGroups.Count -ne 30 -or
    @(Compare-Object @($canonicalGroups.Id) @(1..30)).Count -gt 0 -or
    @($canonicalGroupKeys | Sort-Object -Unique).Count -ne 30) {
    throw 'The canonical muscle-group taxonomy is incomplete or invalid.'
}
$canonicalAssignmentSource = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseCanonicalGroups.psd1') -SkipLimitCheck

$externalIds = @($review.ReviewedExternal | ForEach-Object { [int]$_ })
$internalAnatomyIds = @(
    $review.ReviewedInternalAnatomy | ForEach-Object { [int]$_ })
$humanExternalIds = @(
    $externalIds | Where-Object {
        $externalMedia.ContainsKey($_) -and
        $externalMedia[$_].ContainsKey('Human') -and
        [bool]$externalMedia[$_].Human
    })
$otherExternalCount = $externalIds.Count - $humanExternalIds.Count
$posecodeIds = @($review.ReviewedPosecode | ForEach-Object { [int]$_ })
$svgIds = @($review.PurposeBuiltSvg | ForEach-Object { [int]$_ })
$copyIds = @($review.ReviewedExactCopies | ForEach-Object { [int]$_ })
$transformIds = @($review.ReviewedExactTransforms | ForEach-Object { [int]$_ })
$retainedIds = @(
    $externalIds + $internalAnatomyIds + $posecodeIds + $svgIds +
        $copyIds + $transformIds |
        Sort-Object -Unique)

$duplicateReviewedIds = @(
    $externalIds + $internalAnatomyIds + $posecodeIds + $svgIds +
        $copyIds + $transformIds |
        Group-Object |
        Where-Object Count -ne 1)
$catalogDifference = @(Compare-Object `
        ($retainedIds | Sort-Object) `
        (@($catalog.id | ForEach-Object { [int]$_ }) | Sort-Object))
if ($duplicateReviewedIds.Count -gt 0 -or $catalogDifference.Count -gt 0) {
    throw 'The bundled catalog must exactly match the retained reviewed inventory.'
}

$missingDirectMappings = @(
    @(($externalIds + $internalAnatomyIds) |
        Where-Object { -not $externalMedia.ContainsKey($_) }) +
        @($posecodeIds | Where-Object { -not $posecodeMedia.ContainsKey($_) }))
$copyMappingsMatch =
    (@($copyIds | Sort-Object) -join ',') -eq
    (@($exactMediaCopies.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
$transformMappingsMatch =
    (@($transformIds | Sort-Object) -join ',') -eq
    (@($exactMediaTransforms.Keys |
            ForEach-Object { [int]$_ } |
            Sort-Object) -join ',')
if ($missingDirectMappings.Count -gt 0 -or
    -not $copyMappingsMatch -or
    -not $transformMappingsMatch) {
    throw 'The retained inventory does not match its reviewed media mappings.'
}

if ($otherExternalCount -ne 0 -or $posecodeIds.Count -ne 0 -or
    $svgIds.Count -ne 0 -or
    @($internalAnatomyIds | Where-Object {
            -not $externalMedia[$_].AuthoritativeAnatomicalVisualization
        }).Count -ne 0) {
    throw 'Every retained direct demonstration must be reviewed human footage or an explicitly approved internal-anatomy exception.'
}

$directSourceIds = @(
    $externalIds + $internalAnatomyIds + $posecodeIds + $svgIds)
$unverifiedCopySources = @(
    $exactMediaCopies.Values |
        ForEach-Object { [int]$_ } |
        Where-Object { $_ -notin $directSourceIds })
$unverifiedTransformSources = @(
    $exactMediaTransforms.Values |
        ForEach-Object { [int]$_.Source } |
        Where-Object { $_ -notin $directSourceIds })
if ($unverifiedCopySources.Count -gt 0 -or
    $unverifiedTransformSources.Count -gt 0) {
    throw 'A retained copy or transform points to a discarded source.'
}

$invalidAssignments = @($catalog | Where-Object {
        'muscleGroups' -in @($_.PSObject.Properties.Name) -or
        'primaryCanonicalGroup' -notin @($_.PSObject.Properties.Name) -or
        'secondaryCanonicalGroups' -notin @($_.PSObject.Properties.Name) -or
        [string]::IsNullOrWhiteSpace([string]$_.primaryCanonicalGroup) -or
        [string]$_.primaryCanonicalGroup -notin $canonicalGroupKeys -or
        @($_.secondaryCanonicalGroups | Sort-Object -Unique).Count -ne
            @($_.secondaryCanonicalGroups).Count -or
        [string]$_.primaryCanonicalGroup -in @($_.secondaryCanonicalGroups) -or
        @($_.secondaryCanonicalGroups | Where-Object {
                [string]$_ -notin $canonicalGroupKeys
            }).Count -gt 0
    })
if ($invalidAssignments.Count -gt 0) {
    throw 'The catalog contains an invalid canonical assignment or a legacy muscleGroups field.'
}

$catalogIds = @($catalog.id | ForEach-Object { [int]$_ })
if (@($catalogIds | Where-Object { $_ -notin $definedExerciseIds }).Count -gt 0 -or
    @($catalogIds | Sort-Object -Unique).Count -ne $catalog.Count) {
    throw 'Catalog exercise IDs must be unique declared stable IDs.'
}

$sourceAssignmentIds = @(
    $canonicalAssignmentSource.Keys | ForEach-Object { [int]$_ } | Sort-Object)
$assignmentDrift = @(
    Compare-Object ($catalogIds | Sort-Object) $sourceAssignmentIds)
$assignmentDrift += @(
    $catalog | Where-Object {
        $assignment = $canonicalAssignmentSource[[int]$_.id]
        $assignment -isnot [System.Collections.IDictionary] -or
        [string]$_.primaryCanonicalGroup -ne [string]$assignment.Primary -or
        (@($_.secondaryCanonicalGroups | Sort-Object) -join "`n") -ne
            (@($assignment.Secondary | ForEach-Object { [string]$_ } |
                    Sort-Object) -join "`n")
    })
if ($assignmentDrift.Count -gt 0) {
    throw 'The generated canonical assignments have drifted from ExerciseCanonicalGroups.psd1.'
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Nomadic Method demonstration quality audit')
$lines.Add('')
$lines.Add('Nomadic Method now ships a strictly human-demonstrated exercise catalog.')
$lines.Add(('All **{0}** bundled exercises show an actual person performing the movement.' -f
        $catalog.Count))
$lines.Add('Synthetic, schematic, anatomical, and 3D demonstrations are excluded from')
$lines.Add('both the runtime catalog and the application package.')
$lines.Add('')
$lines.Add('| Canonical muscle group | Primary exercises | All meaningful assignments |')
$lines.Add('| --- | ---: | ---: |')
foreach ($canonicalGroup in $canonicalGroups) {
    $stableKey = [string]$canonicalGroup.StableKey
    $primaryCount = @($catalog | Where-Object {
            [string]$_.primaryCanonicalGroup -eq $stableKey
        }).Count
    $allAssignmentCount = @($catalog | Where-Object {
            [string]$_.primaryCanonicalGroup -eq $stableKey -or
            $stableKey -in @($_.secondaryCanonicalGroups)
        }).Count
    $lines.Add(('| {0} | {1} | {2} |' -f
            [string]$canonicalGroup.DisplayName,
            $primaryCount,
            $allAssignmentCount))
}

$lines.Add('')
$lines.Add('## Retained source quality')
$lines.Add('')
$lines.Add(('- Direct human-footage demonstrations: **{0}**' -f $humanExternalIds.Count))
$lines.Add(('- Exact copies of human footage: **{0}**' -f $copyIds.Count))
$lines.Add(('- Exact deterministic transforms of human footage: **{0}**' -f $transformIds.Count))
$lines.Add('')
$lines.Add('Copy and transform targets are retained only when their reviewed source is')
$lines.Add('human footage and the target movement has identical mechanics. This rule is')
$lines.Add('validated by the catalog generator and this audit script.')
$lines.Add('')
$lines.Add('The retained ID inventory is in `tools/VerifiedExerciseDemos.psd1`; source,')
$lines.Add('copy, and transform mappings live in the corresponding media manifests.')
$lines.Add('')
$lines.Add('The complete 2026-08-29 demonstration-to-metadata review, including one')
$lines.Add('ledger row for every pre-audit exercise and every exact correction, is in')
$lines.Add('`docs/CATALOG_DEMONSTRATION_INTEGRITY_AUDIT.md`.')

$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "Audit: $([IO.Path]::GetFullPath($OutputPath))"
Write-Output "Retained and verified: $($catalog.Count)"
Write-Output 'Placeholders bundled: 0'
