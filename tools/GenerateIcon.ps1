#Requires -Version 5.1
<#
.SYNOPSIS
    Generates AppIcon.ico for ShipExtract — a multi-size .ico containing
    16x16, 32x32, 48x48, and 256x256 frames.

.DESCRIPTION
    Each frame shows a dark-blue (#1F4E79) rounded-rectangle background with
    a bold white "S" letterform centred on it.
    Output: src\ShipExtract.UI\Assets\AppIcon.ico
#>

param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\ShipExtract.UI\Assets\AppIcon.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# ── Helpers ──────────────────────────────────────────────────────────────────

function New-Frame {
    param([int]$Size)

    $bmp        = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g          = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # ── Background: dark-blue rounded rectangle ───────────────────────────────
    $bgColor  = [System.Drawing.Color]::FromArgb(255, 31, 78, 121)   # #1F4E79
    $bgBrush  = [System.Drawing.SolidBrush]::new($bgColor)
    $radius   = [int]([Math]::Max(2, $Size * 0.15))
    $rect     = [System.Drawing.Rectangle]::new(0, 0, $Size - 1, $Size - 1)

    # Draw rounded rectangle via path
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d    = $radius * 2
    $path.AddArc($rect.X,                          $rect.Y,                           $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d,                 $rect.Y,                           $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d,                 $rect.Bottom - $d,                 $d, $d,   0, 90)
    $path.AddArc($rect.X,                          $rect.Bottom - $d,                 $d, $d,  90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # ── Letter "S" ────────────────────────────────────────────────────────────
    $fontSize  = [float]([Math]::Max(6, $Size * 0.60))
    $fontStyle = [System.Drawing.FontStyle]::Bold

    # Prefer a geometric sans-serif; fall back gracefully
    $fontFamily = $null
    foreach ($name in @("Segoe UI", "Arial", "Verdana", "Sans-Serif")) {
        try {
            $fontFamily = [System.Drawing.FontFamily]::new($name)
            break
        } catch { }
    }

    $font       = [System.Drawing.Font]::new($fontFamily, $fontSize, $fontStyle, [System.Drawing.GraphicsUnit]::Pixel)
    $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $sf         = [System.Drawing.StringFormat]::new()
    $sf.Alignment          = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment      = [System.Drawing.StringAlignment]::Center
    $drawRect   = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size)
    $g.DrawString("S", $font, $whiteBrush, $drawRect, $sf)

    # ── Clean up GDI+ objects ─────────────────────────────────────────────────
    $sf.Dispose()
    $font.Dispose()
    $whiteBrush.Dispose()
    $bgBrush.Dispose()
    $path.Dispose()
    $g.Dispose()

    return $bmp
}

# ── Write .ico (RIFF-style ICO format) ───────────────────────────────────────

function Write-Ico {
    param(
        [System.Drawing.Bitmap[]]$Frames,
        [string]$Path
    )

    # Encode each frame as a 32-bpp PNG blob (Windows Vista+ supports PNG in ICO)
    $pngBlobs = foreach ($frame in $Frames) {
        $ms = [System.IO.MemoryStream]::new()
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        , $ms.ToArray()
        $ms.Dispose()
    }

    $frameCount   = $pngBlobs.Count
    $headerSize   = 6                           # ICONDIR
    $entrySize    = 16                          # ICONDIRENTRY per frame
    $dataOffset   = $headerSize + ($entrySize * $frameCount)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    # ICONDIR
    $writer.Write([uint16]0)           # reserved
    $writer.Write([uint16]1)           # type = ICO
    $writer.Write([uint16]$frameCount)

    # ICONDIRENTRY array
    $currentOffset = $dataOffset
    for ($i = 0; $i -lt $frameCount; $i++) {
        $w    = $Frames[$i].Width
        $h    = $Frames[$i].Height
        $blob = $pngBlobs[$i]

        $writer.Write([byte]  ($w -band 0xFF))   # width  (0 = 256)
        $writer.Write([byte]  ($h -band 0xFF))   # height (0 = 256)
        $writer.Write([byte]  0)                 # colour count (0 = no palette)
        $writer.Write([byte]  0)                 # reserved
        $writer.Write([uint16]1)                 # colour planes
        $writer.Write([uint16]32)                # bits per pixel
        $writer.Write([uint32]$blob.Length)      # size of image data
        $writer.Write([uint32]$currentOffset)    # offset of image data

        $currentOffset += $blob.Length
    }

    # Image data
    foreach ($blob in $pngBlobs) {
        $writer.Write($blob)
    }

    $writer.Flush()
    $icoBytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()

    [System.IO.File]::WriteAllBytes($Path, $icoBytes)
}

# ── Main ──────────────────────────────────────────────────────────────────────

$outDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

Write-Host "Generating icon frames..." -ForegroundColor Cyan
$frames = @(16, 32, 48, 256) | ForEach-Object { New-Frame -Size $_ }

Write-Host "Writing $OutputPath ..." -ForegroundColor Cyan
Write-Ico -Frames $frames -Path $OutputPath

foreach ($f in $frames) { $f.Dispose() }

Write-Host "Icon generated: $OutputPath" -ForegroundColor Green
