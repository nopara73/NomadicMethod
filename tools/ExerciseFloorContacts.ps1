function Get-ExerciseFloorContactReview {
    param(
        [string]$ToolsRoot = $PSScriptRoot,
        [int[]]$ExpectedExerciseIds
    )

    $review = Import-PowerShellDataFile -LiteralPath (
        Join-Path $ToolsRoot 'ExerciseFloorContacts.psd1') -SkipLimitCheck
    $known = @('Feet', 'Hands', 'Forearms', 'Elbows', 'Knees', 'Head',
        'Torso', 'Shoulders', 'Hips', 'Thighs', 'LowerLegs')
    $contacts = @{}
    foreach ($id in $review.FeetOnly) {
        if ($contacts.ContainsKey([int]$id)) {
            throw "Duplicate floor-contact review for exercise $id."
        }
        $contacts[[int]$id] = 'Feet'
    }
    foreach ($entry in $review.FloorRequired.GetEnumerator()) {
        $id = [int]$entry.Key
        $points = @($entry.Value)
        if ($contacts.ContainsKey($id) -or $points.Count -eq 0 -or
            @($points | Sort-Object -Unique).Count -ne $points.Count -or
            @($points | Where-Object { $_ -notin $known }).Count -gt 0 -or
            ($points.Count -eq 1 -and $points[0] -eq 'Feet')) {
            throw "Invalid floor-contact review for exercise $id."
        }
        $contacts[$id] = ($known | Where-Object { $_ -in $points }) -join ', '
    }
    if ($PSBoundParameters.ContainsKey('ExpectedExerciseIds') -and
        @(Compare-Object @($ExpectedExerciseIds | Sort-Object) `
            @($contacts.Keys | Sort-Object)).Count -gt 0) {
        throw 'Every retained exercise needs exactly one explicit floor-contact review.'
    }
    return $contacts
}

function Test-ExerciseFloorContactMetadata {
    param($Exercise)
    $known = @('Feet', 'Hands', 'Forearms', 'Elbows', 'Knees', 'Head',
        'Torso', 'Shoulders', 'Hips', 'Thighs', 'LowerLegs')
    $points = @(([string]$Exercise.floorContactPoints) -split ',\s*')
    return $points.Count -gt 0 -and
        @($points | Where-Object { $_ -notin $known }).Count -eq 0 -and
        [bool]$Exercise.onlyFeetTouchGround -eq (
            $points.Count -eq 1 -and $points[0] -eq 'Feet')
}

function Assert-ExerciseFloorContactConstraints {
    param($Exercise)
    if (-not (Test-ExerciseFloorContactMetadata $Exercise)) {
        throw "Exercise $($Exercise.id) has invalid or unreviewed floor contacts."
    }
    $points = @(([string]$Exercise.floorContactPoints) -split ',\s*')
    if (@($points | Where-Object { $_ -in @('Torso','Shoulders') }).Count -gt 0 -and
        $Exercise.upperBodyClothingRequirement -ne 'ClothingRequired') {
        throw "Exercise $($Exercise.id) needs upper-body clothing for its floor contacts."
    }
    if (@($points | Where-Object { $_ -in @('Knees','Elbows','Head','Torso','Shoulders') }).Count -gt 0 -and
        $Exercise.hardFloorCompatibility -ne 'Incompatible') {
        throw "Exercise $($Exercise.id) is incorrectly marked hard-floor compatible."
    }
}

function Get-ExerciseFloorInventory {
    param([object[]]$Exercises)
    $byId = @{}
    foreach ($exercise in $Exercises) { $byId[[int]$exercise.id] = $exercise }
    $floor = [Collections.Generic.HashSet[int]]::new()
    $floorAndWall = [Collections.Generic.HashSet[int]]::new()
    $floorAndLocomotion = [Collections.Generic.HashSet[int]]::new()
    foreach ($root in $Exercises) {
        $blocks = @($root.sequenceBlocks)
        if ($blocks.Count -eq 0) { continue }
        $members = @()
        $valid = $true
        foreach ($block in $blocks) {
            if (-not $byId.ContainsKey([int]$block.exerciseId)) { $valid = $false; break }
            $member = $byId[[int]$block.exerciseId]
            if (-not (Test-ExerciseFloorContactMetadata $member)) { $valid = $false; break }
            $members += $member
        }
        if (-not $valid) { continue }
        # Match the runtime's relaxed equipment/space profiles. One profile
        # must admit the complete sequence: bare-only and clothed-only members
        # can never be combined to inflate an inventory.
        $clothing = @($members.upperBodyClothingRequirement)
        if ('ClothingRequired' -in $clothing -and 'BareUpperBodyRequired' -in $clothing) { continue }
        $movementId = if ([int]$root.sessionMovementId -gt 0) { [int]$root.sessionMovementId } else { [int]$root.id }
        if (@($members | Where-Object { -not $_.onlyFeetTouchGround }).Count -gt 0) {
            [void]$floor.Add($movementId)
        }
        if (@($members | Where-Object { -not $_.onlyFeetTouchGround -and $_.wallRequired }).Count -gt 0) {
            [void]$floorAndWall.Add($movementId)
        }
        if (@($members | Where-Object {
            -not $_.onlyFeetTouchGround -and $_.locomotionSpace -in @('Room', 'Field')
        }).Count -gt 0) {
            [void]$floorAndLocomotion.Add($movementId)
        }
    }
    return @{ Floor = $floor.Count; FloorAndWall = $floorAndWall.Count
        FloorAndLocomotion = $floorAndLocomotion.Count }
}

function Assert-ExerciseFloorInventory {
    param([object[]]$Exercises)
    foreach ($exercise in $Exercises) { Assert-ExerciseFloorContactConstraints $exercise }
    $inventory = Get-ExerciseFloorInventory $Exercises
    if ($inventory.Floor -lt 201) {
        throw "At least 201 distinct floor session movements are required; found $($inventory.Floor)."
    }
    if ($inventory.FloorAndWall -lt 21) {
        throw "At least 21 distinct movements requiring both Floor and Wall are required; found $($inventory.FloorAndWall)."
    }
    if ($inventory.FloorAndLocomotion -lt 21) {
        throw "At least 21 distinct movements requiring both Floor and locomotion are required; found $($inventory.FloorAndLocomotion)."
    }
    return $inventory
}
