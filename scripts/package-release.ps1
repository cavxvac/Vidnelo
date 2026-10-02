$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $projectRoot 'Vidnelo.exe')).ProductVersion
if ($version -notmatch '^\d+\.\d+(\.\d+)?$') { throw 'Invalid application version' }
$releaseDir = Join-Path $projectRoot 'releases'
$staging = Join-Path $projectRoot ('.build\package-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $staging 'Vidnelo'
New-Item -ItemType Directory -Force -Path $releaseDir,$package,(Join-Path $package 'scripts') | Out-Null
foreach ($name in @('Vidnelo.exe','Start Vidnelo.cmd','LICENSE','THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $package
}
foreach ($name in @('setup-tools.ps1','start.ps1')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "scripts\$name") -Destination (Join-Path $package 'scripts')
}
@'
VIDNELO - WINDOWS x64

FIRST START
1. Extract the entire ZIP to a writable folder, for example Documents\Vidnelo.
2. Double-click Start Vidnelo.cmd.
3. Wait for yt-dlp, FFmpeg and Deno to download from their official repositories.
4. Vidnelo opens when setup finishes.

Internet access is required for first-time setup and downloads.
Allow approximately 1 GB of free disk space for tools and download archives,
plus space for your media files. Requires Windows 10/11 x64 and .NET Framework 4.8.
After setup, you can open Vidnelo.exe directly. Keep the tools folder beside it.

USAGE
Paste a video URL, choose video or audio, select a format and quality,
choose a destination folder, and click Download. Use the globe icon for language
selection. The app supports 15 languages. Use the moon/sun icon to change theme.

UPDATES
The update button opens separate options for Vidnelo and yt-dlp.
App updates open the release page and require replacing Vidnelo.exe manually
while the app is closed. Keep your existing tools folder.
Settings are stored in %LOCALAPPDATA%\LinkDownloader.

CREDITS
Created by cavxvac: https://github.com/cavxvac
Website: https://gerardbinder.com
Releases: https://github.com/cavxvac/Vidnelo/releases

Vidnelo is an independent graphical client for yt-dlp.
Third-party executables are downloaded during setup, not bundled in this ZIP:
yt-dlp: https://github.com/yt-dlp/yt-dlp
FFmpeg builds: https://github.com/yt-dlp/FFmpeg-Builds
Deno: https://github.com/denoland/deno
These projects retain their own licenses. Setup records download URLs and
SHA256 hashes in tools\sources.txt and extracts the FFmpeg license to tools\LICENSE.txt.
'@ | Set-Content -LiteralPath (Join-Path $package 'READ ME FIRST.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $releaseDir "Vidnelo-$version-Windows-x64.zip"
if (Test-Path -LiteralPath $zip) { throw "Package already exists: $zip" }
[IO.Compression.ZipFile]::CreateFromDirectory($staging,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Created: $zip"
