$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root "third-party\legendary"
$targetFile = Join-Path $targetDir "legendary.exe"
$manifestPath = Join-Path $targetDir "manifest.json"

if (-not (Test-Path -LiteralPath $manifestPath)) {
    Write-Error "Legendary manifest not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$url = [string]$manifest.downloadUrl
$expectedHash = ([string]$manifest.sha256).Trim().ToUpperInvariant()
$expectedSize = [int64]$manifest.sizeBytes

if ([string]::IsNullOrWhiteSpace($url) -or $expectedHash.Length -ne 64) {
    Write-Error "Legendary manifest is missing downloadUrl or sha256."
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Write-Host "Downloading legendary.exe $($manifest.version) (pinned)..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $targetFile -UseBasicParsing

$info = Get-Item -LiteralPath $targetFile
if ($expectedSize -gt 0 -and $info.Length -ne $expectedSize) {
    Remove-Item -LiteralPath $targetFile -Force -ErrorAction SilentlyContinue
    Write-Error "legendary.exe size mismatch: $($info.Length) bytes, expected $expectedSize."
}

$actualHash = (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash.ToUpperInvariant()
if ($actualHash -ne $expectedHash) {
    Remove-Item -LiteralPath $targetFile -Force -ErrorAction SilentlyContinue
    Write-Error "legendary.exe SHA256 mismatch. Expected $expectedHash, got $actualHash."
}

Write-Host "Saved: $targetFile ($([math]::Round($info.Length / 1MB, 1)) MB)" -ForegroundColor Green
Write-Host "SHA256: $actualHash"
