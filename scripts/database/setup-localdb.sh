#!/bin/bash

# DriftRide LocalDB Setup Script for macOS/Linux using Docker
# This script sets up SQL Server in Docker for development since LocalDB is Windows-only

set -e

CONTAINER_NAME="driftride-sqlserver"
SA_PASSWORD="DriftRide123!"
DATABASE_NAME="DriftRideDB_Dev"
PORT="1433"

echo "Setting up SQL Server for DriftRide development using Docker..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "Error: Docker is not running. Please start Docker and try again."
    exit 1
fi

# Stop and remove existing container if it exists
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Stopping and removing existing container: $CONTAINER_NAME"
    docker stop $CONTAINER_NAME || true
    docker rm $CONTAINER_NAME || true
fi

# Pull SQL Server image
echo "Pulling SQL Server Docker image..."
docker pull mcr.microsoft.com/mssql/server:2022-latest

# Run SQL Server container
echo "Starting SQL Server container: $CONTAINER_NAME"
docker run -e "ACCEPT_EULA=Y" \
    -e "MSSQL_SA_PASSWORD=$SA_PASSWORD" \
    -p $PORT:1433 \
    --name $CONTAINER_NAME \
    --restart unless-stopped \
    -d mcr.microsoft.com/mssql/server:2022-latest

# Wait for SQL Server to start
echo "Waiting for SQL Server to start..."
sleep 30

# Test connection
echo "Testing SQL Server connection..."
for i in {1..10}; do
    if docker exec $CONTAINER_NAME /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" > /dev/null 2>&1; then
        echo "SQL Server is ready!"
        break
    fi
    echo "Waiting for SQL Server... (attempt $i/10)"
    sleep 5
done

# Get the directory of this script
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
SQL_SCRIPT="$SCRIPT_DIR/init-localdb.sql"

if [ ! -f "$SQL_SCRIPT" ]; then
    echo "Error: SQL initialization script not found: $SQL_SCRIPT"
    exit 1
fi

# Execute the initialization script
echo "Executing database initialization script..."
docker exec -i $CONTAINER_NAME /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" < "$SQL_SCRIPT"

if [ $? -eq 0 ]; then
    echo "Database initialization completed successfully!"
else
    echo "Error: Database initialization failed"
    exit 1
fi

# Test the database
echo "Testing database connection..."
ROLE_COUNT=$(docker exec $CONTAINER_NAME /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -d "$DATABASE_NAME" -h -1 -Q "SELECT COUNT(*) FROM Auth.Roles" | tr -d ' \r\n')

echo "Connection test successful. Found $ROLE_COUNT default roles."

echo ""
echo "SQL Server Setup Complete!"
echo "Container: $CONTAINER_NAME"
echo "Database: $DATABASE_NAME"
echo "Port: $PORT"
echo "SA Password: $SA_PASSWORD"
echo "Connection String: Server=localhost,$PORT;Database=$DATABASE_NAME;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=true;"

echo ""
echo "Next steps:"
echo "1. Update your appsettings.Development.json with the connection string above"
echo "2. Run 'dotnet ef database update' from the API project to apply Entity Framework migrations"
echo "3. Start the API and Web projects for development"
echo ""
echo "To stop the SQL Server container: docker stop $CONTAINER_NAME"
echo "To start the SQL Server container: docker start $CONTAINER_NAME"