using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 003: Add ServiceName column to PackageVersions table
/// </summary>
public class Migration003_AddServiceNameToPackageVersions : IMigration
{
    public string Id => "003_AddServiceNameToPackageVersions";
    public string Description => "Add ServiceName column to PackageVersions table for service installation tracking";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE PackageVersions ADD COLUMN ServiceName TEXT";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR - column already exists
        {
            LogService.Info($"ℹ️ Migration {Id} skipped: column already exists");
        }
    }
}
