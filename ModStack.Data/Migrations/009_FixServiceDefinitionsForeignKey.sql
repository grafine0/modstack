-- Migration: Fix ServiceDefinitions foreign key constraint
-- Date: 2025-10-25
-- Description: Fix foreign key constraint in InstalledPHPExtensions table that incorrectly references ServiceDefinitions instead of Services

-- This migration fixes the issue where Entity Framework created foreign keys pointing to ServiceDefinitions instead of Services

-- Step 1: Disable foreign key checks temporarily
PRAGMA foreign_keys = OFF;

-- Step 2: Create a backup of existing data (if any)
CREATE TABLE InstalledPHPExtensions_backup AS SELECT * FROM InstalledPHPExtensions;

-- Step 3: Drop the existing table with wrong foreign key constraint
DROP TABLE InstalledPHPExtensions;

-- Step 4: Recreate the table with correct foreign key constraints
CREATE TABLE InstalledPHPExtensions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ServiceId INTEGER NOT NULL,
    ExtensionId INTEGER NOT NULL,
    PHPVersion TEXT NOT NULL,
    InstallPath TEXT NOT NULL DEFAULT '',
    IsEnabled BOOLEAN NOT NULL DEFAULT 1,
    Configuration TEXT,
    InstalledAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ServiceId) REFERENCES Services(Id) ON DELETE CASCADE,
    FOREIGN KEY (ExtensionId) REFERENCES PHPExtensions(Id) ON DELETE CASCADE
);

-- Step 5: Restore data from backup
INSERT INTO InstalledPHPExtensions SELECT * FROM InstalledPHPExtensions_backup;

-- Step 6: Drop the backup table
DROP TABLE InstalledPHPExtensions_backup;

-- Step 7: Re-enable foreign key checks
PRAGMA foreign_keys = ON;
