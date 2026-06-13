#Requires -Version 5.1
<#
.SYNOPSIS
    Generates all MSIX / Microsoft Store image assets for ShipExtract.

.DESCRIPTION
    Produces PNG files in src\ShipExtract.UI\Assets\ using only
    System.Drawing — no external tools required.

    Brand palette
      Background : #1F4E79  (dark blue)
      Accent     : #C55A11  (orange)
      Foreground : White

    Assets produced
      Square44x44Logo.png      (44 x 44)
      Square150x150Logo.png   (150 x 150)
      Wide310x150Logo.png     (310 x 150)
      Square310x310Logo.png   (310 x 310)
      StoreLogo.png            (50 x 50)
      SplashScreen.png        (620 x 300)
      StoreScreenshot1.png   (1366 x 768)
      StoreScreenshot2.png   (1366 x 768)
#>

param(
    [string]$AssetsDir = (Join-Path $PSScriptRoot "..\src\ShipExtract.UI\Assets")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# ── Colour helpers ────────────────────────────────────────────────────────────

$BgColor     = [System.Drawing.Color]::FromArgb(255, 31,  78, 121)   # #1F4E79
$AccentColor = [System.Drawing.Color]::FromArgb(255,197,  90,  17)   # #C55A11
$White       = [System.Drawing.Color]::White
$LightBlue   = [System.Drawing.Color]::FromArgb(255,173, 216, 230)

# ── Font helper ───────────────────────────────────────────────────────────────

function Get-Font {
    param([float]$Size, [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Bold)
    foreach ($name in @("Segoe UI", "Arial", "Verdana")) {
        try { return [System.Drawing.Font]::new($name, $Size, $Style, [System.Drawing.GraphicsUnit]::Pixel) }
        catch { }
    }
    return [System.Drawing.Font]::new([System.Drawing.FontFamily]::GenericSansSerif, $Size, $Style, [System.Drawing.GraphicsUnit]::Pixel)
}

# ── DrawAsset ─────────────────────────────────────────────────────────────────
# Draws a branded image and saves it as PNG.
#
# Parameters
#   $W / $H          — pixel dimensions
#   $Title           — primary text (e.g. "ShipExtract")
#   $Subtitle        — secondary text (may be empty)
#   $ShowLogo        — draw a small ship icon in the top-left quadrant
#   $IsWide          — title on the right half (for Wide tile)
#   $IsScreenshot    — draw a larger screenshot-style placeholder

function Save-Asset {
    param(
        [int]    $W,
        [int]    $H,
        [string] $FileName,
        [string] $Title     = "ShipExtract",
        [string] $Subtitle  = "Shipping PDF Extractor",
        [bool]   $IsWide    = $false,
        [bool]   $IsScreenshot = $false
    )

    $bmp = [System.Drawing.Bitmap]::new($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear($BgColor)

    $bgBrush     = [System.Drawing.SolidBrush]::new($BgColor)
    $accentBrush = [System.Drawing.SolidBrush]::new($AccentColor)
    $whiteBrush  = [System.Drawing.SolidBrush]::new($White)
    $lbBrush     = [System.Drawing.SolidBrush]::new($LightBlue)

    # ── Ship icon (simplified polygon) ───────────────────────────────────────
    $iconSize  = [int]([Math]::Min($W, $H) * 0.30)
    $iconX     = [int]($W * 0.10)
    $iconY     = [int](($H - $iconSize) / 2)

    if ($IsWide -or $IsScreenshot) {
        # Centre the icon in the left third
        $iconX = [int]($W * 0.08)
        $iconY = [int](($H - $iconSize) / 2)
    }

    # Hull trapezoid
    $hullPts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($iconX,               $iconY + $iconSize * 0.6),
        [System.Drawing.PointF]::new($iconX + $iconSize,   $iconY + $iconSize * 0.6),
        [System.Drawing.PointF]::new($iconX + $iconSize * 0.85, $iconY + $iconSize),
        [System.Drawing.PointF]::new($iconX + $iconSize * 0.15, $iconY + $iconSize)
    )
    $g.FillPolygon($whiteBrush, $hullPts)

    # Mast
    $mastPen = [System.Drawing.Pen]::new($White, [Math]::Max(1, $iconSize * 0.06))
    $mastX   = $iconX + $iconSize * 0.5
    $g.DrawLine($mastPen, $mastX, $iconY + $iconSize * 0.6, $mastX, $iconY)

    # Sail (right triangle)
    $sailPts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($mastX,               $iconY),
        [System.Drawing.PointF]::new($mastX + $iconSize * 0.4, $iconY + $iconSize * 0.55),
        [System.Drawing.PointF]::new($mastX,               $iconY + $iconSize * 0.55)
    )
    $g.FillPolygon($accentBrush, $sailPts)

    $mastPen.Dispose()

    # ── Wave under hull ───────────────────────────────────────────────────────
    $wavePen = [System.Drawing.Pen]::new($LightBlue, [Math]::Max(1, $iconSize * 0.05))
    $waveY   = [int]($iconY + $iconSize * 1.05)
    $seg     = [int]($iconSize / 3)
    for ($i = 0; $i -lt 3; $i++) {
        $x0 = $iconX + $i * $seg
        $x1 = $iconX + ($i + 1) * $seg
        $mid = ($x0 + $x1) / 2
        if ($i % 2 -eq 0) {
            $g.DrawArc($wavePen, $x0, $waveY - $seg / 4, $x1 - $x0, $seg / 2, 180, 180)
        } else {
            $g.DrawArc($wavePen, $x0, $waveY - $seg / 4, $x1 - $x0, $seg / 2, 0, 180)
        }
    }
    $wavePen.Dispose()

    # ── Text ─────────────────────────────────────────────────────────────────
    if ($IsScreenshot) {
        # Large centred title for placeholder screenshots
        $titleFont    = Get-Font -Size ([float]([Math]::Max(20, $H * 0.07)))
        $subtitleFont = Get-Font -Size ([float]([Math]::Max(12, $H * 0.035))) -Style ([System.Drawing.FontStyle]::Regular)
        $sf           = [System.Drawing.StringFormat]::GenericTypographic
        $sf.Alignment      = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment  = [System.Drawing.StringAlignment]::Center
        $g.DrawString($Title,    $titleFont,    $whiteBrush, [System.Drawing.RectangleF]::new(0, $H*0.30, $W, $H*0.25), $sf)
        $g.DrawString($Subtitle, $subtitleFont, $lbBrush,   [System.Drawing.RectangleF]::new(0, $H*0.55, $W, $H*0.15), $sf)
        $titleFont.Dispose(); $subtitleFont.Dispose(); $sf.Dispose()
    }
    elseif ($IsWide) {
        $textAreaX = [int]($W * 0.45)
        $textAreaW = $W - $textAreaX - [int]($W * 0.05)
        $titleFont    = Get-Font -Size ([float]([Math]::Max(8, $H * 0.22)))
        $subtitleFont = Get-Font -Size ([float]([Math]::Max(6, $H * 0.12))) -Style ([System.Drawing.FontStyle]::Regular)
        $sf = [System.Drawing.StringFormat]::new()
        $sf.Alignment     = [System.Drawing.StringAlignment]::Near
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $g.DrawString($Title,    $titleFont,    $whiteBrush, [System.Drawing.RectangleF]::new($textAreaX, 0,     $textAreaW, $H * 0.60), $sf)
        $g.DrawString($Subtitle, $subtitleFont, $lbBrush,   [System.Drawing.RectangleF]::new($textAreaX, $H*0.60, $textAreaW, $H * 0.40), $sf)
        $titleFont.Dispose(); $subtitleFont.Dispose(); $sf.Dispose()
    }
    else {
        # Small/square tiles: title below icon
        $titleFont    = Get-Font -Size ([float]([Math]::Max(5, [Math]::Min($W, $H) * 0.11)))
        $subtitleFont = Get-Font -Size ([float]([Math]::Max(4, [Math]::Min($W, $H) * 0.07))) -Style ([System.Drawing.FontStyle]::Regular)
        $sf = [System.Drawing.StringFormat]::new()
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Near
        $titleY = [int](($H + $iconSize) / 2 + [Math]::Min($W,$H) * 0.03)
        $g.DrawString($Title,    $titleFont,    $whiteBrush, [System.Drawing.RectangleF]::new(0, $titleY,             $W, $H - $titleY), $sf)
        if ($H - $titleY - $titleFont.Height * 1.2 -gt $subtitleFont.Height) {
            $g.DrawString($Subtitle, $subtitleFont, $lbBrush,   [System.Drawing.RectangleF]::new(0, $titleY + $titleFont.Height * 1.2, $W, $H), $sf)
        }
        $titleFont.Dispose(); $subtitleFont.Dispose(); $sf.Dispose()
    }

    # ── Save PNG ─────────────────────────────────────────────────────────────
    $bgBrush.Dispose(); $accentBrush.Dispose(); $whiteBrush.Dispose(); $lbBrush.Dispose()
    $g.Dispose()

    $outPath = Join-Path $AssetsDir $FileName
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    Write-Host "  $FileName" -ForegroundColor Gray
}

# ── Main ──────────────────────────────────────────────────────────────────────

if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null
}

Write-Host "Generating Store assets in: $AssetsDir" -ForegroundColor Cyan

Save-Asset -W  44  -H  44  -FileName "Square44x44Logo.png"    -Title "SE" -Subtitle ""
Save-Asset -W 150  -H 150  -FileName "Square150x150Logo.png"
Save-Asset -W 310  -H 150  -FileName "Wide310x150Logo.png"    -IsWide $true
Save-Asset -W 310  -H 310  -FileName "Square310x310Logo.png"
Save-Asset -W  50  -H  50  -FileName "StoreLogo.png"          -Title "SE" -Subtitle ""
Save-Asset -W 620  -H 300  -FileName "SplashScreen.png"       -IsWide $true
Save-Asset -W 1366 -H 768  -FileName "StoreScreenshot1.png"   -Title "ShipExtract" -Subtitle "Convert shipping PDFs to structured Excel data" -IsScreenshot $true
Save-Asset -W 1366 -H 768  -FileName "StoreScreenshot2.png"   -Title "ShipExtract" -Subtitle "Batch processing with AI-powered extraction"     -IsScreenshot $true

Write-Host "Store assets generated." -ForegroundColor Green
