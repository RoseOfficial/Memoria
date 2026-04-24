#!/bin/bash

# Bash script to run tests with coverage locally (Linux/macOS)
CONFIGURATION="${1:-Debug}"
OPEN_REPORT="${2:-false}"
SKIP_BUILD="${3:-false}"

echo -e "\033[32mRunning Memoria Tests with Coverage Analysis\033[0m"
echo -e "\033[32m=============================================\033[0m"

# Check if required tools are installed
if ! dotnet tool list --global | grep -q "dotnet-coverage"; then
    echo -e "\033[33mInstalling dotnet-coverage...\033[0m"
    dotnet tool install --global dotnet-coverage
fi

if ! dotnet tool list --global | grep -q "reportgenerator"; then
    echo -e "\033[33mInstalling ReportGenerator...\033[0m"
    dotnet tool install --global dotnet-reportgenerator-globaltool
fi

# Build solution if not skipped
if [ "$SKIP_BUILD" != "true" ]; then
    echo -e "\n\033[34mBuilding solution...\033[0m"
    dotnet build Memoria.sln --configuration "$CONFIGURATION"
    if [ $? -ne 0 ]; then
        echo -e "\033[31mBuild failed. Exiting.\033[0m" >&2
        exit 1
    fi
fi

# Clean previous coverage reports
rm -rf CoverageReport coverage.cobertura.xml TestResults

echo -e "\n\033[34mRunning tests with coverage collection...\033[0m"

# Run tests with coverage
dotnet-coverage collect --settings coverage.settings --output coverage.cobertura.xml "dotnet test Memoria.Tests/Memoria.Tests.csproj --no-build --configuration $CONFIGURATION --logger trx --results-directory TestResults/ --verbosity normal"

if [ $? -ne 0 ]; then
    echo -e "\033[31mTest execution failed. Check the output above for details.\033[0m" >&2
    exit 1
fi

# Generate HTML coverage report
echo -e "\n\033[34mGenerating coverage report...\033[0m"
reportgenerator -reports:coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:"Html;JsonSummary;Badges"

# Parse and display coverage summary
if [ -f "CoverageReport/Summary.json" ]; then
    # Parse JSON using basic tools (works on most systems)
    LINE_COVERAGE=$(grep -o '"linecoverage":[^,}]*' CoverageReport/Summary.json | cut -d':' -f2)
    BRANCH_COVERAGE=$(grep -o '"branchcoverage":[^,}]*' CoverageReport/Summary.json | cut -d':' -f2)
    METHOD_COVERAGE=$(grep -o '"methodcoverage":[^,}]*' CoverageReport/Summary.json | cut -d':' -f2)
    
    COVERED_LINES=$(grep -o '"coveredlines":[^,}]*' CoverageReport/Summary.json | cut -d':' -f2)
    COVERABLE_LINES=$(grep -o '"coverablelines":[^,}]*' CoverageReport/Summary.json | cut -d':' -f2)
    
    echo -e "\n\033[32mCoverage Summary:\033[0m"
    echo -e "\033[32m=================\033[0m"
    echo -e "Line Coverage:   $LINE_COVERAGE%"
    echo -e "Branch Coverage: $BRANCH_COVERAGE%"
    echo -e "Method Coverage: $METHOD_COVERAGE%"
    
    echo -e "\n\033[36mCovered Lines:   $COVERED_LINES / $COVERABLE_LINES\033[0m"
fi

# Display test results summary
echo -e "\n\033[32mTest Results:\033[0m"
echo -e "\033[32m=============\033[0m"

if [ -d "TestResults" ]; then
    TRX_FILE=$(find TestResults -name "*.trx" | head -1)
    if [ -n "$TRX_FILE" ]; then
        TOTAL=$(grep -o 'total="[^"]*"' "$TRX_FILE" | cut -d'"' -f2)
        PASSED=$(grep -o 'passed="[^"]*"' "$TRX_FILE" | cut -d'"' -f2)
        FAILED=$(grep -o 'failed="[^"]*"' "$TRX_FILE" | cut -d'"' -f2)
        
        echo -e "\033[36mTotal Tests: $TOTAL\033[0m"
        echo -e "\033[32mPassed:      $PASSED\033[0m"
        if [ "$FAILED" = "0" ]; then
            echo -e "\033[32mFailed:      $FAILED\033[0m"
        else
            echo -e "\033[31mFailed:      $FAILED\033[0m"
        fi
    fi
fi

echo -e "\n\033[33mCoverage report generated in: $(pwd)/CoverageReport\033[0m"
echo -e "\033[33mMain report file: $(pwd)/CoverageReport/index.html\033[0m"

# Open coverage report if requested
if [ "$OPEN_REPORT" = "true" ]; then
    echo -e "\n\033[34mOpening coverage report...\033[0m"
    if command -v xdg-open > /dev/null; then
        xdg-open CoverageReport/index.html
    elif command -v open > /dev/null; then
        open CoverageReport/index.html
    else
        echo -e "\033[33mCannot open browser automatically. Please open CoverageReport/index.html manually.\033[0m"
    fi
fi

echo -e "\n\033[32mCoverage analysis complete!\033[0m"