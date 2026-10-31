$namePolicyPath = Join-Path $PSScriptRoot '..\NomadicMethod\Assets\exercise_names.json'
$namePolicy = Get-Content -LiteralPath $namePolicyPath -Raw | ConvertFrom-Json
if ($namePolicy.version -ne 1) { throw 'Unsupported exercise-name policy.' }
$script:ReviewedExerciseDisplayNames = @{}
foreach ($entry in $namePolicy.names) {
    $id = [int]$entry.id
    if ($id -le 0 -or $script:ReviewedExerciseDisplayNames.ContainsKey($id) -or
        [string]::IsNullOrWhiteSpace([string]$entry.name) -or
        [string]::IsNullOrWhiteSpace([string]$entry.sourceName)) {
        throw "Invalid reviewed display name for exercise $id."
    }
    $script:ReviewedExerciseDisplayNames[$id] = $entry
}

function Get-ExerciseDisplayName {
    param([int]$ExerciseId, [string]$SourceName)
    if (-not $script:ReviewedExerciseDisplayNames.ContainsKey($ExerciseId)) { return $SourceName }
    $review = $script:ReviewedExerciseDisplayNames[$ExerciseId]
    if ($SourceName -cne [string]$review.sourceName) {
        throw "Re-review the display name for changed exercise $ExerciseId ('$SourceName')."
    }
    return [string]$review.name
}

function Get-ExerciseIdentityName {
    param([int]$ExerciseId, [string]$Name)
    if (-not $script:ReviewedExerciseDisplayNames.ContainsKey($ExerciseId)) { return $Name }
    $review = $script:ReviewedExerciseDisplayNames[$ExerciseId]
    if ($Name -ceq $review.name -or $Name -ceq $review.identityName -or
        $Name -cin $review.previousNames) { return [string]$review.identityName }
    return $Name
}

function Restore-ReviewedReactionLabels {
    param([string]$Text)
    foreach ($label in $namePolicy.reactionLabels) {
        $current = '"label": ' + (ConvertTo-Json -InputObject ([string]$label.name) -Compress)
        $previous = '"label": ' + (ConvertTo-Json -InputObject ([string]$label.identityName) -Compress)
        $Text = $Text.Replace($current, $previous)
    }
    return $Text
}
