function Get-SignToolPath {
    $kitRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path $kitRoot)) {
        return $null
    }

    Get-ChildItem -Path $kitRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '\\x64\\' } |
        Sort-Object {
            $versionFolder = $_.Directory.Parent.Name
            try { [version]$versionFolder } catch { [version]"0.0.0.0" }
        } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

function Invoke-AuthenticodeSign {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$CertificatePath,
        [string]$CertificatePassword = "",
        [string]$Description = "OpenGameHUB",
        [string]$DescriptionUrl = "https://github.com/Davidjc13/OpenGameHUB",
        [string]$TimestampUrl = "http://timestamp.digicert.com"
    )

    if (-not (Test-Path $Path)) {
        throw "Cannot sign missing file: $Path"
    }

    if (-not (Test-Path $CertificatePath)) {
        throw "Cannot sign with missing certificate: $CertificatePath"
    }

    $signtool = Get-SignToolPath
    if (-not $signtool) {
        throw "signtool.exe not found. Install the Windows SDK (Signing Tools for Desktop Apps)."
    }

    Write-Host "Signing: $Path" -ForegroundColor Cyan

    $signArgs = @(
        "sign",
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/f", $CertificatePath,
        "/d", $Description,
        "/du", $DescriptionUrl,
        $Path
    )

    if ($CertificatePassword) {
        $signArgs = @(
            "sign",
            "/fd", "SHA256",
            "/tr", $TimestampUrl,
            "/td", "SHA256",
            "/f", $CertificatePath,
            "/p", $CertificatePassword,
            "/d", $Description,
            "/du", $DescriptionUrl,
            $Path
        )
    }

    & $signtool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed for $Path (exit code $LASTEXITCODE)."
    }

    & $signtool verify /pa $Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify failed for $Path (exit code $LASTEXITCODE)."
    }
}

function Invoke-SignPublishedExecutables {
    param(
        [Parameter(Mandatory)][string]$PublishDirectory,
        [Parameter(Mandatory)][string]$CertificatePath,
        [string]$CertificatePassword = ""
    )

    $executables = Get-ChildItem -Path $PublishDirectory -Filter *.exe -Recurse -File
    if ($executables.Count -eq 0) {
        throw "No executables found to sign under $PublishDirectory"
    }

    foreach ($executable in $executables) {
        Invoke-AuthenticodeSign -Path $executable.FullName -CertificatePath $CertificatePath -CertificatePassword $CertificatePassword
    }
}
