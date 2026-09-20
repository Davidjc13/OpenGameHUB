param(
    [string]$AppVersion = "",
    [switch]$SkipWingetInstall,
    [ValidateSet("All", "Publish", "Inno", "Checksum")]
    [string]$Stage = "All",
    [switch]$SkipSign
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Get-SetupExe {
    if ($AppVersion) {
        $exact = Join-Path $root "dist\OpenGameHUB-Setup-$AppVersion.exe"
        if (Test-Path -LiteralPath $exact) {
            return Get-Item -LiteralPath $exact
        }
    }

    return Get-ChildItem ".\dist\OpenGameHUB-Setup-*.exe" |
        Where-Object { $_.Extension -eq ".exe" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Invoke-AuthenticodeSign([string[]]$Files) {
    if ($SkipSign) {
        return
    }

    $signScript = Join-Path $root "scripts\sign-authenticode.ps1"
    & $signScript -Files $Files
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Invoke-Checksum {
    $setup = Get-SetupExe
    if (-not $setup) {
        Write-Error "Installer not found in dist/."
    }

    & (Join-Path $root "scripts\write-installer-checksum.ps1") -InstallerPath $setup.FullName
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "Done: $($setup.FullName)" -ForegroundColor Green
    Write-Host "Size: $([math]::Round($setup.Length / 1MB, 1)) MB"
    Write-Host "Checksum: $($setup.FullName).sha256" -ForegroundColor Green
}

if ($Stage -eq "Checksum") {
    Invoke-Checksum
    return
}

if ($Stage -eq "All" -or $Stage -eq "Publish") {
    & (Join-Path $root "generate-icon.ps1")

    # Optional: bundle legendary.exe for offline installs (run scripts/fetch-legendary.ps1 first)
    try {
        & (Join-Path $root "scripts\fetch-legendary.ps1")
    } catch {
        Write-Host "Could not download legendary.exe (offline build will download it on first run)." -ForegroundColor Yellow
    }

    Write-Host "Publishing OpenGameHUB (Release, win-x64, self-contained)..." -ForegroundColor Cyan
    $publishArgs = @(
        "publish", "OpenGameHUB.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=false",
        "-o", "./publish/win-x64-release"
    )
    if ($AppVersion) {
        $publishArgs += "-p:InformationalVersion=$AppVersion"
        if ($AppVersion -match '(\d+\.\d+\.\d+)') {
            $semver = $Matches[1]
            $publishArgs += "-p:Version=$semver"
            $publishArgs += "-p:AssemblyVersion=$semver.0"
            $publishArgs += "-p:FileVersion=$semver.0"
        }
    }
    dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $appExe = Join-Path $root "publish\win-x64-release\OpenGameHUB.exe"
    Invoke-AuthenticodeSign -Files @($appExe)
}

if ($Stage -eq "Publish") {
    return
}

if ($Stage -ne "All" -and $Stage -ne "Inno") {
    Write-Error "Unsupported stage: $Stage"
}

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

$setup = Get-SetupExe
if (-not $setup) {
    Write-Error "Installer not found in dist/ after Inno Setup."
}

Invoke-AuthenticodeSign -Files @($setup.FullName)

if ($Stage -eq "All") {
    Invoke-Checksum
} else {
    Write-Host "Installer: $($setup.FullName)" -ForegroundColor Green
}
