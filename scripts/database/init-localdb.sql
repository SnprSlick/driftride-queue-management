-- DriftRide LocalDB Initialization Script
-- This script creates the initial database structure for development

USE master;
GO

-- Create the development database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DriftRideDB_Dev')
BEGIN
    CREATE DATABASE DriftRideDB_Dev;
    PRINT 'Created database: DriftRideDB_Dev';
END
ELSE
BEGIN
    PRINT 'Database DriftRideDB_Dev already exists';
END
GO

-- Switch to the development database
USE DriftRideDB_Dev;
GO

-- Create schemas for organization
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Racing')
BEGIN
    EXEC('CREATE SCHEMA Racing');
    PRINT 'Created schema: Racing';
END

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Auth')
BEGIN
    EXEC('CREATE SCHEMA Auth');
    PRINT 'Created schema: Auth';
END

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Analytics')
BEGIN
    EXEC('CREATE SCHEMA Analytics');
    PRINT 'Created schema: Analytics';
END
GO

-- Create initial tables for authentication
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND schema_id = SCHEMA_ID('Auth'))
BEGIN
    CREATE TABLE Auth.Users (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Email NVARCHAR(256) NOT NULL UNIQUE,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        FirstName NVARCHAR(100),
        LastName NVARCHAR(100),
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created table: Auth.Users';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles' AND schema_id = SCHEMA_ID('Auth'))
BEGIN
    CREATE TABLE Auth.Roles (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(50) NOT NULL UNIQUE,
        Description NVARCHAR(255),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created table: Auth.Roles';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoles' AND schema_id = SCHEMA_ID('Auth'))
BEGIN
    CREATE TABLE Auth.UserRoles (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        AssignedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        PRIMARY KEY (UserId, RoleId),
        FOREIGN KEY (UserId) REFERENCES Auth.Users(Id) ON DELETE CASCADE,
        FOREIGN KEY (RoleId) REFERENCES Auth.Roles(Id) ON DELETE CASCADE
    );
    PRINT 'Created table: Auth.UserRoles';
END
GO

-- Create initial tables for racing data
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Races' AND schema_id = SCHEMA_ID('Racing'))
BEGIN
    CREATE TABLE Racing.Races (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(255) NOT NULL,
        TrackName NVARCHAR(255),
        RaceDate DATETIME2 NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Scheduled',
        CreatedBy UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (CreatedBy) REFERENCES Auth.Users(Id)
    );
    PRINT 'Created table: Racing.Races';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Participants' AND schema_id = SCHEMA_ID('Racing'))
BEGIN
    CREATE TABLE Racing.Participants (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        RaceId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CarModel NVARCHAR(255),
        RegistrationTime DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Status NVARCHAR(50) NOT NULL DEFAULT 'Registered',
        FOREIGN KEY (RaceId) REFERENCES Racing.Races(Id) ON DELETE CASCADE,
        FOREIGN KEY (UserId) REFERENCES Auth.Users(Id)
    );
    PRINT 'Created table: Racing.Participants';
END
GO

-- Insert default roles
IF NOT EXISTS (SELECT * FROM Auth.Roles WHERE Name = 'Admin')
BEGIN
    INSERT INTO Auth.Roles (Name, Description) VALUES ('Admin', 'System Administrator with full access');
    PRINT 'Inserted default role: Admin';
END

IF NOT EXISTS (SELECT * FROM Auth.Roles WHERE Name = 'RaceOrganizer')
BEGIN
    INSERT INTO Auth.Roles (Name, Description) VALUES ('RaceOrganizer', 'Can create and manage races');
    PRINT 'Inserted default role: RaceOrganizer';
END

IF NOT EXISTS (SELECT * FROM Auth.Roles WHERE Name = 'Participant')
BEGIN
    INSERT INTO Auth.Roles (Name, Description) VALUES ('Participant', 'Can join races and view results');
    PRINT 'Inserted default role: Participant';
END
GO

-- Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Email')
BEGIN
    CREATE INDEX IX_Users_Email ON Auth.Users(Email);
    PRINT 'Created index: IX_Users_Email';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Races_RaceDate')
BEGIN
    CREATE INDEX IX_Races_RaceDate ON Racing.Races(RaceDate);
    PRINT 'Created index: IX_Races_RaceDate';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Participants_RaceId')
BEGIN
    CREATE INDEX IX_Participants_RaceId ON Racing.Participants(RaceId);
    PRINT 'Created index: IX_Participants_RaceId';
END
GO

PRINT 'Database initialization completed successfully!';