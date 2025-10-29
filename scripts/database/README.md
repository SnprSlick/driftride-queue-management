# DriftRide Database Setup

This directory contains scripts to initialize the DriftRide database for development.

## Files

- `init-localdb.sql` - SQL script that creates the database schema, tables, and initial data
- `setup-localdb.ps1` - PowerShell script for Windows LocalDB setup
- `setup-localdb.sh` - Bash script for macOS/Linux using Docker SQL Server

## Windows Setup (LocalDB)

Run the PowerShell script as Administrator:

```powershell
.\setup-localdb.ps1
```

This will:
1. Check if LocalDB is installed
2. Create/start the MSSQLLocalDB instance
3. Execute the SQL initialization script
4. Test the connection

## macOS/Linux Setup (Docker)

Run the bash script:

```bash
./setup-localdb.sh
```

This will:
1. Check if Docker is running
2. Pull and run SQL Server 2022 in Docker
3. Execute the SQL initialization script
4. Test the connection

## Connection Strings

### Windows LocalDB
```
Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=DriftRideDB_Dev;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False
```

### Docker SQL Server (macOS/Linux)
```
Server=localhost,1433;Database=DriftRideDB_Dev;User Id=sa;Password=DriftRide123!;TrustServerCertificate=true;
```

## Database Schema

The initialization script creates:

### Schemas
- `Auth` - Authentication and authorization tables
- `Racing` - Race and participant data
- `Analytics` - Performance and analytics data

### Tables
- `Auth.Users` - User accounts
- `Auth.Roles` - User roles (Admin, RaceOrganizer, Participant)
- `Auth.UserRoles` - User-role assignments
- `Racing.Races` - Race events
- `Racing.Participants` - Race participants

### Default Roles
- **Admin** - System Administrator with full access
- **RaceOrganizer** - Can create and manage races
- **Participant** - Can join races and view results

## Next Steps

1. Update `appsettings.Development.json` with the appropriate connection string
2. Install Entity Framework tools if not already installed:
   ```bash
   dotnet tool install --global dotnet-ef
   ```
3. Run Entity Framework migrations from the API project:
   ```bash
   cd backend/DriftRide.Api
   dotnet ef database update
   ```
4. Start the development servers

## Troubleshooting

### Windows LocalDB Issues
- Ensure SQL Server Express LocalDB is installed
- Run PowerShell as Administrator
- Check Windows services for SQL Server instances

### Docker Issues
- Ensure Docker Desktop is running
- Check if port 1433 is available
- Verify container is running: `docker ps`
- View logs: `docker logs driftride-sqlserver`

### Connection Issues
- Verify connection strings match your setup
- Check firewall settings
- Ensure SQL Server is accepting connections