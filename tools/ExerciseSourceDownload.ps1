function Get-YtDlpPath {
    $sourceCacheRoot = Join-Path ([IO.Path]::GetTempPath()) 'NomadicMethodExerciseSourceCache'
    New-Item -ItemType Directory -Force -Path $sourceCacheRoot | Out-Null
    $version = '2026.08.19'
    $expectedHash = '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a'
    $path = Join-Path $sourceCacheRoot "yt-dlp-$version.exe"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $download = $path + '.' + [Guid]::NewGuid().ToString('N') + '.download'
        try {
            Invoke-WebRequest -Uri "https://github.com/yt-dlp/yt-dlp/releases/download/$version/yt-dlp.exe" -OutFile $download
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
                throw 'The downloaded yt-dlp executable does not match the official pinned SHA-256.'
            }
            Move-Item -LiteralPath $download -Destination $path -Force
        }
        finally {
            if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force }
        }
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
        throw 'The cached yt-dlp executable changed; restore the pinned official release before downloading sources.'
    }
    return $path
}

function Assert-ExerciseSourceVideo {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Media,
        [switch]$RequireReviewedGeometry
    )
    $probeText = & ffprobe -v error -select_streams v:0 `
        -show_entries stream=width,height,duration,avg_frame_rate:format=duration -of json $Path
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect exercise source video: $Path" }
    $probe = ($probeText -join "`n") | ConvertFrom-Json
    if (@($probe.streams).Count -ne 1) { throw "Exercise source must contain a decodable video stream: $Path" }
    $stream = $probe.streams[0]
    $hasGeometry = $Media.Contains('SourceWidth') -and $Media.Contains('SourceHeight')
    if ($RequireReviewedGeometry -and -not $hasGeometry) {
        throw "Downloaded source geometry is unreviewed ($($stream.width)x$($stream.height)); review the native footage and pin SourceWidth/SourceHeight before generating a crop."
    }
    if ($hasGeometry -and ([int]$Media.SourceWidth -ne [int]$stream.width -or
            [int]$Media.SourceHeight -ne [int]$stream.height)) {
        throw "Exercise source geometry changed: expected $($Media.SourceWidth)x$($Media.SourceHeight), found $($stream.width)x$($stream.height). Review the crop against the new native footage."
    }
    $culture = [Globalization.CultureInfo]::InvariantCulture
    $start = if ($Media.Contains('StartSeconds')) { [double]$Media.StartSeconds } else { 0.0 }
    $duration = [double]$Media.DurationSeconds
    $sourceDurationText = if ($stream.duration -and $stream.duration -ne 'N/A') {
        [string]$stream.duration
    } else { [string]$probe.format.duration }
    $sourceDuration = 0.0
    if (-not [double]::TryParse($sourceDurationText, [Globalization.NumberStyles]::Float, $culture, [ref]$sourceDuration) -or
        -not [double]::IsFinite($sourceDuration) -or
        -not [double]::IsFinite($start) -or -not [double]::IsFinite($duration) -or
        $start -lt 0 -or $duration -le 0 -or $start + $duration -gt $sourceDuration + 0.05) {
        throw "The complete reviewed trim is not present in the source video: $Path"
    }
    # Decoding one frame does not detect damage later in a loop. Validate the
    # complete exact excerpt and fail rather than replacing its source silently.
    & ffmpeg -hide_banner -loglevel error -xerror -err_detect explode `
        -ss $start.ToString('0.###', $culture) -i $Path `
        -t $duration.ToString('0.###', $culture) -an -f null -
    if ($LASTEXITCODE -ne 0) { throw "The complete reviewed source trim is not decodable: $Path" }
}

function Save-YouTubeExerciseSource {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Media
    )
    $ytDlpPath = Get-YtDlpPath
    $null = Get-Command node -ErrorAction Stop
    $temporary = $Destination + '.' + [Guid]::NewGuid().ToString('N') + '.mp4'
    try {
        # Let the pinned extractor choose its current supported public clients.
        # A removed Android client or an arbitrary fallback resolution must not
        # silently change the geometry used by an existing pixel crop.
        $height = if ($Media.Contains('SourceHeight')) { [int]$Media.SourceHeight } else { 720 }
        $format = "bv*[height<=$height][ext=mp4]/b[height<=$height][ext=mp4]"
        & $ytDlpPath --js-runtimes node --no-playlist --no-progress `
            --retries 2 --fragment-retries 2 --retry-sleep 1 `
            --format $format --output $temporary $Url
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $temporary -PathType Leaf)) {
            throw "Could not download the exercise source: $Url"
        }
        Assert-ExerciseSourceVideo -Path $temporary -Media $Media -RequireReviewedGeometry
        Move-Item -LiteralPath $temporary -Destination $Destination -Force
    }
    finally {
        foreach ($candidate in @($temporary, ($temporary + '.part'), ($temporary + '.ytdl'))) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { Remove-Item -LiteralPath $candidate -Force }
        }
    }
}
