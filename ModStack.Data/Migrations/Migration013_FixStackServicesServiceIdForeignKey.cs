using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 013: Fix StackServices ServiceId foreign key constraint
/// </summary>
public class Migration013_FixStackServicesServiceIdForeignKey : IMigration
{
    public string Id => "013_FixStackServicesServiceIdForeignKey";
    public string Description => "Fix foreign key constraint in StackServices table that incorrectly references ServiceDefinitions instead of Services";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            // Check if the wrong foreign key constraint exists
            var foreignKeys = await connection.QueryAsync<dynamic>("PRAGMA foreign_key_list(StackServices)");
            var hasWrongConstraint = foreignKeys.Any(fk => fk.table == "ServiceDefinitions");
            
            if (!hasWrongConstraint)
            {
                LogService.Info($"ℹ️ Migration {Id} skipped: foreign key constraint is already correct");
                return;
            }

            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Step 1: Disable foreign key checks temporarily
            using var cmd1 = connection.CreateCommand();
            cmd1.CommandText = "PRAGMA foreign_keys = OFF";
            await cmd1.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 2: Create a backup of existing data
            using var cmd2 = connection.CreateCommand();
            cmd2.CommandText = "CREATE TABLE StackServices_backup AS SELECT * FROM StackServices";
            await cmd2.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 3: Drop the existing table with wrong foreign key constraint
            using var cmd3 = connection.CreateCommand();
            cmd3.CommandText = "DROP TABLE StackServices";
            await cmd3.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 4: Recreate the table with correct foreign key constraints
            using var cmd4 = connection.CreateCommand();
            cmd4.CommandText = @"
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
                )";
            await cmd4.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 5: Restore data from backup
            using var cmd5 = connection.CreateCommand();
            cmd5.CommandText = "INSERT INTO StackServices SELECT * FROM StackServices_backup";
            await cmd5.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 6: Drop the backup table
            using var cmd6 = connection.CreateCommand();
            cmd6.CommandText = "DROP TABLE StackServices_backup";
            await cmd6.ExecuteNonQueryAsync().ConfigureAwait(false);

            // Step 7: Re-enable foreign key checks
            using var cmd7 = connection.CreateCommand();
            cmd7.CommandText = "PRAGMA foreign_keys = ON";
            await cmd7.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}: {Description}", ex);
            throw;
        }
    }
}

