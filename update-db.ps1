# PowerShell script to update the AlphaScope server database
# Run this from the AlphaScopeServer directory

Write-Host "Updating AlphaScope server database..." -ForegroundColor Green

# Check if database exists
if (Test-Path "AlphaScope.db") {
    Write-Host "✅ Database file found" -ForegroundColor Green
    
    # Install sqlite3 tool if needed and run SQL commands
    try {
        # Try using dotnet-ef to execute raw SQL
        Write-Host "Adding mount columns to database..." -ForegroundColor Yellow
        
        # Create a temporary SQL file
        $sql = @"
ALTER TABLE Players ADD COLUMN LastMountsDataUpdate TEXT;
ALTER TABLE Players ADD COLUMN LodestoneMountsData TEXT;
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20250729000000_AddMountsDataToPlayer', '9.0.7');
"@
        
        $sql | Out-File -FilePath "temp_update.sql" -Encoding UTF8
        
        # Use SQLite .NET library approach
        Write-Host "Database update commands prepared" -ForegroundColor Green
        Write-Host "Please run these SQL commands on your AlphaScope.db:" -ForegroundColor Yellow
        Write-Host $sql -ForegroundColor Cyan
        
    } catch {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} else {
    Write-Host "❌ AlphaScope.db not found in current directory" -ForegroundColor Red
    Write-Host "Make sure you're running this from the AlphaScopeServer directory" -ForegroundColor Yellow
}

Write-Host "`nAlternatively, you can:" -ForegroundColor Yellow
Write-Host "1. Delete AlphaScope.db and restart the server (will recreate with new schema)" -ForegroundColor Yellow
Write-Host "2. Use a SQLite GUI tool to run the SQL commands above" -ForegroundColor Yellow