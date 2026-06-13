#Requires -Version 5.1

param(
    # "Gumroad", "Store", or "All"
    [string]$Target  = "Gumroad",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$ProjectRoot  = $PSScriptRoot
$SolutionFile = Join-Path $ProjectRoot "ShipExtract.sln"
$UiProject    = Join-Path $ProjectRoot "src\ShipExtract.UI\ShipExtract.UI.csproj"

Write-Host "ShipExtract Release Builder v$Version" -ForegroundColor Cyan
Write-Host "Target: $Target" -ForegroundColor Cyan
Write-Host ""

# Step 1: Run tests
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test $SolutionFile --configuration Release --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed - aborting release"
    exit 1
}
Write-Host "Tests passed" -ForegroundColor Green
Write-Host ""

# Step 2: Build
Write-Host "Building..." -ForegroundColor Yellow
dotnet build $SolutionFile --configuration Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
Write-Host "Build succeeded" -ForegroundColor Green
Write-Host ""

# Step 3: Generate store assets
Write-Host "Generating store assets..." -ForegroundColor Yellow
& (Join-Path $ProjectRoot "tools\CreateStoreAssets.ps1")
Write-Host "Assets generated" -ForegroundColor Green
Write-Host ""

# Step 4: Publish Gumroad build
if ($Target -in @("Gumroad", "All")) {
    Write-Host "Publishing Gumroad build..." -ForegroundColor Yellow
    $publishDir = Join-Path $ProjectRoot "publish\Gumroad\ShipExtract-v$Version"

    dotnet publish $UiProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        --output $publishDir

    if ($LASTEXITCODE -ne 0) { Write-Error "Gumroad publish failed"; exit 1 }

    # Remove any .pdb or XML doc files that slipped through
    Get-ChildItem -Path $publishDir -Include "*.pdb","*.xml" -Recurse | Remove-Item -Force

    # Copy supporting files
    Copy-Item (Join-Path $ProjectRoot "README.md") $publishDir -Force
    $changelogPath = Join-Path $ProjectRoot "CHANGELOG.md"
    if (Test-Path $changelogPath) {
        Copy-Item $changelogPath $publishDir -Force
    }

    # Wait for Windows Defender / AV to release the file lock on the new EXE
    $exePath = Join-Path $publishDir "ShipExtract.UI.exe"
    $maxWait = 30
    $waited  = 0
    Write-Host "Waiting for file lock to release..." -ForegroundColor Yellow
    while ($waited -lt $maxWait) {
        try {
            $stream = [System.IO.File]::Open(
                $exePath,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::None)
            $stream.Close()
            $stream.Dispose()
            Write-Host "File ready." -ForegroundColor Green
            break
        }
        catch {
            Start-Sleep -Seconds 2
            $waited += 2
            Write-Host "  Still locked, waiting... ($waited/$maxWait s)" -ForegroundColor Gray
        }
    }
    if ($waited -ge $maxWait) {
        Write-Warning "File lock timeout - attempting ZIP anyway"
    }
    Start-Sleep -Seconds 1

    # Create ZIP for Gumroad upload (retry loop — AV may re-lock after our check)
    $zipPath    = Join-Path $ProjectRoot "publish\ShipExtract-v$Version-Windows.zip"
    $zipMaxWait = 60
    $zipWaited  = 0
    $zipDone    = $false
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    while (-not $zipDone -and $zipWaited -lt $zipMaxWait) {
        try {
            Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
            $zipDone = $true
        }
        catch {
            if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Seconds 3
            $zipWaited += 3
            Write-Host "  ZIP locked, retrying... ($zipWaited/$zipMaxWait s)" -ForegroundColor Gray
        }
    }
    if (-not $zipDone) {
        Write-Error "Could not create ZIP after $zipMaxWait s - AV may still be holding the lock"
        exit 1
    }

    # Verify ZIP contents
    if (Test-Path $zipPath) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip     = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
        $entries = $zip.Entries | Select-Object Name, Length
        $zip.Dispose()
        Write-Host "ZIP contents:" -ForegroundColor Cyan
        $entries | ForEach-Object {
            Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)"
        }
        $pdbNames = ($entries | Where-Object { $_.Name -like "*.pdb" }).Name
        if ($pdbNames) {
            Write-Warning "ZIP contains .pdb files: $($pdbNames -join ', ')"
        }
    } else {
        Write-Error "ZIP was not created at: $zipPath"
        exit 1
    }

    Write-Host ""
    Write-Host "Gumroad build ready:" -ForegroundColor Green
    Write-Host "  EXE : $exePath"
    Write-Host "  ZIP : $zipPath"
    Write-Host "  Upload the ZIP to Gumroad"
    Write-Host ""
}

# Step 5: Publish Store build (framework-dependent)
if ($Target -in @("Store", "All")) {
    Write-Host "Publishing Store build..." -ForegroundColor Yellow
    $storeDir = Join-Path $ProjectRoot "publish\Store\ShipExtract-v$Version"

    dotnet publish $UiProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained false `
        -p:PublishReadyToRun=true `
        -p:DebugType=none `
        --output $storeDir

    if ($LASTEXITCODE -ne 0) { Write-Error "Store publish failed"; exit 1 }

    Get-ChildItem -Path $storeDir -Include "*.pdb","*.xml" -Recurse | Remove-Item -Force

    Write-Host ""
    Write-Host "Store build ready at: $storeDir" -ForegroundColor Green
    Write-Host "Next: Package as MSIX using Visual Studio or"
    Write-Host "      the Windows Application Packaging Project"
    Write-Host ""
}

Write-Host "Release build complete!" -ForegroundColor Green
