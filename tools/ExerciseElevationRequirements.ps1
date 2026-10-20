function Get-ExerciseElevationReview {
    param([int[]]$RetainedExerciseIds)
    $review = Import-PowerShellDataFile -LiteralPath (
        Join-Path $PSScriptRoot 'ExerciseElevationRequirements.psd1')
    $required = @($review.Required | ForEach-Object { [int]$_ })
    $platform = @($review.JumpPlatformRequired | ForEach-Object { [int]$_ })
    if ($required.Count -ne @($required | Sort-Object -Unique).Count -or
        $platform.Count -ne @($platform | Sort-Object -Unique).Count -or
        @($required | Where-Object { $_ -notin $RetainedExerciseIds }).Count -gt 0 -or
        @($platform | Where-Object { $_ -notin $required }).Count -gt 0) {
        throw 'Elevation requirements must be unique retained IDs; platform requirements must also require elevation.'
    }
    return @{ Required = $required; JumpPlatformRequired = $platform }
}

function Assert-ExerciseElevationInventory {
    param([object[]]$Exercises)
    $support = [System.Collections.Generic.HashSet[int]]::new()
    $platform = [System.Collections.Generic.HashSet[int]]::new()
    $floor = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($exercise in $Exercises) {
        if ($exercise.elevationRequired -isnot [bool] -or
            $exercise.jumpPlatformRequired -isnot [bool] -or
            ($exercise.jumpPlatformRequired -and -not $exercise.elevationRequired)) {
            throw "Exercise $($exercise.id) has invalid elevation requirements."
        }
        if (-not $exercise.elevationRequired) { continue }
        $movementId = if ([int]$exercise.sessionMovementId -gt 0) {
            [int]$exercise.sessionMovementId
        } else { [int]$exercise.id }
        # A family alias cannot fill another pool using the same identity.
        if ($movementId -ne [int]$exercise.id) { continue }
        if (-not $exercise.onlyFeetTouchGround) { [void]$floor.Add($movementId) }
        elseif ($exercise.jumpPlatformRequired) { [void]$platform.Add($movementId) }
        else { [void]$support.Add($movementId) }
    }
    if ($support.Count -lt 20 -or $platform.Count -lt 5 -or $floor.Count -lt 5) {
        throw "Elevation needs 20 support, 5 platform and 5 floor movements; found $($support.Count), $($platform.Count), $($floor.Count)."
    }
    return @{ Support = $support.Count; Platform = $platform.Count; FloorAndElevation = $floor.Count }
}
