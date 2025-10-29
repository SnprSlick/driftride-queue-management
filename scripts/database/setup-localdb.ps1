# DriftRide LocalDB Setup Script
# This PowerShell script initializes LocalDB for development

param(
    [switch]$Force,
    [string]$InstanceName = "MSSQLLocalDB",
    [string]$DatabaseName = "DriftRideDB_Dev"
)

Write-Host "Setting up LocalDB for DriftRide development..." -ForegroundColor Green

try {
    # Check if LocalDB is installed
    Write-Host "Checking LocalDB installation..." -ForegroundColor Yellow
    $localdbInfo = & SqlLocalDB.exe info
    if ($LASTEXITCODE -ne 0) {
        throw "LocalDB is not installed. Please install SQL Server Express LocalDB."
    }
    Write-Host "LocalDB is installed." -ForegroundColor Green

    # Check if instance exists
    Write-Host "Checking LocalDB instance: $InstanceName" -ForegroundColor Yellow
    $instanceExists = & SqlLocalDB.exe info $InstanceName 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating LocalDB instance: $InstanceName" -ForegroundColor Yellow
        & SqlLocalDB.exe create $InstanceName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create LocalDB instance: $InstanceName"
        }
    }
    Write-Host "LocalDB instance $InstanceName is available." -ForegroundColor Green

    # Start the instance
    Write-Host "Starting LocalDB instance: $InstanceName" -ForegroundColor Yellow
    & SqlLocalDB.exe start $InstanceName
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start LocalDB instance: $InstanceName"
    }
    Write-Host "LocalDB instance $InstanceName is running." -ForegroundColor Green

    # Get the script directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $sqlScript = Join-Path $scriptDir "init-localdb.sql"

    if (-not (Test-Path $sqlScript)) {
        throw "SQL initialization script not found: $sqlScript"
    }

    # Execute the initialization script
    Write-Host "Executing database initialization script..." -ForegroundColor Yellow
    $connectionString = "Data Source=(localdb)\$InstanceName;Integrated Security=True;Connect Timeout=30;"

    # Use sqlcmd to execute the script
    & sqlcmd -S "(localdb)\$InstanceName" -i $sqlScript -b
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to execute database initialization script"
    }

    Write-Host "Database initialization completed successfully!" -ForegroundColor Green

    # Test the connection
    Write-Host "Testing database connection..." -ForegroundColor Yellow
    $testConnectionString = "Data Source=(localdb)\$InstanceName;Initial Catalog=$DatabaseName;Integrated Security=True;"

    try {
        $connection = New-Object System.Data.SqlClient.SqlConnection($testConnectionString)
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT COUNT(*) FROM Auth.Roles"
        $roleCount = $command.ExecuteScalar()
        $connection.Close()

        Write-Host "Connection test successful. Found $roleCount default roles." -ForegroundColor Green
    }
    catch {
        Write-Warning "Connection test failed: $($_.Exception.Message)"
    }

    Write-Host "" -ForegroundColor White
    Write-Host "LocalDB Setup Complete!" -ForegroundColor Green
    Write-Host "Instance: (localdb)\$InstanceName" -ForegroundColor White
    Write-Host "Database: $DatabaseName" -ForegroundColor White
    Write-Host "Connection String: Data Source=(localdb)\$InstanceName;Initial Catalog=$DatabaseName;Integrated Security=True;" -ForegroundColor White

}
catch {
    Write-Error "Setup failed: $($_.Exception.Message)"
    exit 1
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Update your appsettings.Development.json with the connection string above" -ForegroundColor White
Write-Host "2. Run 'dotnet ef database update' from the API project to apply Entity Framework migrations" -ForegroundColor White
Write-Host "3. Start the API and Web projects for development" -ForegroundColor White