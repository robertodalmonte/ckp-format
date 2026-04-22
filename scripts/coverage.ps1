<#
.SYNOPSIS
    Produces an HTML branch-coverage report for the Ckp.* solution.

.DESCRIPTION
    Runs `dotnet test` with coverlet to emit Cobertura XML, then invokes
    ReportGenerator (local tool) to produce an HTML report at
    TestResults/coverage/index.html.

    Per-project branch-coverage targets (Phase 2, item T9):
      - Ckp.Core     >= 85%
      - Ckp.IO       >= 85%
      - Ckp.Signing  >= 85%
      - Ckp.Transpiler >= 75%
      - Ckp.Epub     >= 75%

.PARAMETER Filter
    Optional xUnit filter expression (e.g. "FullyQualifiedName~Alignment").

.PARAMETER OpenReport
    If set, opens the generated index.html in the default browser.

.EXAMPLE
    pwsh ./scripts/coverage.ps1
    pwsh ./scripts/coverage.ps1 -OpenReport
#>
[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$OpenReport
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$testResults = Join-Path $repoRoot 'TestResults'
$coverageOut = Join-Path $testResults 'coverage'

if (Test-Path $testResults) {
    Write-Host "Clearing previous TestResults..."
    Remove-Item $testResults -Recurse -Force
}

Write-Host "Restoring local tools..."
& dotnet tool restore | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed" }

$testArgs = @(
    'test',
    'ckp-format.slnx',
    '--nologo',
    '--collect:XPlat Code Coverage',
    '--results-directory', $testResults,
    '--',
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura'
)

if ($Filter) {
    $testArgs = $testArgs[0..2] + @('--filter', $Filter) + $testArgs[3..($testArgs.Length - 1)]
}

Write-Host "Running tests with coverage..."
& dotnet @testArgs | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }

$coberturaFiles = Get-ChildItem -Path $testResults -Recurse -Filter 'coverage.cobertura.xml' -File
if (-not $coberturaFiles) {
    throw "No coverage.cobertura.xml files found under $testResults"
}

Write-Host "Found $($coberturaFiles.Count) coverage file(s). Generating HTML report..."

$reports = ($coberturaFiles | ForEach-Object { $_.FullName }) -join ';'

& dotnet reportgenerator `
    "-reports:$reports" `
    "-targetdir:$coverageOut" `
    '-reporttypes:Html;TextSummary;Cobertura' `
    '-assemblyfilters:+Ckp.*;-Ckp.Tests;-Ckp.*.Cli' `
    | Out-Host
if ($LASTEXITCODE -ne 0) { throw "reportgenerator failed with exit code $LASTEXITCODE" }

$summaryPath = Join-Path $coverageOut 'Summary.txt'
if (Test-Path $summaryPath) {
    Write-Host "`n--- Coverage summary ---"
    Get-Content $summaryPath | Write-Host
}

$indexPath = Join-Path $coverageOut 'index.html'
Write-Host "`nHTML report: $indexPath"

if ($OpenReport -and (Test-Path $indexPath)) {
    Start-Process $indexPath
}
