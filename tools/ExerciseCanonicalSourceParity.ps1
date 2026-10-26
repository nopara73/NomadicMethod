function Assert-ExerciseCanonicalSourceParity {
    param(
        [object[]]$Exercises,
        [System.Collections.IDictionary]$Assignments,
        [System.Collections.IDictionary]$Replacements
    )

    $catalogIds = @($Exercises | ForEach-Object { [int]$_.id } | Sort-Object)
    $assignmentIds = @($Assignments.Keys | ForEach-Object { [int]$_ } | Sort-Object)
    if ($catalogIds.Count -ne @($catalogIds | Sort-Object -Unique).Count -or
        ($catalogIds -join ',') -cne ($assignmentIds -join ',')) {
        throw 'Canonical generation assignments must match the packaged exercise inventory exactly.'
    }

    foreach ($exercise in $Exercises) {
        $id = [int]$exercise.id
        $assignment = $Assignments[$id]
        if ([string]$assignment.Primary -cne [string]$exercise.primaryCanonicalGroup -or
            (@($assignment.Secondary | Sort-Object) -join ',') -cne
                (@($exercise.secondaryCanonicalGroups | Sort-Object) -join ',')) {
            throw "Exercise $id canonical generation assignments differ from the packaged catalog."
        }
    }

    foreach ($key in $Replacements.Keys) {
        $id = [int]$key
        $replacement = $Replacements[$key]
        $assignment = $Assignments[$id]
        if (-not $Assignments.Contains($id) -or
            [string]$replacement.Primary -cne [string]$assignment.Primary -or
            (@($replacement.Secondary | Sort-Object) -join ',') -cne
                (@($assignment.Secondary | Sort-Object) -join ',')) {
            throw "Replacement $id canonical generation assignments have drifted."
        }
    }
}
