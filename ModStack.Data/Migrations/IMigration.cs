using Microsoft.Data.Sqlite;

namespace ModStack.Data.Migrations;

/// <summary>
/// Interface for database migrations
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Unique identifier for the migration (e.g., "001_AddDescriptionColumn")
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Human-readable description of what this migration does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Apply the migration to the database
    /// </summary>
    /// <param name="connection">Database connection</param>
    Task ApplyAsync(SqliteConnection connection);
}
