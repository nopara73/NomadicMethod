param([string]$CatalogPath = (Join-Path $PSScriptRoot '../NomadicMethod/Assets/exercises.json'))
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseElevationRequirements.ps1')
$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$review = Get-ExerciseElevationReview -RetainedExerciseIds @($catalog.id)
foreach ($exercise in $catalog) {
    if ($exercise.elevationRequired -cne ([int]$exercise.id -in $review.Required) -or
        $exercise.jumpPlatformRequired -cne ([int]$exercise.id -in $review.JumpPlatformRequired)) {
        throw "Exercise $($exercise.id) differs from its elevation review."
    }
}
$inventory = Assert-ExerciseElevationInventory $catalog
Write-Output "Elevation audited: $($inventory.Support) support, $($inventory.Platform) platform, $($inventory.FloorAndElevation) requiring Floor and Elevation."
