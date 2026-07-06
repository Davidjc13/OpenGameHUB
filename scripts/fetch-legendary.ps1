$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetDir = Join-Path $root "third-party\legendary"
$targetFile = Join-Path $targetDir "legendary.exe"
$url = "https://github.com/derrod/legendary/releases/latest/download/legendary.exe"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Write-Host "Downloading legendary.exe from GitHub..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $targetFile -UseBasicParsing

Write-Host "Saved: $targetFile ($([math]::Round((Get-Item $targetFile).Length / 1MB, 1)) MB)" -ForegroundColor Green
