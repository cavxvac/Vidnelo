param([string]$OutputName = 'Vidnelo.exe')
$ErrorActionPreference = 'Stop'
if ([IO.Path]::GetFileName($OutputName) -ne $OutputName -or [IO.Path]::GetExtension($OutputName) -ne '.exe') { throw 'OutputName must be an .exe filename in the application folder' }
$sourceDir = Join-Path $PSScriptRoot 'src'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$assemblyName = [IO.Path]::GetFileNameWithoutExtension($OutputName)
$layoutDir = Join-Path $PSScriptRoot ('.build\' + $assemblyName)
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
$xaml = [IO.File]::ReadAllText((Join-Path $sourceDir 'MainWindow.xaml')).Replace('__FAST__','{StaticResource FastDuration}')
[IO.File]::WriteAllText((Join-Path $layoutDir 'MainWindow.xaml'),$xaml,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $sourceDir 'SupportedSites.xaml') -Destination (Join-Path $layoutDir 'SupportedSites.xaml') -Force
Copy-Item -LiteralPath (Join-Path $sourceDir 'About.xaml') -Destination (Join-Path $layoutDir 'About.xaml') -Force
Copy-Item -LiteralPath (Join-Path $sourceDir 'LayoutCompile.proj') -Destination (Join-Path $layoutDir 'LayoutCompile.proj') -Force
& (Join-Path $framework 'MSBuild.exe') (Join-Path $layoutDir 'LayoutCompile.proj') /nologo /verbosity:quiet /property:UiAssembly=$assemblyName
if ($LASTEXITCODE -ne 0) { throw 'UI compilation failed' }
$references = @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object { '/reference:' + (Join-Path $framework "WPF\$_") }
& $compiler /nologo /target:winexe /optimize+ /codepage:65001 /out:"$PSScriptRoot\$OutputName" /win32icon:"$PSScriptRoot\assets\app-icon.ico" /win32manifest:"$sourceDir\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Xaml.dll /reference:System.Web.Extensions.dll @references /resource:"$layoutDir\Layout.g.resources,$assemblyName.g.resources" /resource:"$PSScriptRoot\assets\app-icon-ui.png,AppIcon.png" /resource:"$PSScriptRoot\assets\vidnelo-logo-original.png,BrandLogo.png" /resource:"$PSScriptRoot\assets\app-icon.ico,AppIcon.ico" /resource:"$sourceDir\Translations.txt,Translations.txt" "$sourceDir\Localization.cs" "$sourceDir\DownloadEngine.cs" "$sourceDir\ProgressModel.cs" "$sourceDir\MainWindow.cs" "$sourceDir\SupportedSitesDialog.cs" "$sourceDir\AboutDialog.cs" "$sourceDir\AppUpdates.cs" "$sourceDir\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
