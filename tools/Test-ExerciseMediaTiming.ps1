param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExerciseSourceDownload.ps1')

# Exercise the production encoders, using a generated moving test pattern.
# Parsing only these function declarations avoids generating the real catalog.
$generatorPath = Join-Path $PSScriptRoot 'Generate-ExerciseCatalog.ps1'
$tokens = $null
$parseErrors = $null
$syntax = [Management.Automation.Language.Parser]::ParseFile(
    $generatorPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) { throw 'The catalog generator has syntax errors.' }
foreach ($name in @('Publish-GeneratedFile', 'New-ExternalExerciseGif', 'New-ExerciseMp4')) {
    $declaration = $syntax.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -eq $name
    }, $false)
    if ($null -eq $declaration) { throw "Missing encoder function: $name" }
    . ([scriptblock]::Create($declaration.Extent.Text))
}

$testKey = 'nomadic-method-media-timing-' + [Guid]::NewGuid().ToString('N')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) $testKey
$sourceRoot = Join-Path ([IO.Path]::GetTempPath()) 'NomadicMethodExerciseSourceCache'
$sourceName = $testKey + '.mp4'
$sourcePath = Join-Path $sourceRoot $sourceName
$animatedSourceName = $testKey + '.gif'
$animatedSourcePath = Join-Path $sourceRoot $animatedSourceName
New-Item -ItemType Directory -Force -Path $testRoot, $sourceRoot | Out-Null
try {
    & ffmpeg -hide_banner -loglevel error -y -f lavfi `
        -i 'testsrc2=size=256x256:rate=24:duration=3' -an `
        -c:v libx264 -pix_fmt yuv420p $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create timing-test input.' }

    $cases = @(
        @{ Fps = 8; Side = 'Continuous'; Seconds = 3.0 },
        @{ Fps = 12; Side = 'Continuous'; Seconds = 3.0 },
        @{ Fps = 10; Side = 'Continuous'; Seconds = 3.0 },
        @{ Fps = 0; Side = 'Continuous'; Seconds = 3.0 },
        @{ Fps = 12; Side = 'Alternating'; Mirror = $true; Seconds = 6.0 },
        @{ Fps = 8; Side = 'ScreenLeftThenRight'; Mirror = $true; Seconds = 3.0 },
        @{ Fps = 12; Side = 'Continuous'; PingPong = $true; Seconds = 70.0 / 12.0 }
    )
    $id = 0
    foreach ($case in $cases) {
        $id++
        $media = @{
            File = $sourceName
            Video = $true
            StartSeconds = 0
            DurationSeconds = 3
            MirrorForAlternation = [bool]$case.Mirror
            PingPong = [bool]$case.PingPong
        }
        if ($case.Fps -gt 0) { $media.FramesPerSecond = [int]$case.Fps }
        $gif = Join-Path $testRoot "$id.gif"
        $mp4 = Join-Path $testRoot "$id.mp4"
        New-ExternalExerciseGif -ExerciseId $id -ExerciseName 'Timing test pattern' `
            -SideSequence $case.Side -Media $media -GifPath $gif -WorkingRoot $testRoot
        New-ExerciseMp4 -GifPath $gif -VideoPath $mp4
        $delays = @(& magick identify -format "%T`n" $gif)
        if ($LASTEXITCODE -ne 0) { throw "Could not read GIF timing for case $id." }
        $gifSeconds = ($delays | ForEach-Object { [int]$_ } | Measure-Object -Sum).Sum / 100.0
        $probe = (& ffprobe -v error -show_entries format=duration -of json $mp4 | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0) { throw "Could not read MP4 timing for case $id." }
        $mp4Seconds = [double]::Parse($probe.format.duration, [Globalization.CultureInfo]::InvariantCulture)
        if ([Math]::Abs($gifSeconds - $case.Seconds) -gt 0.0051) {
            throw "Case $id changed source cadence: expected $($case.Seconds) seconds, GIF $gifSeconds."
        }
        # The deployable format is 20 fps, so one output frame bounds rounding.
        if ([Math]::Abs($mp4Seconds - $case.Seconds) -gt 0.0501) {
            throw "Case $id changed source cadence: expected $($case.Seconds) seconds, MP4 $mp4Seconds."
        }
        Write-Output "Timing case $id passed: GIF $gifSeconds s, MP4 $mp4Seconds s."
    }

    & magick -size 80x60 '(' -delay 6 xc:red ')' `
        '(' -delay 13 xc:green ')' '(' -delay 25 xc:blue ')' `
        -loop 0 $animatedSourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create animated timing-test input.' }
    $animatedCases = @(
        @{ Side = 'Continuous'; Delays = @(6, 13, 25) },
        @{ Side = 'Alternating'; Mirror = $true; Delays = @(6, 13, 25, 6, 13, 25) },
        @{ Side = 'ScreenLeftThenRight'; Mirror = $true; Delays = @(6, 13, 25) },
        @{ Side = 'Continuous'; PingPong = $true; Delays = @(6, 13, 25, 13) },
        @{ Side = 'Alternating'; Mirror = $true; PingPong = $true;
            Delays = @(6, 13, 25, 6, 13, 25, 13, 6, 25, 13) },
        @{ Side = 'Continuous'; Override = 10; Delays = @(10, 10, 10) }
    )
    foreach ($case in $animatedCases) {
        $id++
        $media = @{
            File = $animatedSourceName
            MirrorForAlternation = [bool]$case.Mirror
            PingPong = [bool]$case.PingPong
        }
        if ($case.Override) { $media.DelayCentiseconds = [int]$case.Override }
        $gif = Join-Path $testRoot "$id.gif"
        New-ExternalExerciseGif -ExerciseId $id -ExerciseName 'Animated timing test' `
            -SideSequence $case.Side -Media $media -GifPath $gif -WorkingRoot $testRoot
        $actual = @(& magick identify -format "%T`n" $gif)
        if ($LASTEXITCODE -ne 0 -or ($actual -join ',') -ne ($case.Delays -join ',')) {
            throw "Animated case $id lost per-frame timing: $($actual -join ',')."
        }
        Write-Output "Animated timing case $id passed: $($actual -join ',') cs."
    }
}
finally {
    Remove-Item -LiteralPath $sourcePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $animatedSourcePath -Force -ErrorAction SilentlyContinue
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $testKey))
    if ($resolvedRoot -ne $expectedRoot -or
        [IO.Path]::GetFileName($resolvedRoot) -ne $testKey) {
        throw 'Refusing to clean an unexpected timing-test directory.'
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
