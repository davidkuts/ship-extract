#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$Project = Join-Path $ProjectRoot "src\ShipExtract.UI\ShipExtract.UI.csproj"
$OutputDir = Join-Path $ProjectRoot "publish\ShipExtract-v1.0.0"

Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test "$ProjectRoot\ShipExtract.sln" --no-build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed — aborting publish."
    exit 1
}

Write-Host "Building release..." -ForegroundColor Cyan
dotnet build "$ProjectRoot\ShipExtract.sln" --configuration Release

Write-Host "Publishing..." -ForegroundColor Cyan
dotnet publish $Project `
    --configuration Release `
    --output $OutputDir `
    --no-build

# Write a brief readme into the output directory
$publishReadme = @"
ShipExtract v1.0.0
==================
Run ShipExtract.UI.exe to start the application.

On first launch you will be prompted to enter your Anthropic API key.
Get a free key at: https://console.anthropic.com

For OCR support on scanned PDFs:
1. Download eng.traineddata from:
   https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
2. Place it in: %APPDATA%\ShipExtract\tessdata\
"@
$publishReadme | Out-File -FilePath (Join-Path $OutputDir "README.txt") -Encoding UTF8

$exePath = Join-Path $OutputDir "ShipExtract.UI.exe"
if (Test-Path $exePath) {
    Write-Host ""
    Write-Host "Build complete -> $exePath" -ForegroundColor Green
} else {
    Write-Error "Publish output not found at expected path: $exePath"
}
