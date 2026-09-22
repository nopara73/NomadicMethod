$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReviewedSourceMedia.ps1')
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('NomadicMethodReviewedSourceTest-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $testRoot 'ReviewedSourceMedia') | Out-Null
try {
    $path = Join-Path $testRoot 'ReviewedSourceMedia/source.gif'
    [IO.File]::WriteAllBytes($path, [byte[]](71, 73, 70, 56, 57, 97))
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    $media = @{ LocalSourceFile = 'source.gif'; LocalSourceSha256 = $hash }
    if ((Resolve-ReviewedSourceMedia -Media $media -ToolsRoot $testRoot) -ne $path) {
        throw 'The hash-verified source did not resolve.'
    }
    foreach ($invalid in @(
        @{ LocalSourceFile = '../source.gif'; LocalSourceSha256 = $hash },
        @{ LocalSourceFile = $path; LocalSourceSha256 = $hash },
        @{ LocalSourceFile = 'missing.gif'; LocalSourceSha256 = $hash },
        @{ LocalSourceFile = 'source.gif'; LocalSourceSha256 = 'unverified' },
        @{ LocalSourceFile = 'source.gif'; LocalSourceSha256 = ('0' * 64) }
    )) {
        $rejected = $false
        try { $null = Resolve-ReviewedSourceMedia -Media $invalid -ToolsRoot $testRoot }
        catch { $rejected = $true }
        if (-not $rejected) { throw 'An invalid or changed source was accepted.' }
    }
    [IO.File]::WriteAllBytes($path, [byte[]](71, 73, 70, 56, 55, 97))
    $rejected = $false
    try { $null = Resolve-ReviewedSourceMedia -Media $media -ToolsRoot $testRoot }
    catch { $rejected = $true }
    if (-not $rejected) { throw 'Source mutation did not invalidate its pinned review.' }
    Write-Output 'Reviewed source media integrity checks passed.'
}
finally {
    # Delete only this explicitly created, resolved test directory.
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'NomadicMethodReviewedSourceTest-*') {
        throw 'Unexpected test cleanup path.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
