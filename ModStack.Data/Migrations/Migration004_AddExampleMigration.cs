using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 004: Add example migration to demonstrate auto-discovery
/// </summary>
public class Migration004_AddExampleMigration : IMigration
{
    public string Id => "004_AddExampleMigration";
    public string Description => "Add example migration to demonstrate auto-discovery feature";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            // Example: Add a comment column to Packages table
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Packages ADD COLUMN Comment TEXT";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR - column already exists
        {
            LogService.Info($"ℹ️ Migration {Id} skipped: column already exists");
        }
    }
}
