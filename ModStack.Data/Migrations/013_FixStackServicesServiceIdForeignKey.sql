-- Migration: Fix StackServices ServiceId foreign key constraint
-- Date: 2025-10-29
-- Description: Fix foreign key constraint in StackServices table that incorrectly references ServiceDefinitions instead of Services

-- This migration fixes the issue where the StackServices table was created with a foreign key pointing to ServiceDefinitions instead of Services

-- Step 1: Disable foreign key checks temporarily
PRAGMA foreign_keys = OFF;

-- Step 2: Create a backup of existing data
CREATE TABLE StackServices_backup AS SELECT * FROM StackServices;

-- Step 3: Drop the existing table with wrong foreign key constraint
DROP TABLE StackServices;

-- Step 4: Recreate the table with correct foreign key constraints
CREATE TABLE StackServices (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    StackId INTEGER NOT NULL,
    ServiceId INTEGER NOT NULL,
    Port INTEGER,
    Priority INTEGER DEFAULT 0,
    IsEnabled BOOLEAN DEFAULT TRUE,
    Configuration TEXT,
    FOREIGN KEY (StackId) REFERENCES Stacks(Id) ON DELETE CASCADE,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    UNIQUE(StackId, ServiceId)
);

-- Step 5: Restore data from backup
INSERT INTO StackServices SELECT * FROM StackServices_backup;

-- Step 6: Drop the backup table
DROP TABLE StackServices_backup;

-- Step 7: Re-enable foreign key checks
PRAGMA foreign_keys = ON;

