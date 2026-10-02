$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = Split-Path $PSScriptRoot -Parent
$toolsDir = Join-Path $projectRoot 'tools'
$cacheDir = Join-Path $projectRoot '.downloads'
New-Item -ItemType Directory -Force -Path $toolsDir,$cacheDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-ReleaseAsset($repo, $name) {
    $release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
    $asset = $release.assets | Where-Object name -eq $name | Select-Object -First 1
    if (-not $asset) { throw "Missing release asset: $repo / $name" }
    $target = Join-Path $cacheDir $name
    & curl.exe -fL --retry 2 --connect-timeout 20 --max-time 600 --silent --show-error -o $target $asset.browser_download_url
    if ($LASTEXITCODE -ne 0) { throw "Download failed: $name" }
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($asset.digest -and $asset.digest -ne "sha256:$hash") { throw "Checksum mismatch: $name" }
    "$name | $hash | $($asset.browser_download_url)" | Add-Content -LiteralPath (Join-Path $toolsDir 'sources.txt')
    Write-Output "Downloaded and checked: $name"
    return $target
}
function Extract-Tools($zipPath, $names) {
    $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($name in $names) {
            $entry = $archive.Entries | Where-Object Name -eq $name | Select-Object -First 1
            if (-not $entry) { throw "Missing $name in archive" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $toolsDir $name), $true)
        }
    } finally { $archive.Dispose() }
}
$yt = @(Get-ReleaseAsset 'yt-dlp/yt-dlp' 'yt-dlp.exe')
Copy-Item -LiteralPath $yt[-1] -Destination (Join-Path $toolsDir 'yt-dlp.exe') -Force
Write-Output $yt[0]
$deno = @(Get-ReleaseAsset 'denoland/deno' 'deno-x86_64-pc-windows-msvc.zip')
Extract-Tools $deno[-1] @('deno.exe')
Write-Output $deno[0]
$ff = @(Get-ReleaseAsset 'yt-dlp/FFmpeg-Builds' 'ffmpeg-master-latest-win64-gpl.zip')
Extract-Tools $ff[-1] @('ffmpeg.exe','ffprobe.exe','LICENSE.txt')
Write-Output $ff[0]
& (Join-Path $toolsDir 'yt-dlp.exe') --version
& (Join-Path $toolsDir 'deno.exe') --version
& (Join-Path $toolsDir 'ffmpeg.exe') -version | Select-Object -First 1
