# OpenGameHUB dev tasks — PowerShell equivalent of a Makefile.
# Usage: .\make.ps1 <target>   or   .\make.cmd <target>
# Example: .\make.ps1 ci

param(
    [Parameter(Position = 0)]
    [string]$Target = "help",

    [string]$Config = $env:CONFIG
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Config)) {
    $Config = "Release"
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$sln = "OpenGameHUB.sln"
$coverageRaw = "coverage/raw"
$coverageReport = "coverage/report"

$env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Show-Help {
    Write-Host "OpenGameHUB targets:"
    Write-Host ""
    @(
        @{ Name = "help";             Description = "Show this help" },
        @{ Name = "all";              Description = "Alias for ci" },
        @{ Name = "restore";          Description = "dotnet restore" },
        @{ Name = "format";           Description = "Auto-fix formatting (dotnet format)" },
        @{ Name = "lint";             Description = "Verify format and style (CI lint step)" },
        @{ Name = "build";            Description = "Build (CONFIG=Debug for debug)" },
        @{ Name = "test";             Description = "Run unit tests" },
        @{ Name = "test-coverage";    Description = "Run tests with coverlet" },
        @{ Name = "coverage-report";  Description = "Generate report from last test-coverage run" },
        @{ Name = "coverage";         Description = "Tests + coverage report (>=70% gate)" },
        @{ Name = "ci";               Description = "Full CI pipeline locally" },
        @{ Name = "clean";            Description = "Remove build outputs and coverage artifacts" },
        @{ Name = "installer";        Description = "Build Windows installer" }
    ) | ForEach-Object {
        Write-Host ("  {0,-18} {1}" -f $_.Name, $_.Description)
    }
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\make.ps1 ci"
    Write-Host "  .\make.ps1 build -Config Debug"
    Write-Host "  `$env:CONFIG='Debug'; .\make.ps1 test"
}

function Invoke-Restore {
    Invoke-Dotnet restore $sln
}

function Invoke-Format {
    Invoke-Dotnet format $sln
}

function Invoke-Lint {
    try { git config --local core.autocrlf false 2>$null } catch { }
    Invoke-Dotnet format $sln, "--verify-no-changes", "--verbosity", "minimal"
}

function Invoke-Build {
    Invoke-Restore
    Invoke-Dotnet build $sln, "-c", $Config, "--no-restore"
}

function Invoke-Test {
    Invoke-Build
    Invoke-Dotnet test $sln, "-c", $Config, "--no-build", "--verbosity", "normal"
}

function Invoke-TestCoverage {
    Invoke-Build
    Invoke-Dotnet test $sln, "-c", $Config, "--no-build",
        "--settings", "coverlet.runsettings",
        "--collect:XPlat Code Coverage",
        "--results-directory", "./$coverageRaw",
        "--verbosity", "normal"
}

function Ensure-ReportGenerator {
    $null = dotnet tool update -g dotnet-reportgenerator-globaltool 2>$null
    if ($LASTEXITCODE -ne 0) {
        Invoke-Dotnet tool install -g dotnet-reportgenerator-globaltool
    }
}

function Invoke-CoverageReport {
    Ensure-ReportGenerator

    $reports = @(
        Get-ChildItem -Path $coverageRaw -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue
        Get-ChildItem -Path tests/OpenGameHUB.Tests/TestResults -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue
    ) | Sort-Object LastWriteTime -Descending

    if ($reports.Count -eq 0) {
        Write-Error "No coverage files found. Run '.\make.ps1 test-coverage' first."
    }

    & reportgenerator `
        -reports:$reports[0].FullName `
        -targetdir:./$coverageReport `
        -reporttypes:"TextSummary;HtmlInline_AzurePipelines;Cobertura"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Get-Content "./$coverageReport/Summary.txt"
}

function Invoke-Coverage {
    Invoke-TestCoverage
    Invoke-CoverageReport
}

function Invoke-Ci {
    Invoke-Lint
    Invoke-Build
    Invoke-TestCoverage
    Invoke-CoverageReport
}

function Invoke-Clean {
    Invoke-Dotnet clean $sln, "-c", $Config
    foreach ($path in @($coverageRaw, $coverageReport, "tests/OpenGameHUB.Tests/TestResults", "publish", "dist")) {
        if (Test-Path $path) {
            Remove-Item -Recurse -Force $path
        }
    }
}

function Invoke-Installer {
    Invoke-Build
    & (Join-Path $root "build-installer.ps1")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

switch ($Target.ToLowerInvariant()) {
    "help"            { Show-Help }
    "all"             { Invoke-Ci }
    "restore"         { Invoke-Restore }
    "format"          { Invoke-Format }
    "lint"            { Invoke-Lint }
    "build"           { Invoke-Build }
    "test"            { Invoke-Test }
    "test-coverage"   { Invoke-TestCoverage }
    "coverage-report" { Invoke-CoverageReport }
    "coverage"        { Invoke-Coverage }
    "ci"              { Invoke-Ci }
    "clean"           { Invoke-Clean }
    "installer"       { Invoke-Installer }
    default {
        Write-Error "Unknown target '$Target'. Run '.\make.ps1 help' for available targets."
    }
}
