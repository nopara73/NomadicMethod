param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\NomadicMethod\Assets\exercises.json'),
    [string]$GraphPath = (Join-Path $PSScriptRoot '..\docs\supplementary\movement-practices\movement_practices_graph.json'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\docs')
)

$ErrorActionPreference = 'Stop'

$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$graph = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
$taxonomy = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExercisePracticeTaxonomy.psd1') -SkipLimitCheck
$additionAuditPath = Join-Path $PSScriptRoot '..\docs\catalog_additions_2026-08-02.csv'
$additionAudit = @(Import-Csv -LiteralPath $additionAuditPath)
$additionIds = @($taxonomy.CatalogAdditionIds | ForEach-Object { [int]$_ })

$additionCsvIds = @($additionAudit | ForEach-Object { [int]$_.ID })
$duplicateAdditionIds = @(
    $additionCsvIds | Group-Object | Where-Object Count -ne 1)
$additionIdDifference = @(
    Compare-Object `
        ($additionIds | Sort-Object) `
        ($additionCsvIds | Sort-Object))
if ($duplicateAdditionIds.Count -gt 0 -or $additionIdDifference.Count -gt 0) {
    throw 'The reviewed catalog delta must exactly match CatalogAdditionIds.'
}

$catalogById = @{}
foreach ($exercise in $catalog) {
    $catalogById[[int]$exercise.id] = $exercise
}
$validCapacities = @('Balance', 'Strength', 'Stamina', 'Stepping', 'Mobility')
foreach ($addition in $additionAudit) {
    $exerciseId = [int]$addition.ID
    if (-not $catalogById.ContainsKey($exerciseId)) {
        throw "Catalog addition $exerciseId is absent from the runtime catalog."
    }

    $exercise = $catalogById[$exerciseId]
    if ([string]$addition.Name -ne [string]$exercise.name) {
        throw "Catalog addition $exerciseId has a stale name."
    }

    $primaryCapacity = [string]$addition.'Primary capacity'
    $qualifyingCapacities = @(
        [string]$addition.'Qualifying capacities' -split ';' |
            ForEach-Object { $_.Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($primaryCapacity -notin $validCapacities -or
        $primaryCapacity -notin $qualifyingCapacities) {
        throw "Catalog addition $exerciseId has an invalid primary capacity."
    }

    $mappedPracticeNodes = @(
        $taxonomy.DagMappings[[string]$exercise.practice] |
            ForEach-Object { [string]$_ })
    if ([string]$addition.'DAG practice node' -notin $mappedPracticeNodes) {
        throw "Catalog addition $exerciseId does not match its runtime practice mapping."
    }
}

$nodeById = @{}
foreach ($node in $graph.nodes) {
    $nodeById[[string]$node.id] = $node
}

$primaryParents = @{}
$allParents = @{}
foreach ($edge in $graph.edges) {
    if ([string]$edge.relation -eq 'contains') {
        $primaryParents[[string]$edge.target] = @(
            @($primaryParents[[string]$edge.target]) + [string]$edge.source |
                Sort-Object -Unique)
    }
    if ([string]$edge.relation -in @('contains', 'cross-link')) {
        $allParents[[string]$edge.target] = @(
            @($allParents[[string]$edge.target]) + [string]$edge.source |
                Sort-Object -Unique)
    }
}

function Get-AncestorIds {
    param(
        [string[]]$StartIds,
        [hashtable]$ParentMap
    )

    $seen = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $pending = [Collections.Generic.Queue[string]]::new()
    foreach ($startId in $StartIds) {
        if (-not [string]::IsNullOrWhiteSpace($startId)) {
            $pending.Enqueue($startId)
        }
    }

    while ($pending.Count -gt 0) {
        $nodeId = $pending.Dequeue()
        if (-not $seen.Add($nodeId)) {
            continue
        }
        foreach ($parentId in @($ParentMap[$nodeId])) {
            $pending.Enqueue([string]$parentId)
        }
    }

    return @($seen)
}

$unknownPracticeLabels = @(
    $catalog.practice |
        Sort-Object -Unique |
        Where-Object { -not $taxonomy.DagMappings.ContainsKey([string]$_) })
if ($unknownPracticeLabels.Count -gt 0) {
    throw "Catalog practice labels lack DAG mappings: $($unknownPracticeLabels -join ', ')."
}

$mappedNodeIds = @(
    $taxonomy.DagMappings.Values |
        ForEach-Object { $_ } |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        Sort-Object -Unique)
$unknownNodeIds = @($mappedNodeIds | Where-Object { -not $nodeById.ContainsKey($_) })
if ($unknownNodeIds.Count -gt 0) {
    throw "Practice mappings reference unknown DAG nodes: $($unknownNodeIds -join ', ')."
}

$auditRecords = @(
    foreach ($exercise in $catalog) {
        $directNodeIds = @(
            $taxonomy.DagMappings[[string]$exercise.practice] |
                ForEach-Object { [string]$_ } |
                Sort-Object -Unique)
        $primaryAncestorIds = Get-AncestorIds `
            -StartIds $directNodeIds `
            -ParentMap $primaryParents
        $allAncestorIds = Get-AncestorIds `
            -StartIds $directNodeIds `
            -ParentMap $allParents
        $primaryDomainIds = @($primaryAncestorIds | Where-Object {
                $nodeById.ContainsKey($_) -and
                [string]$nodeById[$_].kind -eq 'domain'
            } | Sort-Object -Unique)
        $allDomainIds = @($allAncestorIds | Where-Object {
                $nodeById.ContainsKey($_) -and
                [string]$nodeById[$_].kind -eq 'domain'
            } | Sort-Object -Unique)
        $primaryFamilyIds = @($primaryAncestorIds | Where-Object {
                $nodeById.ContainsKey($_) -and
                [string]$nodeById[$_].kind -eq 'family'
            } | Sort-Object -Unique)
        $allFamilyIds = @($allAncestorIds | Where-Object {
                $nodeById.ContainsKey($_) -and
                [string]$nodeById[$_].kind -eq 'family'
            } | Sort-Object -Unique)
        $directKinds = @($directNodeIds | ForEach-Object {
                [string]$nodeById[$_].kind
            })
        $specificity = if ($directNodeIds.Count -eq 0) {
            'Unmapped'
        }
        elseif ('practice' -in $directKinds) {
            'Practice'
        }
        elseif ('family' -in $directKinds) {
            'Family'
        }
        else {
            'Domain'
        }

        [pscustomobject]@{
            Id = [int]$exercise.id
            Name = [string]$exercise.name
            CatalogPractice = [string]$exercise.practice
            DirectNodeIds = $directNodeIds
            PrimaryDomainIds = $primaryDomainIds
            AllDomainIds = $allDomainIds
            PrimaryFamilyIds = $primaryFamilyIds
            AllFamilyIds = $allFamilyIds
            Specificity = $specificity
            IsAddition = [int]$exercise.id -in $additionIds
        }
    })

$csvPath = Join-Path $OutputDirectory 'practice_coverage_audit.csv'
$auditRecords |
    Sort-Object Id |
    Select-Object `
        @{ Name = 'ID'; Expression = { $_.Id } },
        Name,
        @{ Name = 'Catalog practice'; Expression = { $_.CatalogPractice } },
        @{ Name = 'Direct DAG nodes'; Expression = { $_.DirectNodeIds -join ';' } },
        @{ Name = 'Mapping specificity'; Expression = { $_.Specificity } },
        @{ Name = 'Primary domains'; Expression = { $_.PrimaryDomainIds -join ';' } },
        @{ Name = 'All-path domains'; Expression = { $_.AllDomainIds -join ';' } },
        @{ Name = 'Primary families'; Expression = { $_.PrimaryFamilyIds -join ';' } },
        @{ Name = 'All-path families'; Expression = { $_.AllFamilyIds -join ';' } },
        @{ Name = 'Added in this pass'; Expression = { $_.IsAddition } } |
    Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8

$domainNodes = @($graph.nodes | Where-Object kind -eq 'domain' | Sort-Object label)
$familyNodes = @($graph.nodes | Where-Object kind -eq 'family' | Sort-Object label)
$baselineRecords = @($auditRecords | Where-Object { -not $_.IsAddition })
$domainCoverage = @(
    foreach ($node in $domainNodes) {
        [pscustomobject]@{
            Id = [string]$node.id
            Label = [string]$node.label
            BaselinePrimary = @($baselineRecords | Where-Object {
                    [string]$node.id -in $_.PrimaryDomainIds }).Count
            CurrentPrimary = @($auditRecords | Where-Object {
                    [string]$node.id -in $_.PrimaryDomainIds }).Count
            CurrentAllPaths = @($auditRecords | Where-Object {
                    [string]$node.id -in $_.AllDomainIds }).Count
        }
    })
$familyCoverage = @(
    foreach ($node in $familyNodes) {
        [pscustomobject]@{
            Id = [string]$node.id
            Label = [string]$node.label
            BaselineAllPaths = @($baselineRecords | Where-Object {
                    [string]$node.id -in $_.AllFamilyIds }).Count
            CurrentAllPaths = @($auditRecords | Where-Object {
                    [string]$node.id -in $_.AllFamilyIds }).Count
        }
    })

$specificityCounts = @{}
foreach ($specificity in 'Practice', 'Family', 'Domain', 'Unmapped') {
    $specificityCounts[$specificity] = @(
        $auditRecords | Where-Object Specificity -eq $specificity).Count
}
$practiceCounts = @(
    $catalog |
        Group-Object practice |
        Sort-Object -Property @(
            @{ Expression = 'Count'; Descending = $true },
            @{ Expression = 'Name'; Descending = $false }))
$coveredPrimaryDomains = @($domainCoverage | Where-Object CurrentPrimary -gt 0).Count
$coveredAllDomains = @($domainCoverage | Where-Object CurrentAllPaths -gt 0).Count
$coveredFamilies = @($familyCoverage | Where-Object CurrentAllPaths -gt 0).Count
$newlyCoveredFamilies = @($familyCoverage | Where-Object {
        $_.BaselineAllPaths -eq 0 -and $_.CurrentAllPaths -gt 0 })

$baselineStrong = [ordered]@{
    Balance = 34
    Strength = 61
    Stamina = 8
    Stepping = 12
    Mobility = 31
}
$currentStrong = [ordered]@{}
foreach ($capacity in $baselineStrong.Keys) {
    $newPrimaryCount = @(
        $additionAudit | Where-Object { $_.'Primary capacity' -eq $capacity }).Count
    $currentStrong[$capacity] = [int]$baselineStrong[$capacity] + $newPrimaryCount
}

$markdown = [Collections.Generic.List[string]]::new()
$markdown.Add('# Nomadic Method movement-practice coverage audit')
$markdown.Add('')
$markdown.Add('Generated from the runtime catalog and the supplementary movement-practices DAG. The DAG is used for provenance, discovery, and diversity review; Nomadic Method capacities remain the scheduling taxonomy.')
$markdown.Add('')
$markdown.Add('## Outcome')
$markdown.Add('')
$markdown.Add("- Runtime catalog: **$($catalog.Count)** exercises. The frozen supplementary practice-coverage delta below contains **$($additionIds.Count)** additions.")
$markdown.Add("- Exact practice-node provenance: **$($specificityCounts.Practice)**; family-only: **$($specificityCounts.Family)**; domain-only: **$($specificityCounts.Domain)**; intentionally unmapped: **$($specificityCounts.Unmapped)**.")
$markdown.Add("- Primary DAG coverage: **$coveredPrimaryDomains/$($domainNodes.Count)** domains. Including honest cross-links: **$coveredAllDomains/$($domainNodes.Count)** domains and **$coveredFamilies/$($familyNodes.Count)** families.")
$markdown.Add("- Newly represented families: **$($newlyCoveredFamilies.Count)** — $((@($newlyCoveredFamilies.Label) -join ', ')).")
$markdown.Add('- All additions passed the standing, feet-only, zero-equipment, shoe-agnostic, 2 m × 2 m, quiet, non-jumping, bilateral/alternating, and exact-human-media rules.')
$markdown.Add('')
$markdown.Add('## Added from weak places')
$markdown.Add('')
$markdown.Add('| Exercise | Primary capacity | Practice branch | Why it survived review |')
$markdown.Add('|---|---|---|---|')
foreach ($addition in $additionAudit) {
    $markdown.Add("| $($addition.Name) | $($addition.'Primary capacity') | ``$($addition.'DAG practice node')`` | $($addition.'Why added') |")
}
$markdown.Add('')
$markdown.Add('## Strong primary-capacity pools')
$markdown.Add('')
$markdown.Add('The frozen 236-record capacity audit remains the baseline. Every addition above is a high-confidence Keep with a clear primary stimulus, so it qualifies as a strong representative.')
$markdown.Add('')
$markdown.Add('| Capacity | Frozen baseline | Added primary exercises | Current strong pool | Minimum met |')
$markdown.Add('|---|---:|---:|---:|:---:|')
foreach ($capacity in $baselineStrong.Keys) {
    $addedCount = [int]$currentStrong[$capacity] - [int]$baselineStrong[$capacity]
    $minimumMet = if ([int]$currentStrong[$capacity] -ge 10) { 'Yes' } else { 'No' }
    $markdown.Add("| $capacity | $($baselineStrong[$capacity]) | $addedCount | $($currentStrong[$capacity]) | $minimumMet |")
}
$markdown.Add('')
$markdown.Add('## Practice-label concentration')
$markdown.Add('')
$markdown.Add('| Catalog practice | Exercises |')
$markdown.Add('|---|---:|')
foreach ($group in $practiceCounts) {
    $markdown.Add("| $($group.Name) | $($group.Count) |")
}
$markdown.Add('')
$markdown.Add('## Domain coverage')
$markdown.Add('')
$markdown.Add('| DAG domain | Baseline primary | Current primary | Current all paths |')
$markdown.Add('|---|---:|---:|---:|')
foreach ($coverage in $domainCoverage) {
    $markdown.Add("| $($coverage.Label) | $($coverage.BaselinePrimary) | $($coverage.CurrentPrimary) | $($coverage.CurrentAllPaths) |")
}
$markdown.Add('')
$markdown.Add('## Editorial conclusions')
$markdown.Add('')
$markdown.Add('- The largest remaining weakness is not raw catalog size but the nearly half of legacy records whose labels still collapse into generic fitness or mobility buckets. Recovering their true lineage requires source-by-source verification, not name guessing.')
$markdown.Add('- Aquatic, animal-partnered, digital, weapon, grappling, climbing, circus/object, and most occupational/outdoor branches remain deliberately empty because their defining stimulus depends on water, equipment, partners, floor/hand contact, impact, or more space.')
$markdown.Add('- Dry-land swimming pantomimes, generic arm circles, long-court ghosting, rail-assisted gait footage, hidden-feet demonstrations, and ambiguous one-sided martial footage were reviewed and rejected in this pass.')
$markdown.Add('- The next defensible discovery targets are Western somatics, actor movement training, compact natural movement, and additional gait-retraining patterns—but only when exact full-body human footage survives the same constraints.')
$markdown.Add('')
$markdown.Add('## Method')
$markdown.Add('')
$markdown.Add('- `contains` edges determine primary ancestry. `contains` plus `cross-link` edges determine all-path coverage. Characterization/classification edges are excluded.')
$markdown.Add('- A practice-level mapping names a concrete DAG practice; family/domain mappings are explicitly reported as less specific. The four Naruto hand-seal records remain unmapped rather than being falsely assigned to ninja obstacle training.')
$markdown.Add('- The row-level mapping is in [`practice_coverage_audit.csv`](practice_coverage_audit.csv); the reviewed catalog delta is in [`catalog_additions_2026-08-02.csv`](catalog_additions_2026-08-02.csv).')

$markdownPath = Join-Path $OutputDirectory 'PRACTICE_COVERAGE_AUDIT.md'
$markdown | Set-Content -LiteralPath $markdownPath -Encoding utf8

Write-Output "Markdown: $markdownPath"
Write-Output "CSV: $csvPath"
Write-Output "Records: $($auditRecords.Count)"
