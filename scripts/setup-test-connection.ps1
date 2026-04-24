#!/usr/bin/env pwsh
# PowerShell script to set up test connection between Memoria plugin and server

Write-Host "Setting up Memoria test connection..." -ForegroundColor Green

# Test server health
Write-Host "`nTesting server health..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "https://localhost:5001/health"
    Write-Host "✅ Server is healthy: $($healthResponse.Content)" -ForegroundColor Green
} catch {
    Write-Host "❌ Server health check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test server status
Write-Host "`nTesting server status..." -ForegroundColor Yellow
try {
    $statusResponse = Invoke-WebRequest -Uri "https://localhost:5001/v1/server"
    Write-Host "✅ Server status: $($statusResponse.Content)" -ForegroundColor Green
} catch {
    Write-Host "❌ Server status check failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Create test user
Write-Host "`nCreating test user..." -ForegroundColor Yellow
try {
    $userResponse = Invoke-WebRequest -Uri "https://localhost:5001/v1/users/create-test-user" -Method POST
    Write-Host "✅ Test user created successfully" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 409) {
        Write-Host "✅ Test user already exists" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Test user creation response: $($_.Exception.Message)" -ForegroundColor Yellow
        # Continue anyway as user might already exist
    }
}

# Test API with the test user credentials
Write-Host "`nTesting API authentication..." -ForegroundColor Yellow
$apiKey = "PrkdCR9gOCSYZYOlGruL-1387972975"
$headers = @{
    "api-key" = $apiKey
    "Content-Type" = "application/json"
}

try {
    $meResponse = Invoke-WebRequest -Uri "https://localhost:5001/v1/users/me" -Headers $headers
    Write-Host "✅ API authentication successful: $($meResponse.Content)" -ForegroundColor Green
} catch {
    Write-Host "❌ API authentication failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Configuration Instructions ===" -ForegroundColor Cyan
Write-Host "To connect your Memoria plugin to the server:" -ForegroundColor White
Write-Host "1. Open the Memoria plugin in-game using /alpha" -ForegroundColor White
Write-Host "2. Go to Settings tab" -ForegroundColor White
Write-Host "3. Set the following values:" -ForegroundColor White
Write-Host "   - Server URL: https://localhost:5001/v1/" -ForegroundColor Yellow
Write-Host "   - API Key: PrkdCR9gOCSYZYOlGruL" -ForegroundColor Yellow
Write-Host "   - Account ID: 1387972975" -ForegroundColor Yellow
Write-Host "4. The status should show 'Connected'" -ForegroundColor White

Write-Host "`nServer is ready for connections!" -ForegroundColor Green