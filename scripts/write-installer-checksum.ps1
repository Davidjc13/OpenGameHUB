param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    Write-Error "Installer not found: $InstallerPath"
}

$hash = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash
$checksumPath = "$InstallerPath.sha256"
Set-Content -LiteralPath $checksumPath -Value $hash -Encoding ascii -NoNewline

Write-Host "Checksum: $checksumPath" -ForegroundColor Green
Write-Host $hash
