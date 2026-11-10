-- Migration: Add PackageType column to Packages table
-- Date: 2024-01-01
-- Description: Adds PackageType column to support different package installation types

-- Add PackageType column to Packages table
ALTER TABLE Packages ADD COLUMN PackageType INTEGER DEFAULT 1;

-- Update existing packages to have Service type (1) as default
UPDATE Packages SET PackageType = 1 WHERE PackageType IS NULL;

-- Create index for better performance on PackageType queries
CREATE INDEX idx_packages_type ON Packages(PackageType);
