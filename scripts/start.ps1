param([switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
try {
    $required = @('yt-dlp.exe', 'ffmpeg.exe', 'ffprobe.exe', 'deno.exe')
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $projectRoot "tools\$_") -PathType Leaf) })
    if ($missing.Count) {
        Write-Host 'Vidnelo first-time setup: downloading yt-dlp, FFmpeg and Deno from their official GitHub repositories.'
        Write-Host 'Internet access is required. This may take a few minutes.'
        & (Join-Path $PSScriptRoot 'setup-tools.ps1')
    }
    foreach ($name in $required) {
        $tool = Join-Path $projectRoot "tools\$name"
        if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Missing dependency: $name" }
        $versionArgument = if ($name -in @('ffmpeg.exe','ffprobe.exe')) { '-version' } else { '--version' }
        $output = & $tool $versionArgument 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Unable to run $name. Extract the ZIP to a writable folder and try again." }
    }
    if (-not $PrepareOnly) {
        Start-Process -FilePath (Join-Path $projectRoot 'Vidnelo.exe') -WorkingDirectory $projectRoot
    }
} catch {
    Write-Host "Setup could not finish: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
