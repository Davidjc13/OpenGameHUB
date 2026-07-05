param(
    [string]$AppVersion = "",
    [switch]$SkipWingetInstall
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

& (Join-Path $root "generate-icon.ps1")

# Optional: bundle legendary.exe for offline installs (run scripts/fetch-legendary.ps1 first)
try {
    & (Join-Path $root "scripts\fetch-legendary.ps1")
} catch {
    Write-Host "Could not download legendary.exe (offline build will download it on first run)." -ForegroundColor Yellow
}

Write-Host "Publishing OpenGameHUB (Release, win-x64, self-contained)..." -ForegroundColor Cyan
dotnet publish OpenGameHUB.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o ./publish/win-x64-release

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc -and -not $SkipWingetInstall) {
    Write-Host "Inno Setup 6 not found. Installing via winget..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements

    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    Write-Error "Inno Setup compiler (ISCC.exe) not found. Install from https://jrsoftware.org/isinfo.php"
}

$isccArgs = if ($AppVersion) {
    @("/DMyAppVersion=$AppVersion", ".\installer\OpenGameHUB.iss")
} else {
    @(".\installer\OpenGameHUB.iss")
}

Write-Host "Building installer with Inno Setup..." -ForegroundColor Cyan
if ($AppVersion) {
    Write-Host "Version: $AppVersion" -ForegroundColor Cyan
}
& $iscc @isccArgs

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setup = Get-ChildItem ".\dist\OpenGameHUB-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "Done: $($setup.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($setup.Length / 1MB, 1)) MB"
