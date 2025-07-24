# Simple script to generate coverage report from existing coverage data
param(
    [string]$CoverageFile = "coverage.cobertura.xml",
    [string]$OutputDir = "CoverageReport"
)

Write-Host "Generating coverage report..." -ForegroundColor Blue

# Check if coverage file exists
if (-not (Test-Path $CoverageFile)) {
    Write-Error "Coverage file '$CoverageFile' not found. Run tests with coverage first."
    exit 1
}

# Install ReportGenerator if not present
$hasReportGenerator = dotnet tool list --global | Select-String "reportgenerator"
if (-not $hasReportGenerator) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# Clean previous report
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Generate report
reportgenerator -reports:$CoverageFile -targetdir:$OutputDir -reporttypes:"Html;JsonSummary;Badges"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Coverage report generated successfully!" -ForegroundColor Green
    Write-Host "Report location: $(Resolve-Path $OutputDir)" -ForegroundColor Yellow
    Write-Host "Main report: $(Resolve-Path "$OutputDir/index.html")" -ForegroundColor Yellow
    
    # Display summary if available
    if (Test-Path "$OutputDir/Summary.json") {
        $summary = Get-Content "$OutputDir/Summary.json" | ConvertFrom-Json
        Write-Host "`nCoverage Summary:" -ForegroundColor Green
        Write-Host "Line Coverage: $($summary.summary.linecoverage)%" -ForegroundColor Cyan
        Write-Host "Branch Coverage: $($summary.summary.branchcoverage)%" -ForegroundColor Cyan
        Write-Host "Method Coverage: $($summary.summary.methodcoverage)%" -ForegroundColor Cyan
    }
} else {
    Write-Error "Failed to generate coverage report"
    exit 1
}