$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path $projectRoot 'releases'
$upload = Join-Path $releaseDir 'github-upload'
if (Test-Path -LiteralPath $upload) { throw "Source upload folder already exists: $upload" }
New-Item -ItemType Directory -Force -Path $upload,(Join-Path $upload 'tools') | Out-Null
foreach ($name in @('src','assets','tests','scripts','docs')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $upload -Recurse
}
foreach ($name in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md','INSTRUKCJA.txt','.gitignore','build.ps1','Start Vidnelo.cmd')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $upload
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'tools\README.md') -Destination (Join-Path $upload 'tools')
$unexpected = @(Get-ChildItem -LiteralPath $upload -File -Recurse -Force | Where-Object { $_.Extension -in @('.exe','.log','.tmp','.zip','.pyc') })
if ($unexpected.Count) { throw 'Generated files found in source upload' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $releaseDir 'Vidnelo-1.0-source.zip'
if (Test-Path -LiteralPath $zip) { throw "Source ZIP already exists: $zip" }
[IO.Compression.ZipFile]::CreateFromDirectory($upload,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
Write-Host "Upload the CONTENTS of: $upload"
Write-Host "Source archive: $zip"
