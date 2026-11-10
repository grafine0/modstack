using Microsoft.Data.Sqlite;
using ModStack.Logging;
using Dapper;

namespace ModStack.Data.Migrations;

/// <summary>
/// Migration 007: Add missing Description and AutoStart columns to Services table
/// </summary>
public class Migration007_AddMissingColumnsToServices : IMigration
{
    public string Id => "007_AddMissingColumnsToServices";
    public string Description => "Add missing Description and AutoStart columns to Services table";

    public async Task ApplyAsync(SqliteConnection connection)
    {
        try
        {
            // Check if Description column exists
            var descriptionExists = await ColumnExistsAsync(connection, "Services", "Description");
            if (!descriptionExists)
            {
                await connection.ExecuteAsync("ALTER TABLE Services ADD COLUMN Description TEXT;");
                LogService.Info("Added Description column to Services table");
            }

            // Check if AutoStart column exists
            var autoStartExists = await ColumnExistsAsync(connection, "Services", "AutoStart");
            if (!autoStartExists)
            {
                await connection.ExecuteAsync("ALTER TABLE Services ADD COLUMN AutoStart BOOLEAN DEFAULT FALSE;");
                LogService.Info("Added AutoStart column to Services table");
            }

            LogService.Info($"✅ Applied migration {Id}: {Description}");
        }
        catch (Exception ex)
        {
            LogService.Error($"❌ Failed to apply migration {Id}: {Description}", ex);
            throw;
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName)
    {
        var sql = "PRAGMA table_info(" + tableName + ");";
        var columns = await connection.QueryAsync(sql);
        
        return columns.Any(c => c.name == columnName);
    }
}
