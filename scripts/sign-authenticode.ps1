param(
    [Parameter(Mandatory = $true)]
    [string[]]$Files,
    [switch]$Required
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    $onPath = Get-Command "signtool.exe" -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kitsRoot)) {
        return $null
    }

    return Get-ChildItem -LiteralPath $kitsRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}

function Assert-FilesExist {
    foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file)) {
            Write-Error "File to sign not found: $file"
        }
    }
}

function Get-PfxPath {
    if (-not [string]::IsNullOrWhiteSpace($env:AUTHENTICODE_PFX_PATH)) {
        return $env:AUTHENTICODE_PFX_PATH
    }

    if ([string]::IsNullOrWhiteSpace($env:AUTHENTICODE_PFX_BASE64)) {
        return $null
    }

    $tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
    $pfxPath = Join-Path $tempRoot "opengamehub-codesign.pfx"
    [System.IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($env:AUTHENTICODE_PFX_BASE64.Trim()))
    return $pfxPath
}

function Invoke-PfxSign([string]$PfxPath) {
    $signtool = Find-SignTool
    if (-not $signtool) {
        Write-Error "signtool.exe not found. Install the Windows 10/11 SDK."
    }

    $timestampUrl = if ($env:AUTHENTICODE_TIMESTAMP_URL) {
        $env:AUTHENTICODE_TIMESTAMP_URL
    } else {
        "http://timestamp.digicert.com"
    }

    foreach ($file in $Files) {
        $signArgs = @(
            "sign",
            "/fd", "SHA256",
            "/td", "SHA256",
            "/tr", $timestampUrl,
            "/f", $PfxPath
        )
        if (-not [string]::IsNullOrWhiteSpace($env:AUTHENTICODE_PFX_PASSWORD)) {
            $signArgs += @("/p", $env:AUTHENTICODE_PFX_PASSWORD)
        }
        $signArgs += $file

        Write-Host "Signing (Authenticode PFX): $file" -ForegroundColor Cyan
        & $signtool @signArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "signtool failed for $file (exit $LASTEXITCODE)"
        }
    }
}

function Invoke-TrustedSigning {
    $endpoint = $env:AZURE_TRUSTED_SIGNING_ENDPOINT
    $account = $env:AZURE_TRUSTED_SIGNING_ACCOUNT
    $profile = $env:AZURE_TRUSTED_SIGNING_PROFILE
    if ([string]::IsNullOrWhiteSpace($profile)) {
        $profile = $env:AZURE_TRUSTED_SIGNING_CERTIFICATE_PROFILE
    }

    if ([string]::IsNullOrWhiteSpace($endpoint) -or
        [string]::IsNullOrWhiteSpace($account) -or
        [string]::IsNullOrWhiteSpace($profile)) {
        Write-Error "Azure Trusted Signing requires AZURE_TRUSTED_SIGNING_ENDPOINT, AZURE_TRUSTED_SIGNING_ACCOUNT, and AZURE_TRUSTED_SIGNING_PROFILE."
    }

    $toolVersion = "0.9.1-beta.25330.2"
    dotnet tool update --global sign --version $toolVersion | Out-Host
    if ($LASTEXITCODE -ne 0) {
        dotnet tool install --global sign --version $toolVersion | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Could not install the 'sign' CLI for Azure Trusted Signing."
        }
    }

    $signCmd = Join-Path $env:USERPROFILE ".dotnet\tools\sign.exe"
    if (-not (Test-Path -LiteralPath $signCmd)) {
        $onPath = Get-Command "sign" -ErrorAction SilentlyContinue
        if ($onPath) {
            $signCmd = $onPath.Source
        } else {
            Write-Error "The 'sign' CLI was installed but could not be located."
        }
    }

    foreach ($file in $Files) {
        Write-Host "Signing (Azure Trusted Signing): $file" -ForegroundColor Cyan
        & $signCmd code trusted-signing `
            --endpoint $endpoint `
            --account $account `
            --certificate-profile $profile `
            --file-digest sha256 `
            --timestamp-digest sha256 `
            --timestamp-url "http://timestamp.acs.microsoft.com" `
            --files $file
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Azure Trusted Signing failed for $file (exit $LASTEXITCODE)"
        }
    }
}

Assert-FilesExist

$pfxPath = Get-PfxPath
$hasTrustedSigning = -not [string]::IsNullOrWhiteSpace($env:AZURE_TRUSTED_SIGNING_ACCOUNT)

if ($pfxPath) {
    Invoke-PfxSign -PfxPath $pfxPath
    return
}

if ($hasTrustedSigning) {
    Invoke-TrustedSigning
    return
}

$message = "Authenticode signing skipped: no AUTHENTICODE_PFX_* or AZURE_TRUSTED_SIGNING_* credentials."
if ($Required) {
    Write-Error $message
}

Write-Host $message -ForegroundColor Yellow
