[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$assetDirectory = Join-Path $repoRoot "src\LanTransfer.Host\wwwroot\assets"
$iconPath = Join-Path $assetDirectory "lantransfer.ico"
$pngPath = Join-Path $assetDirectory "lantransfer.png"
$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-RoundedRectanglePath {
    param(
        [float]$Size,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc(0, 0, $diameter, $diameter, 180, 90)
    $path.AddArc($Size - $diameter, 0, $diameter, $diameter, 270, 90)
    $path.AddArc($Size - $diameter, $Size - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc(0, $Size - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng {
    param([int]$Size)

    $scale = $Size / 32.0
    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $backgroundPath = New-RoundedRectanglePath -Size $Size -Radius (7 * $scale)
    $backgroundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 29, 111, 242))
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $monitorPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [Math]::Max(1.0, 2 * $scale))
    $monitorPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawRectangle($monitorPen, 7 * $scale, 7 * $scale, 17 * $scale, 14 * $scale)

    $baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $graphics.FillRectangle($baseBrush, 14 * $scale, 21 * $scale, 3 * $scale, 4 * $scale)
    $graphics.FillRectangle($baseBrush, 10 * $scale, 25 * $scale, 11 * $scale, 2 * $scale)

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $stream.ToArray()

    $stream.Dispose()
    $baseBrush.Dispose()
    $monitorPen.Dispose()
    $backgroundBrush.Dispose()
    $backgroundPath.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()

    return ,$pngBytes
}

$frames = @(
    foreach ($size in $sizes) {
        [PSCustomObject]@{
            Size = $size
            Bytes = New-IconPng -Size $size
        }
    }
)

$iconStream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($iconStream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$frames.Count)

$imageOffset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frame.Bytes.Length)
    $writer.Write([UInt32]$imageOffset)
    $imageOffset += $frame.Bytes.Length
}

foreach ($frame in $frames) {
    $writer.Write($frame.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
$writer.Dispose()
$iconStream.Dispose()

$largeBitmap = New-Object System.Drawing.Bitmap(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$largeGraphics = [System.Drawing.Graphics]::FromImage($largeBitmap)
$largeGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$largeGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$largeGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$largeGraphics.Clear([System.Drawing.Color]::Transparent)
$largePath = New-RoundedRectanglePath -Size 1024 -Radius 224
$largeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 29, 111, 242))
$largeGraphics.FillPath($largeBrush, $largePath)
$largePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 64)
$largePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$largeGraphics.DrawRectangle($largePen, 224, 224, 544, 448)
$largeWhiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$largeGraphics.FillRectangle($largeWhiteBrush, 448, 672, 96, 128)
$largeGraphics.FillRectangle($largeWhiteBrush, 320, 800, 352, 64)
$largeBitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$largeWhiteBrush.Dispose()
$largePen.Dispose()
$largeBrush.Dispose()
$largePath.Dispose()
$largeGraphics.Dispose()
$largeBitmap.Dispose()

Write-Host "Generated $iconPath and $pngPath"
