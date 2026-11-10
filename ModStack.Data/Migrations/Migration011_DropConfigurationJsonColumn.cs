using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 011: Drop unused ConfigurationJson column from Services table
/// </summary>
public class Migration011_DropConfigurationJsonColumn : IMigration
{
    public string Id => "011_DropConfigurationJsonColumn";
    public string Description => "Drop unused ConfigurationJson column and consolidate to Configuration column";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Check if ConfigurationJson column exists
            var checkColumnSql = @"
                SELECT COUNT(*) 
                FROM pragma_table_info('Services') 
                WHERE name = 'ConfigurationJson'";
            
            var columnExists = await connection.QuerySingleAsync<long>(checkColumnSql);
            
            if (columnExists == 0)
            {
                LogService.Info($"⏭️ Migration {Id} skipped: ConfigurationJson column doesn't exist");
                return;
            }

            // SQLite doesn't support DROP COLUMN directly in older versions
            // We need to recreate the table without the ConfigurationJson column
            
            // Step 1: Create new table without ConfigurationJson
            await connection.ExecuteAsync(@"
                CREATE TABLE Services_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    Description TEXT,
                    ServiceType INTEGER NOT NULL,
                    Version TEXT NOT NULL,
                    BinaryPath TEXT NOT NULL,
                    ConfigPath TEXT,
                    DefaultPort INTEGER,
                    IsInstalled INTEGER NOT NULL DEFAULT 0,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    InstallDate TEXT,
                    Status INTEGER NOT NULL DEFAULT 0,
                    AutoStart INTEGER NOT NULL DEFAULT 0,
                    Configuration TEXT,
                    Dependencies TEXT,
                    UniqueId TEXT,
                    WindowsServiceName TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )");

            LogService.Info("✅ Created new Services table without ConfigurationJson column");

            // Step 2: Copy data from old table to new table
            await connection.ExecuteAsync(@"
                INSERT INTO Services_New 
                    (Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath, 
                     DefaultPort, IsInstalled, IsEnabled, InstallDate, Status, AutoStart, Configuration, 
                     Dependencies, UniqueId, WindowsServiceName, CreatedAt, UpdatedAt)
                SELECT 
                    Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                    DefaultPort, IsInstalled, IsEnabled, InstallDate, Status, AutoStart, Configuration,
                    Dependencies, UniqueId, WindowsServiceName, CreatedAt, UpdatedAt
                FROM Services");

            LogService.Info("✅ Copied all data to new Services table");

            // Step 3: Drop old table
            await connection.ExecuteAsync("DROP TABLE Services");
            LogService.Info("✅ Dropped old Services table");

            // Step 4: Rename new table to Services
            await connection.ExecuteAsync("ALTER TABLE Services_New RENAME TO Services");
            LogService.Info("✅ Renamed Services_New to Services");

            // Step 5: Recreate indexes
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS idx_services_name ON Services(Name);
                CREATE INDEX IF NOT EXISTS idx_services_isinstalled ON Services(IsInstalled);
                CREATE INDEX IF NOT EXISTS idx_services_servicetype ON Services(ServiceType);
                CREATE UNIQUE INDEX IF NOT EXISTS idx_services_uniqueid ON Services(UniqueId);");

            LogService.Info("✅ Recreated indexes on Services table");

            LogService.Info($"✅ Migration {Id} applied successfully: ConfigurationJson column removed");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Error applying migration {Id}: {ex.Message}", ex);
            throw;
        }
    }
}

