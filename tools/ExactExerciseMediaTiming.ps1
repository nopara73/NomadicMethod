function New-ExactExerciseGif {
    param(
        [Parameter(Mandatory)][string]$SourceGifPath,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Transform,
        [string]$Comment = 'Nomadic Method reviewed transformed exercise'
    )
    # Coalescing and cloning keep the image and its individual source delay
    # together. An intermediate PNG loses that timing metadata.
    $delays = @(& magick identify -format "%T`n" $SourceGifPath)
    if ($LASTEXITCODE -ne 0 -or $delays.Count -lt 2) {
        throw 'Exact transform source must be an animated GIF.'
    }
    foreach ($value in $delays) {
        $delay = 0
        if (-not [int]::TryParse([string]$value, [ref]$delay) -or $delay -lt 1) {
            throw 'Exact transform source has an invalid frame delay.'
        }
    }
    $indices = @(0..($delays.Count - 1))
    if ($Transform.ReverseFrames) { [Array]::Reverse($indices) }
    if ($Transform.ContainsKey('StartFramePercent')) {
        $percent = [double]$Transform.StartFramePercent
        if ($percent -lt 0 -or $percent -gt 100) {
            throw 'An exact transform start percentage must be between 0 and 100.'
        }
        $start = [int][Math]::Round(($indices.Count - 1) * $percent / 100.0)
        $indices = @(
            for ($index = 0; $index -lt $indices.Count; $index++) {
                $indices[($start + $index) % $indices.Count]
            }
        )
    }
    $arguments = @($SourceGifPath, '-coalesce', '(', '-clone',
        ($indices -join ','), ')', '-delete', ('0-' + ($delays.Count - 1)))
    if ($Transform.HorizontalMirror) { $arguments += '-flop' }
    if ($Transform.ContainsKey('DelayCentiseconds')) {
        $delay = [int]$Transform.DelayCentiseconds
        if ($delay -lt 1) { throw 'An explicit frame delay must be positive.' }
        $arguments += @('-set', 'delay', $delay.ToString())
    }
    $arguments += @('-set', 'dispose', 'background', '-set', 'comment', $Comment,
        '-loop', '0', '-layers', 'Optimize', $OutputPath)
    & magick @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $OutputPath)) {
        throw 'Could not generate the exact transformed GIF.'
    }
}
