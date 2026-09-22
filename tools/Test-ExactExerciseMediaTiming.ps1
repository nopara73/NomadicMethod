$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ExactExerciseMediaTiming.ps1')
$key = 'nomadic-method-exact-timing-' + [guid]::NewGuid().ToString('N')
$root = Join-Path ([IO.Path]::GetTempPath()) $key
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $source = Join-Path $root 'source.gif'
    & magick '(' -size 12x12 xc:red -set delay 13 ')' `
        '(' -size 12x12 xc:green -set delay 12 ')' `
        '(' -size 12x12 xc:blue -set delay 13 ')' `
        '(' -size 12x12 xc:yellow -set delay 12 ')' -loop 0 $source
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the variable-cadence fixture.' }
    $colors = @(& magick $source -coalesce -format '%[hex:p{0,0}] ' info:)[0].Trim().Split(' ')
    $colorNames = @{}
    $names = @('red', 'green', 'blue', 'yellow')
    for ($index = 0; $index -lt 4; $index++) { $colorNames[$colors[$index]] = $names[$index] }
    $cases = @(
        @{ Transform = @{}; Expected = 'red:13,green:12,blue:13,yellow:12' },
        @{ Transform = @{ ReverseFrames = $true }; Expected = 'yellow:12,blue:13,green:12,red:13' },
        @{ Transform = @{ StartFramePercent = 50 }; Expected = 'blue:13,yellow:12,red:13,green:12' },
        @{ Transform = @{ ReverseFrames = $true; StartFramePercent = 50 }; Expected = 'green:12,red:13,yellow:12,blue:13' },
        @{ Transform = @{ ReverseFrames = $true; DelayCentiseconds = 10 }; Expected = 'yellow:10,blue:10,green:10,red:10' }
    )
    foreach ($case in $cases) {
        $output = Join-Path $root 'transformed.gif'
        New-ExactExerciseGif -SourceGifPath $source -OutputPath $output -Transform $case.Transform
        $frames = @(& magick $output -coalesce -format "%[hex:p{0,0}]:%T`n" info:)
        $actual = ($frames | ForEach-Object {
            $parts = $_.Split(':')
            $colorNames[$parts[0]] + ':' + $parts[1]
        }) -join ','
        if ($actual -cne $case.Expected) { throw "Frame/delay pairing changed: $actual" }
    }
    $rejected = $false
    try { New-ExactExerciseGif -SourceGifPath $source -OutputPath (Join-Path $root 'bad.gif') -Transform @{ DelayCentiseconds = 0 } }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'A zero-duration frame delay was accepted.' }
    Write-Output 'Exact timing passed: reverse, rotation, combined transforms, explicit cadence and invalid-delay rejection.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if ($resolved -ne [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $key)) -or
        [IO.Path]::GetFileName($resolved) -ne $key) { throw 'Unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
