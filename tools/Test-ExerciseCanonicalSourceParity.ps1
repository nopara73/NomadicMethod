$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseCanonicalSourceParity.ps1')

$catalogPath = Join-Path $PSScriptRoot '../NomadicMethod/Assets/exercises.json'
$catalog = @(Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json)
$assignments = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'ExerciseCanonicalGroups.psd1') -SkipLimitCheck
$replacements = Import-PowerShellDataFile -LiteralPath (
    Join-Path $PSScriptRoot 'CatalogExerciseReplacements.psd1') -SkipLimitCheck
Assert-ExerciseCanonicalSourceParity -Exercises $catalog -Assignments $assignments -Replacements $replacements

# A runtime-only correction used to pass the secondary-claim audit while leaving
# replacement definitions too stale to regenerate any exercise. Cover both sources.
$exercise = [pscustomobject]@{
    id = 1
    primaryCanonicalGroup = 'Chest'
    secondaryCanonicalGroups = @('ElbowExtensors')
}
$valid = @{ 1 = @{ Primary = 'Chest'; Secondary = @('ElbowExtensors') } }
Assert-ExerciseCanonicalSourceParity -Exercises @($exercise) -Assignments $valid -Replacements $valid
foreach ($fixture in @(
    @{ Assignments = @{ 1 = @{ Primary = 'AbdominalWall'; Secondary = @('ElbowExtensors') } }; Replacements = $valid },
    @{ Assignments = @{ 1 = @{ Primary = 'Chest'; Secondary = @() } }; Replacements = $valid },
    @{ Assignments = $valid; Replacements = @{ 1 = @{ Primary = 'Chest'; Secondary = @() } } },
    @{ Assignments = $valid; Replacements = @{ 2 = @{ Primary = 'Chest'; Secondary = @() } } },
    @{ Assignments = @{}; Replacements = @{} }
)) {
    $rejected = $false
    try {
        Assert-ExerciseCanonicalSourceParity -Exercises @($exercise) -Assignments $fixture.Assignments -Replacements $fixture.Replacements
    }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'A stale canonical generation source was accepted.' }
}
Write-Output "Canonical sources valid: $($catalog.Count) exercises, $($replacements.Count) replacements, five drift regressions rejected."
