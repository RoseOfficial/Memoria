# AlphaScope Testing Documentation

This document provides comprehensive information about the testing infrastructure for the AlphaScope FFXIV plugin project.

## Test Suite Overview

The AlphaScope project includes comprehensive testing across multiple layers:

- **Total Tests**: 72 tests
- **Unit Tests**: 26 tests (Plugin API client, Server controllers)
- **Integration Tests**: 21 tests (Data upload workflows, cache management)
- **Performance Tests**: 25 tests (Database, cache, and API performance)

## Test Categories

### 1. Unit Tests
Located in `PlayerScope.Tests/`

#### API Client Tests (`API/ApiClientTests.cs`)
- Tests limited by Dalamud dependencies (11 tests)
- Focuses on testable public API surface
- Validates request/response model structures

#### Server Controller Tests
- **PlayersController** (`Controllers/PlayersControllerTests.cs`) - 5 tests
- **UsersController** (`Controllers/UsersControllerTests.cs`) - 5 tests  
- **RetainersController** (`Controllers/RetainersControllerTests.cs`) - 5 tests
- **AuthController** (`Controllers/AuthControllerTests.cs`) - 5 tests
- **ServerController** (`Controllers/ServerControllerTests.cs`) - 5 tests

### 2. Integration Tests  
Located in `PlayerScope.Tests/Integration/`

#### PersistenceContext Integration Tests
- **21 tests** covering data upload workflows
- Cache management and validation
- Multi-data handling and cleanup operations
- Player/retainer relationship management

### 3. Performance Tests
Located in `PlayerScope.Tests/Performance/`

#### Database Performance (`DatabasePerformanceTests.cs`) - 8 tests
- Bulk insert operations (1000+ records)
- Query performance validation
- Memory usage monitoring
- Scaling behavior analysis

#### Cache Performance (`CachePerformanceTests.cs`) - 9 tests  
- High-volume cache operations (10,000+ entries)
- Concurrent access patterns
- World-retainer cache complexity
- Memory efficiency validation

#### API Performance Simulation (`ApiPerformanceSimulationTests.cs`) - 8 tests
- JSON serialization/deserialization
- Concurrent processing validation
- High-frequency operation patterns
- Data transformation performance

## Running Tests

### Local Development

#### Quick Test Run
```bash
# Run all tests
dotnet test PlayerScope.Tests/PlayerScope.Tests.csproj

# Run specific test categories
dotnet test --filter "FullyQualifiedName~Performance"
dotnet test --filter "FullyQualifiedName~Integration"
dotnet test --filter "FullyQualifiedName~Controllers"
```

#### With Coverage Analysis

**Windows (PowerShell)**:
```powershell
# Run with coverage and open report
.\scripts\run-tests-with-coverage.ps1 -OpenReport

# Run in Release mode
.\scripts\run-tests-with-coverage.ps1 -Configuration Release

# Skip build step
.\scripts\run-tests-with-coverage.ps1 -SkipBuild
```

**Linux/macOS (Bash)**:
```bash
# Run with coverage
./scripts/run-tests-with-coverage.sh

# Run in Release mode and open report
./scripts/run-tests-with-coverage.sh Release true

# Skip build step
./scripts/run-tests-with-coverage.sh Debug false true
```

#### Manual Coverage Setup
```bash
# Install required tools
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet-coverage collect --settings coverage.settings --output coverage.cobertura.xml "dotnet test PlayerScope.Tests/PlayerScope.Tests.csproj --logger trx --results-directory TestResults/"

# Generate report
reportgenerator -reports:coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:"Html;JsonSummary"
```

### CI/CD Pipeline

Tests automatically run on:
- **Push** to `main` or `develop` branches
- **Pull requests** targeting `main` or `develop`

The pipeline includes:
1. **Test Execution** with coverage collection
2. **Coverage Reporting** with minimum thresholds
3. **Artifact Generation** for test results and coverage reports
4. **Build Validation** for both plugin and server components

## Test Configuration

### Coverage Settings (`coverage.settings`)
- **Included Modules**: PlayerScope.dll, PlayerScopeServer.dll
- **Excluded**: Test assemblies, generated code
- **Thresholds**: 70% minimum coverage recommended
- **Formats**: Cobertura XML, HTML reports, JSON summaries

### GitHub Actions (`.github/workflows/test-coverage.yml`)
- **Windows runner** for Dalamud compatibility
- **Multi-target .NET** (8.0.x for server, 9.0.x for plugin)  
- **Codecov integration** for coverage tracking
- **PR commenting** with coverage delta information

## Test Infrastructure

### Shared Utilities (`TestUtilities/`)
- **Database utilities**: In-memory database setup
- **Logger utilities**: Mock logger creation for testing
- **Test data factories**: Consistent test data generation

### Mock Configuration
- **NSubstitute**: For dependency mocking
- **Entity Framework InMemory**: For database testing
- **FluentAssertions**: For readable test assertions

## Limitations and Constraints

### Dalamud Dependencies
Some tests are limited by Dalamud framework dependencies:
- **ApiClient tests**: 11 tests with Dalamud initialization requirements
- **Integration boundaries**: Database persistence requires full plugin context
- **Static dependencies**: Some static field access patterns

### Performance Test Environment
- **Windows-specific**: Some performance characteristics vary by OS
- **Hardware dependent**: Performance thresholds may need adjustment
- **Memory testing**: GC behavior can affect memory usage tests

## Coverage Targets

### Current Coverage Goals
- **Line Coverage**: 70%+ (minimum threshold)
- **Branch Coverage**: 60%+ (recommended)
- **Method Coverage**: 80%+ (target)

### Coverage Exclusions
- Generated code (Entity Framework migrations)
- Property getters/setters
- Debug and compiler-generated code
- Third-party integration boundaries

## Best Practices

### Writing Tests
1. **Follow AAA pattern**: Arrange, Act, Assert
2. **Use meaningful names**: Descriptive test method names
3. **Test one thing**: Single responsibility per test
4. **Mock dependencies**: Isolate units under test
5. **Clean up resources**: Proper disposal in integration tests

### Performance Testing
1. **Set realistic thresholds**: Based on actual usage patterns
2. **Test scaling behavior**: Validate performance across data sizes  
3. **Monitor memory usage**: Prevent memory leaks
4. **Use appropriate timeouts**: Allow for CI environment variability

### Continuous Improvement
1. **Monitor coverage trends**: Track coverage over time
2. **Add tests for bugs**: Regression prevention
3. **Review failing tests**: Investigate and fix consistently
4. **Update thresholds**: Adjust as codebase matures

## Troubleshooting

### Common Issues

**Dalamud dependency errors**:
- Ensure tests don't require full plugin initialization
- Use mocking for Dalamud-specific interfaces
- Focus on testable public API surfaces

**Performance test failures**:
- Check if running on slower hardware
- Adjust timeout values for CI environments
- Verify no competing processes affecting performance

**Coverage collection issues**:
- Ensure coverage tools are installed globally
- Verify coverage.settings paths are correct
- Check that target assemblies are being built

### Getting Help
- Check existing test patterns for examples
- Review test utility classes for common functionality
- Consult plugin documentation for Dalamud-specific testing approaches