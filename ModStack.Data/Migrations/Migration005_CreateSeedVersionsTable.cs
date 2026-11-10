using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 005: Create SeedVersions table for tracking seed data versions
/// </summary>
public class Migration005_CreateSeedVersionsTable : IMigration
{
    public string Id => "005_CreateSeedVersionsTable";
    public string Description => "Create SeedVersions table for tracking seed data versions";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS SeedVersions (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Domain TEXT NOT NULL DEFAULT 'global',
                            Version TEXT NOT NULL,
                            AppliedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
                        )";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR - table already exists
        {
            LogService.Info($"ℹ️ Migration {Id} skipped: table already exists");
        }
    }
}
