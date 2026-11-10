using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 014: Create InterceptedEmails table
/// </summary>
public class Migration014_CreateInterceptedEmailsTable : IMigration
{
    public string Id => "014_CreateInterceptedEmailsTable";
    public string Description => "Create InterceptedEmails table for Mail Catcher feature";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            // Check if the table already exists
            var checkTableSql = "SELECT name FROM sqlite_master WHERE type='table' AND name='InterceptedEmails'";
            var tableExists = await connection.ExecuteScalarAsync<string>(checkTableSql).ConfigureAwait(false);
            
            if (tableExists != null)
            {
                LogService.Info($"ℹ️ Migration {Id} skipped: InterceptedEmails table already exists");
                return;
            }

            LogService.Info($"🔧 Applying migration {Id}: {Description}");

            // Create InterceptedEmails table
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE InterceptedEmails (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MessageId TEXT,
                    Sender TEXT NOT NULL,
                    Recipient TEXT NOT NULL,
                    Subject TEXT,
                    BodyHtml TEXT,
                    BodyText TEXT,
                    RawHeaders TEXT,
                    ReceivedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    IsRead BOOLEAN DEFAULT FALSE
                )";
            
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}: {Description}", ex);
            throw;
        }
    }
}
