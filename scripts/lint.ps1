# DriftRide Linting and Code Quality Script (PowerShell)
# This script runs code analysis, linting, and quality checks

param(
    [switch]$SkipTests = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 DriftRide Code Quality Check" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# Navigate to solution directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Resolve-Path (Join-Path $scriptDir "..")
Set-Location $solutionDir

try {
    Write-Host "📦 Restoring packages..." -ForegroundColor Yellow
    $verbosity = if ($Verbose) { "normal" } else { "minimal" }

    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }

    Write-Host "🔨 Building solution..." -ForegroundColor Yellow
    dotnet build --no-restore --verbosity $verbosity
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    Write-Host "🔍 Running code analysis..." -ForegroundColor Yellow
    dotnet build --no-restore --verbosity normal --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Code analysis failed" }

    if (-not $SkipTests) {
        Write-Host "🧪 Running tests..." -ForegroundColor Yellow
        dotnet test --no-build --verbosity $verbosity --configuration Release
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    }

    Write-Host "✅ All checks completed successfully!" -ForegroundColor Green
    Write-Host "📊 Code quality metrics:" -ForegroundColor Cyan
    Write-Host "  - All projects built without errors" -ForegroundColor White
    Write-Host "  - Code analysis rules applied" -ForegroundColor White
    Write-Host "  - StyleCop rules enforced" -ForegroundColor White
    if (-not $SkipTests) {
        Write-Host "  - Tests passing" -ForegroundColor White
    } else {
        Write-Host "  - Tests skipped" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Quality check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}