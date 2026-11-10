using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 008: Remove ActualPath and DefaultPath columns from Services table
/// </summary>
public class Migration008_RemoveActualPathAndDefaultPath : IMigration
{
    public string Id => "008_RemoveActualPathAndDefaultPath";
    public string Description => "Remove ActualPath, DefaultPath, ActualVersion, DefaultVersion, InstalledAt, and Metadata columns from Services table";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            // Check if ActualPath, DefaultPath, ActualVersion, DefaultVersion, InstalledAt, or Metadata columns exist
            var columns = await GetColumnsAsync(connection, "Services");
            bool hasActualPath = columns.Any(c => c.Equals("actualpath", StringComparison.OrdinalIgnoreCase));
            bool hasDefaultPath = columns.Any(c => c.Equals("defaultpath", StringComparison.OrdinalIgnoreCase));
            bool hasActualVersion = columns.Any(c => c.Equals("actualversion", StringComparison.OrdinalIgnoreCase));
            bool hasDefaultVersion = columns.Any(c => c.Equals("defaultversion", StringComparison.OrdinalIgnoreCase));
            bool hasInstalledAt = columns.Any(c => c.Equals("installedat", StringComparison.OrdinalIgnoreCase));
            bool hasMetadata = columns.Any(c => c.Equals("metadata", StringComparison.OrdinalIgnoreCase));

            if (!hasActualPath && !hasDefaultPath && !hasActualVersion && !hasDefaultVersion && !hasInstalledAt && !hasMetadata)
            {
                LogService.Info($"ℹ️ Migration {Id} skipped: columns do not exist");
                return;
            }

            // SQLite doesn't support DROP COLUMN, so we need to:
            // 1. Create a new table without the columns
            // 2. Copy data (excluding those columns)
            // 3. Drop old table
            // 4. Rename new table

            LogService.Info("🔄 Starting column removal migration...");

            // Step 1: Create new table without ActualPath, DefaultPath, ActualVersion, DefaultVersion, InstalledAt, and Metadata
            var createTableSql = @"
                CREATE TABLE Services_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    ServiceType INTEGER NOT NULL,
                    Version TEXT NOT NULL,
                    BinaryPath TEXT NOT NULL,
                    ConfigPath TEXT,
                    DefaultPort INTEGER,
                    IsInstalled INTEGER NOT NULL DEFAULT 0,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    InstallDate TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    Description TEXT,
                    Status INTEGER NOT NULL DEFAULT 0,
                    AutoStart INTEGER NOT NULL DEFAULT 0,
                    Configuration TEXT,
                    Dependencies TEXT,
                    UniqueId TEXT,
                    WindowsServiceName TEXT
                )";

            await connection.ExecuteAsync(createTableSql);
            LogService.Info("✅ Created new Services table");

            // Step 2: Copy data (excluding ActualPath, DefaultPath, ActualVersion, DefaultVersion, InstalledAt, and Metadata)
            var selectColumns = string.Join(", ", columns
                .Where(c => !c.Equals("actualpath", StringComparison.OrdinalIgnoreCase) && !c.Equals("defaultpath", StringComparison.OrdinalIgnoreCase) && 
                           !c.Equals("actualversion", StringComparison.OrdinalIgnoreCase) && !c.Equals("defaultversion", StringComparison.OrdinalIgnoreCase) &&
                           !c.Equals("installedat", StringComparison.OrdinalIgnoreCase) && !c.Equals("metadata", StringComparison.OrdinalIgnoreCase))
                .Select(c => $"[{c}]"));

            var insertSql = $@"
                INSERT INTO Services_New ({selectColumns})
                SELECT {selectColumns}
                FROM Services";

            await connection.ExecuteAsync(insertSql);
            LogService.Info("✅ Copied data to new table");

            // Step 3: Drop old table
            await connection.ExecuteAsync("DROP TABLE Services");
            LogService.Info("✅ Dropped old Services table");

            // Step 4: Rename new table
            await connection.ExecuteAsync("ALTER TABLE Services_New RENAME TO Services");
            LogService.Info("✅ Renamed new table to Services");

            // Recreate indexes and constraints if needed
            await connection.ExecuteAsync(@"
                CREATE INDEX IF NOT EXISTS IX_Services_Name_Version ON Services(Name, Version);
                CREATE INDEX IF NOT EXISTS IX_Services_ServiceType ON Services(ServiceType);
            ");
            LogService.Info("✅ Recreated indexes");

            LogService.Info($"✅ Migration {Id} completed successfully");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}", ex);
            throw;
        }
    }

    private static async Task<List<string>> GetColumnsAsync(SqliteConnection connection, string tableName)
    {
        var sql = $"PRAGMA table_info({tableName})";
        var result = await connection.QueryAsync<dynamic>(sql);
        return [.. result.Select(r => (string)r.name)];
    }
}
