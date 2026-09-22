$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseSourceDownload.ps1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('NomadicMethodSourceVideoTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
function Assert-Rejected {
    param([scriptblock]$Action, [string]$Message)
    $rejected = $false
    try { & $Action }
    catch { $rejected = $true }
    if (-not $rejected) { throw $Message }
}
try {
    $source = Join-Path $testRoot 'complete.mp4'
    & ffmpeg -hide_banner -loglevel error -y -f lavfi `
        -i 'testsrc2=size=256x256:rate=25:duration=2' -an `
        -c:v libx264 -pix_fmt yuv420p -movflags +faststart $source
    if ($LASTEXITCODE -ne 0) { throw 'Could not create source validation test footage.' }
    $valid = @{ SourceWidth = 256; SourceHeight = 256; StartSeconds = 0; DurationSeconds = 2 }
    Assert-ExerciseSourceVideo -Path $source -Media $valid -RequireReviewedGeometry
    Assert-ExerciseSourceVideo -Path $source -Media @{ StartSeconds = 0.2; DurationSeconds = 0.8 }
    Assert-Rejected { Assert-ExerciseSourceVideo -Path $source -Media @{ DurationSeconds = 2 } -RequireReviewedGeometry } 'An unreviewed source geometry was accepted.'
    Assert-Rejected { Assert-ExerciseSourceVideo -Path $source -Media @{ SourceWidth = 640; SourceHeight = 360; DurationSeconds = 2 } } 'A changed source resolution was accepted for an existing crop.'
    foreach ($invalid in @(
        @{ StartSeconds = 1.5; DurationSeconds = 0.8 },
        @{ StartSeconds = -1; DurationSeconds = 1 },
        @{ StartSeconds = 0; DurationSeconds = 0 },
        @{ StartSeconds = 0; DurationSeconds = [double]::NaN }
    )) {
        Assert-Rejected { Assert-ExerciseSourceVideo -Path $source -Media $invalid } 'An incomplete or invalid trim was accepted.'
    }
    # Keep the early MP4 headers and frames, but remove the end of the actual
    # video. A first-frame-only decoder would incorrectly accept this file.
    $damaged = Join-Path $testRoot 'late-damage.mp4'
    $bytes = [IO.File]::ReadAllBytes($source)
    [IO.File]::WriteAllBytes($damaged, $bytes[0..([int]($bytes.Length * 0.7))])
    & ffmpeg -hide_banner -loglevel error -i $damaged -frames:v 1 -an -f null -
    if ($LASTEXITCODE -ne 0) { throw 'Damaged fixture must retain a decodable first frame.' }
    Assert-Rejected { Assert-ExerciseSourceVideo -Path $damaged -Media $valid } 'A damaged later part of the loop was accepted.'
    Write-Output 'Source geometry, complete-trim, and late-corruption validation passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'NomadicMethodSourceVideoTest-*') {
        throw 'Unexpected source test cleanup path.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
