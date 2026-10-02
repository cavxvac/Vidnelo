param([string]$Source = (Join-Path (Split-Path $PSScriptRoot -Parent) 'assets\app-icon.png'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$sourceImage = [Drawing.Image]::FromFile($Source)
try {
    $sizes = @(16,24,32,48,64,128,256)
    $frames = @()
    foreach ($size in $sizes) {
        $bitmap = New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($sourceImage,0,0,$size,$size)
            $buffer = New-Object IO.MemoryStream
            $bitmap.Save($buffer,[Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$buffer.ToArray()
            $buffer.Dispose()
            if ($size -eq 256) { $bitmap.Save((Join-Path $projectRoot 'assets\app-icon-ui.png'),[Drawing.Imaging.ImageFormat]::Png) }
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    $output = [IO.File]::Create((Join-Path $projectRoot 'assets\vidnelo-v.ico'))
    $writer = New-Object IO.BinaryWriter($output)
    try {
        $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([UInt16]1); $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frames[$index].Length); $writer.Write([UInt32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $output.Dispose() }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\vidnelo-v.ico') -Destination (Join-Path $projectRoot 'assets\app-icon.ico') -Force
    Write-Output "Icon sizes: $($sizes -join ', ')"
} finally { $sourceImage.Dispose() }
