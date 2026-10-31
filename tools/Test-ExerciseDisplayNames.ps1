param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseCatalogDefinitions.ps1')
. (Join-Path $PSScriptRoot 'ExerciseDisplayNames.ps1')
$bilateral = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'BilateralExerciseNames.psd1') -SkipLimitCheck
$replacements = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'CatalogExerciseReplacements.psd1') -SkipLimitCheck
$catalog = @{}
Get-Content -LiteralPath (Join-Path $RepositoryRoot 'NomadicMethod\Assets\exercises.json') -Raw |
    ConvertFrom-Json | ForEach-Object { $catalog[[int]$_.id] = $_ }
$checked = 0
foreach ($definition in Get-ExerciseCatalogDefinitions) {
    $id = [int]$definition.Id
    if (-not $catalog.ContainsKey($id) -or -not $script:ReviewedExerciseDisplayNames.ContainsKey($id)) { continue }
    $sourceName = if ($replacements.ContainsKey($id)) { [string]$replacements[$id].Name }
        elseif ($bilateral.ContainsKey($id)) { [string]$bilateral[$id] }
        else { [string]$definition.Name }
    if ($catalog[$id].sideSequence -in @('ScreenLeftThenRight', 'ScreenRightThenLeft') -and
        $sourceName.StartsWith('Alternating ', [StringComparison]::Ordinal)) {
        $sourceName = $sourceName.Substring(12)
    }
    if ((Get-ExerciseDisplayName -ExerciseId $id -SourceName $sourceName) -cne $catalog[$id].name) {
        throw "The generator would change exercise $id's reviewed display name."
    }
    $checked++
}
if ($checked -ne $script:ReviewedExerciseDisplayNames.Count) { throw 'A reviewed exercise is missing from the generator.' }
$rejected = $false
try { $null = Get-ExerciseDisplayName -ExerciseId 1255 -SourceName 'A different movement' }
catch { $rejected = $true }
if (-not $rejected) { throw 'Changed source movements must require a new naming review.' }
Write-Output "Generator preserves $checked reviewed display names and rejects changed sources."
