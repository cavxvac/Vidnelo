$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Vidnelo.lnk'
$oldShortcutPath = Join-Path $desktop 'Pobierz film - Link Downloader.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
if ((Test-Path -LiteralPath $shortcutPath) -and $shortcut.TargetPath -ne (Join-Path $projectRoot 'Vidnelo.exe')) {
    throw 'This shortcut points to a different program. It was not changed.'
}
$shortcut.TargetPath = Join-Path $projectRoot 'Vidnelo.exe'
$shortcut.WorkingDirectory = $projectRoot
$shortcut.IconLocation = (Join-Path $projectRoot 'assets\vidnelo-v.ico') + ',0'
$shortcut.Description = 'Vidnelo - pobierz film MP4 lub dzwiek MP3'
$shortcut.Save()
$verified = $shell.CreateShortcut($shortcutPath)
$verified.TargetPath
$verified.IconLocation
if (Test-Path -LiteralPath $oldShortcutPath) {
    $old = $shell.CreateShortcut($oldShortcutPath)
    if ($old.TargetPath -eq (Join-Path $projectRoot 'LinkDownloader.exe')) {
        Remove-Item -LiteralPath $oldShortcutPath
    }
}
