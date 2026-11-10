using Microsoft.Data.Sqlite;
using ModStack.Logging;

namespace ModStack.Data.Migrations;

/// <summary>
/// Service responsible for running database migrations
/// </summary>
public class MigrationRunner
{
    private readonly IEnumerable<IMigration> _migrations;

    public MigrationRunner(IEnumerable<IMigration> migrations)
    {
        _migrations = migrations
            .OrderBy(m => int.TryParse(m.Id.Split('_')[0], out var id) ? id : int.MaxValue)
            .ThenBy(m => m.Id)
            .ToList();
    }

    /// <summary>
    /// Run all pending migrations
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task RunAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            LogService.Info("🔄 Starting database migrations...");
            
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            await EnsureMigrationsTableAsync(connection);

            var appliedCount = 0;
            var skippedCount = 0;

            foreach (var migration in _migrations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!await HasMigrationAsync(connection, migration.Id).ConfigureAwait(false))
                {
                    LogService.Info($"📦 Applying migration {migration.Id}: {migration.Description}");
                    
                    try
                    {
                        await migration.ApplyAsync(connection).ConfigureAwait(false);
                        await MarkMigrationAppliedAsync(connection, migration.Id).ConfigureAwait(false);
                        appliedCount++;
                        
                        LogService.Info($"✅ Migration {migration.Id} applied successfully");
                    }
                    catch (Exception ex)
                    {
                        LogService.Error($"❌ Failed to apply migration {migration.Id}: {migration.Description}", ex);
                        throw; // Re-throw to stop migration process
                    }
                }
                else
                {
                    LogService.Info($"⏭️ Migration {migration.Id} already applied, skipping");
                    skippedCount++;
                }
            }

            LogService.Info($"🎯 Migration process completed: {appliedCount} applied, {skippedCount} skipped");
        }
        catch (Exception ex)
        {
            LogService.Error("❌ Migration process failed", ex);
            throw;
        }
    }

    /// <summary>
    /// Ensure the migrations tracking table exists
    /// </summary>
    private static async Task EnsureMigrationsTableAsync(SqliteConnection connection)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS __Migrations (
                Id TEXT PRIMARY KEY,
                AppliedOn TEXT NOT NULL,
                Description TEXT
            )";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Check if a migration has already been applied
    /// </summary>
    private static async Task<bool> HasMigrationAsync(SqliteConnection connection, string id)
    {
        var sql = "SELECT COUNT(*) FROM __Migrations WHERE Id = @Id";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync().ConfigureAwait(false));
        return count > 0;
    }

    /// <summary>
    /// Mark a migration as applied
    /// </summary>
    private static async Task MarkMigrationAppliedAsync(SqliteConnection connection, string id)
    {
        var sql = "INSERT INTO __Migrations (Id, AppliedOn, Description) VALUES (@Id, @AppliedOn, @Description)";
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@AppliedOn", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Description", $"Migration {id}");
        
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Get list of applied migrations for debugging
    /// </summary>
    public async Task<List<string>> GetAppliedMigrationsAsync(string connectionString)
    {
        try
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var sql = "SELECT Id FROM __Migrations ORDER BY Id";
            
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            
            var migrations = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                migrations.Add(reader.GetString(0)); // Get by column index instead of name
            }
            
            return migrations;
        }
        catch (Exception ex)
        {
            LogService.Error("❌ Failed to get applied migrations list", ex);
            return new List<string>();
        }
    }
}
