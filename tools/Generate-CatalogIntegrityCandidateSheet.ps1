param(
    [Parameter(Mandatory = $true)]
    [int[]] $Ids,

    [string] $OutputDirectory = (Join-Path $env:TEMP 'nomadic-method-catalog-integrity-candidates')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $root 'NomadicMethod/Assets/exercises.json'
$videoRoot = Join-Path $root 'NomadicMethod/Assets'
$catalog = Get-Content -Raw -LiteralPath $catalogPath | ConvertFrom-Json

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

foreach ($id in $Ids) {
    $exercise = $catalog | Where-Object { $_.id -eq $id } | Select-Object -First 1
    if ($null -eq $exercise) {
        throw "Exercise $id is not in the runtime catalog."
    }

    $videoPath = Join-Path $videoRoot ($exercise.video -replace '/', '\')
    if (-not (Test-Path -LiteralPath $videoPath)) {
        throw "Packaged video missing for exercise ${id}: $videoPath"
    }

    $durationText = & ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 $videoPath
    $duration = 0.0
    if ($LASTEXITCODE -ne 0 -or -not [double]::TryParse(
            $durationText,
            [Globalization.NumberStyles]::Float,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref] $duration)) {
        throw "ffprobe failed while reading exercise $id."
    }

    $outputPath = Join-Path $OutputDirectory ("exercise_{0:D4}.png" -f $id)
    $sampleRate = (25.0 / $duration).ToString('0.########', [Globalization.CultureInfo]::InvariantCulture)
    $filter = "fps=$sampleRate,scale=256:256:force_original_aspect_ratio=decrease,pad=256:256:(ow-iw)/2:(oh-ih)/2:white,tile=5x5"
    & ffmpeg -hide_banner -loglevel error -y -i $videoPath -vf $filter -frames:v 1 $outputPath
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg failed while rendering exercise $id."
    }
}

Write-Output $OutputDirectory
