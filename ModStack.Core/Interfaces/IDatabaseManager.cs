using ModStack.Common.Models;

namespace ModStack.Core.Interfaces
{
    /// <summary>
    /// Interface for managing database operations
    /// </summary>
    public interface IDatabaseManager
    {
        /// <summary>
        /// Creates a new database
        /// </summary>
        Task<bool> CreateDatabaseAsync(string databaseName, string host = "localhost", int port = 3306, string? username = null, string? password = null);

        /// <summary>
        /// Drops a database
        /// </summary>
        Task<bool> DropDatabaseAsync(string databaseName, string host = "localhost", int port = 3306, string? username = null, string? password = null);

        /// <summary>
        /// Checks if a database exists
        /// </summary>
        Task<bool> DatabaseExistsAsync(string databaseName, string host = "localhost", int port = 3306, string? username = null, string? password = null);

        /// <summary>
        /// Creates a new database user
        /// </summary>
        Task<bool> CreateUserAsync(string username, string password, string host = "localhost", int port = 3306);

        /// <summary>
        /// Drops a database user
        /// </summary>
        Task<bool> DropUserAsync(string username, string host = "localhost", int port = 3306);

        /// <summary>
        /// Grants database access to a user
        /// </summary>
        Task<bool> GrantDatabaseAccessAsync(string username, string databaseName, string host = "localhost", int port = 3306);

        /// <summary>
        /// Revokes database access from a user
        /// </summary>
        Task<bool> RevokeDatabaseAccessAsync(string username, string databaseName, string host = "localhost", int port = 3306);

        /// <summary>
        /// Tests database connection
        /// </summary>
        Task<bool> TestConnectionAsync(string host = "localhost", int port = 3306, string? username = null, string? password = null);

        /// <summary>
        /// Gets all databases
        /// </summary>
        Task<List<string>> GetDatabasesAsync(string host = "localhost", int port = 3306, string? username = null, string? password = null);

        /// <summary>
        /// Gets all users
        /// </summary>
        Task<List<string>> GetUsersAsync(string host = "localhost", int port = 3306, string? username = null, string? password = null);
    }
}
