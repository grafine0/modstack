using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 002: Add PackageType column to Packages table
/// </summary>
public class Migration002_AddPackageTypeColumn : IMigration
{
    public string Id => "002_AddPackageTypeColumn";
    public string Description => "Add PackageType column to Packages table";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE Packages ADD COLUMN PackageType INTEGER DEFAULT 1";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // SQLITE_ERROR - column already exists
        {
            LogService.Info($"ℹ️ Migration {Id} skipped: column already exists");
        }
    }
}
