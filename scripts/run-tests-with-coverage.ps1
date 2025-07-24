# PowerShell script to run tests with coverage locally
param(
    [string]$Configuration = "Debug",
    [switch]$OpenReport = $false,
    [switch]$SkipBuild = $false
)

Write-Host "Running AlphaScope Tests with Coverage Analysis" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

# Check if required tools are installed
$hasCoverage = dotnet tool list --global | Select-String "dotnet-coverage"
$hasReportGenerator = dotnet tool list --global | Select-String "reportgenerator"

if (-not $hasCoverage) {
    Write-Host "Installing dotnet-coverage..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-coverage
}

if (-not $hasReportGenerator) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# Build solution if not skipped
if (-not $SkipBuild) {
    Write-Host "`nBuilding solution..." -ForegroundColor Blue
    dotnet build PlayerScope.sln --configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Exiting."
        exit 1
    }
}

# Clean previous coverage reports
if (Test-Path "CoverageReport") {
    Remove-Item -Recurse -Force "CoverageReport"
}
if (Test-Path "coverage.cobertura.xml") {
    Remove-Item "coverage.cobertura.xml"
}
if (Test-Path "TestResults") {
    Remove-Item -Recurse -Force "TestResults"
}

Write-Host "`nRunning tests with coverage collection..." -ForegroundColor Blue

# Run tests with coverage
dotnet-coverage collect --settings coverage.settings --output coverage.cobertura.xml "dotnet test PlayerScope.Tests/PlayerScope.Tests.csproj --no-build --configuration $Configuration --logger trx --results-directory TestResults/ --verbosity normal"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Test execution failed. Check the output above for details."
    exit 1
}

# Generate HTML coverage report
Write-Host "`nGenerating coverage report..." -ForegroundColor Blue
reportgenerator -reports:coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:"Html;JsonSummary;Badges"

# Parse and display coverage summary
if (Test-Path "CoverageReport/Summary.json") {
    $summary = Get-Content "CoverageReport/Summary.json" | ConvertFrom-Json
    
    Write-Host "`n" -NoNewline
    Write-Host "Coverage Summary:" -ForegroundColor Green
    Write-Host "=================" -ForegroundColor Green
    Write-Host "Line Coverage:   $($summary.summary.linecoverage)%" -ForegroundColor $(if ($summary.summary.linecoverage -ge 80) { "Green" } elseif ($summary.summary.linecoverage -ge 60) { "Yellow" } else { "Red" })
    Write-Host "Branch Coverage: $($summary.summary.branchcoverage)%" -ForegroundColor $(if ($summary.summary.branchcoverage -ge 70) { "Green" } elseif ($summary.summary.branchcoverage -ge 50) { "Yellow" } else { "Red" })
    Write-Host "Method Coverage: $($summary.summary.methodcoverage)%" -ForegroundColor $(if ($summary.summary.methodcoverage -ge 85) { "Green" } elseif ($summary.summary.methodcoverage -ge 70) { "Yellow" } else { "Red" })
    
    Write-Host "`nCovered Lines:   $($summary.summary.coveredlines) / $($summary.summary.coverablelines)" -ForegroundColor Cyan
    Write-Host "Covered Branches: $($summary.summary.coveredbranches) / $($summary.summary.totalbranches)" -ForegroundColor Cyan
    Write-Host "Covered Methods:  $($summary.summary.coveredmethods) / $($summary.summary.totalmethods)" -ForegroundColor Cyan
}

# Display test results summary
Write-Host "`n" -NoNewline
Write-Host "Test Results:" -ForegroundColor Green
Write-Host "=============" -ForegroundColor Green

$testResults = Get-ChildItem -Path "TestResults" -Filter "*.trx" | Get-Content | Out-String
if ($testResults -match 'total="(\d+)".*passed="(\d+)".*failed="(\d+)"') {
    $total = $matches[1]
    $passed = $matches[2] 
    $failed = $matches[3]
    
    Write-Host "Total Tests: $total" -ForegroundColor Cyan
    Write-Host "Passed:      $passed" -ForegroundColor Green
    Write-Host "Failed:      $failed" -ForegroundColor $(if ($failed -eq "0") { "Green" } else { "Red" })
}

Write-Host "`nCoverage report generated in: $(Resolve-Path 'CoverageReport')" -ForegroundColor Yellow
Write-Host "Main report file: $(Resolve-Path 'CoverageReport/index.html')" -ForegroundColor Yellow

# Open coverage report if requested
if ($OpenReport) {
    Write-Host "`nOpening coverage report..." -ForegroundColor Blue
    Start-Process "CoverageReport/index.html"
}

Write-Host "`nCoverage analysis complete!" -ForegroundColor Green