function Get-ExerciseLocomotionReview {
    param(
        [Parameter(Mandatory)][int[]]$RetainedExerciseIds,
        [hashtable]$SessionMovementByExerciseId = @{},
        [string]$ToolsRoot = $PSScriptRoot
    )
    $review = Import-PowerShellDataFile -LiteralPath (
        Join-Path $ToolsRoot 'ExerciseLocomotionSpaces.psd1') -SkipLimitCheck
    if (@(Compare-Object @('None', 'Room', 'Field') @($review.Keys)).Count) {
        throw 'Locomotion review must define exactly None, Room and Field.'
    }
    $byId = @{}
    $byMovement = @{}
    foreach ($space in @('None', 'Room', 'Field')) {
        $movements = [Collections.Generic.HashSet[int]]::new()
        foreach ($id in $review[$space]) {
            if ($id -isnot [int] -or $id -notin $RetainedExerciseIds -or $byId.ContainsKey($id)) {
                throw "Invalid or repeated locomotion review for exercise $id."
            }
            $byId[$id] = $space
            $movementId = if ($SessionMovementByExerciseId.ContainsKey($id)) {
                $SessionMovementByExerciseId[$id]
            } else { $id }
            if ($byMovement.ContainsKey($movementId) -and $byMovement[$movementId] -ne $space) {
                throw "Aliases of session movement $movementId disagree about travel space."
            }
            $byMovement[$movementId] = $space
            $null = $movements.Add($movementId)
        }
        $minimum = switch ($space) { Room { 25 }; Field { 5 }; default { 0 } }
        if ($movements.Count -lt $minimum) {
            throw "$space locomotion requires at least $minimum distinct approved session movements; found $($movements.Count)."
        }
    }
    if ($byId.Count -ne $RetainedExerciseIds.Count) {
        throw 'Every retained exercise needs exactly one explicit locomotion review.'
    }
    return $byId
}
