param(
    [string]$CatalogPath = (Join-Path $PSScriptRoot '../NomadicMethod/Assets/exercises.json')
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseFloorContacts.ps1')
$catalog = @(Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json)
$contacts = Get-ExerciseFloorContactReview -ExpectedExerciseIds @($catalog.id)
foreach ($exercise in $catalog) {
    if (-not (Test-ExerciseFloorContactMetadata $exercise) -or
        $contacts[[int]$exercise.id] -cne [string]$exercise.floorContactPoints) {
        throw "Exercise $($exercise.id) has invalid or unreviewed floor contacts."
    }
}
$inventory = Assert-ExerciseFloorInventory $catalog
Write-Output "Floor contacts audited: $($catalog.Count) exercises, $($inventory.Floor) distinct floor movements, $($inventory.FloorAndWall) requiring both Floor and Wall, $($inventory.FloorAndLocomotion) requiring both Floor and locomotion."
