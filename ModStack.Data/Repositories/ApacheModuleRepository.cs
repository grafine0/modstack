using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

namespace ModStack.Data.Repositories
{
    /// <summary>
    /// Repository for Apache module data operations
    /// </summary>
    public class ApacheModuleRepository : IApacheModuleRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ApacheModuleRepository> _logger;

        public ApacheModuleRepository(string connectionString, ILogger<ApacheModuleRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<IEnumerable<ApacheModule>> GetAllAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Ensure ApacheModules table exists
                await EnsureTableExistsAsync(connection);
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    ORDER BY Category, Name";
                
                var modules = await connection.QueryAsync<ApacheModule>(sql);
                _logger.LogDebug("Retrieved {Count} Apache modules from database", modules.Count());
                return modules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Apache modules from database");
                return Enumerable.Empty<ApacheModule>();
            }
        }

        /// <summary>
        /// Ensures the ApacheModules table exists, creates it if it doesn't
        /// </summary>
        private async Task EnsureTableExistsAsync(SqliteConnection connection)
        {
            try
            {
                // Check if table exists
                const string checkTableSql = @"
                    SELECT COUNT(*) 
                    FROM sqlite_master 
                    WHERE type='table' AND name='ApacheModules'";
                
                var tableExists = await connection.QuerySingleAsync<long>(checkTableSql) > 0;
                
                if (!tableExists)
                {
                    _logger.LogInformation("ApacheModules table not found, creating it...");
                    
                    // Create table
                    const string createTableSql = @"
                        CREATE TABLE ApacheModules (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            ModuleName TEXT NOT NULL UNIQUE,
                            FileName TEXT NOT NULL,
                            Description TEXT,
                            Category TEXT,
                            IsEnabled BOOLEAN DEFAULT FALSE,
                            IsRequired BOOLEAN DEFAULT FALSE,
                            IsAvailable BOOLEAN DEFAULT TRUE,
                            ApacheVersion TEXT,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        );";
                    
                    await connection.ExecuteAsync(createTableSql);
                    
                    // Create indexes
                    const string createIndexesSql = @"
                        CREATE INDEX idx_apache_modules_category ON ApacheModules(Category);
                        CREATE INDEX idx_apache_modules_enabled ON ApacheModules(IsEnabled);
                        CREATE INDEX idx_apache_modules_version ON ApacheModules(ApacheVersion);";
                    
                    await connection.ExecuteAsync(createIndexesSql);
                    
                    _logger.LogInformation("Successfully created ApacheModules table and indexes");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring ApacheModules table exists");
                throw;
            }
        }

        public async Task<IEnumerable<ApacheModule>> GetByCategoryAsync(string category)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE Category = @Category 
                    ORDER BY Name";
                
                var modules = await connection.QueryAsync<ApacheModule>(sql, new { Category = category });
                return modules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Apache modules by category {Category}", category);
                return Enumerable.Empty<ApacheModule>();
            }
        }

        public async Task<IEnumerable<ApacheModule>> GetEnabledAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE IsEnabled = 1 
                    ORDER BY Category, Name";
                
                var modules = await connection.QueryAsync<ApacheModule>(sql);
                return modules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enabled Apache modules");
                return Enumerable.Empty<ApacheModule>();
            }
        }

        public async Task<IEnumerable<ApacheModule>> GetAvailableAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE IsAvailable = 1 
                    ORDER BY Category, Name";
                
                var modules = await connection.QueryAsync<ApacheModule>(sql);
                return modules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available Apache modules");
                return Enumerable.Empty<ApacheModule>();
            }
        }

        public async Task<ApacheModule?> GetByModuleNameAsync(string moduleName)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE ModuleName = @ModuleName";
                
                var module = await connection.QueryFirstOrDefaultAsync<ApacheModule>(sql, new { ModuleName = moduleName });
                return module;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Apache module by name {ModuleName}", moduleName);
                return null;
            }
        }

        public async Task<ApacheModule?> GetByIdAsync(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE Id = @Id";
                
                var module = await connection.QueryFirstOrDefaultAsync<ApacheModule>(sql, new { Id = id });
                return module;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Apache module by ID {Id}", id);
                return null;
            }
        }

        public async Task<ApacheModule> CreateAsync(ApacheModule module)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // Ensure ApacheModules table exists
                await EnsureTableExistsAsync(connection);
                
                const string sql = @"
                    INSERT INTO ApacheModules (Name, ModuleName, FileName, Description, Category, 
                                             IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                                             CreatedAt, UpdatedAt)
                    VALUES (@Name, @ModuleName, @FileName, @Description, @Category, 
                            @IsEnabled, @IsRequired, @IsAvailable, @ApacheVersion, 
                            @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();";
                
                var id = await connection.QuerySingleAsync<long>(sql, module);
                module.Id = (int)id;
                _logger.LogInformation("Created Apache module {ModuleName} with ID {Id}", module.ModuleName, module.Id);
                return module;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Apache module {ModuleName}", module.ModuleName);
                throw;
            }
        }

        public async Task<ApacheModule> UpdateAsync(ApacheModule module)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                module.UpdatedAt = DateTime.Now;
                
                const string sql = @"
                    UPDATE ApacheModules 
                    SET Name = @Name, ModuleName = @ModuleName, FileName = @FileName, 
                        Description = @Description, Category = @Category, 
                        IsEnabled = @IsEnabled, IsRequired = @IsRequired, 
                        IsAvailable = @IsAvailable, ApacheVersion = @ApacheVersion, 
                        UpdatedAt = @UpdatedAt
                    WHERE Id = @Id";
                
                var rowsAffected = await connection.ExecuteAsync(sql, module);
                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Apache module with ID {module.Id} not found");
                }
                
                _logger.LogInformation("Updated Apache module {ModuleName}", module.ModuleName);
                return module;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Apache module {ModuleName}", module.ModuleName);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(long id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = "DELETE FROM ApacheModules WHERE Id = @Id";
                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
                
                var success = rowsAffected > 0;
                if (success)
                {
                    _logger.LogInformation("Deleted Apache module with ID {Id}", id);
                }
                else
                {
                    _logger.LogWarning("Apache module with ID {Id} not found for deletion", id);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Apache module with ID {Id}", id);
                return false;
            }
        }

        public async Task<bool> UpdateEnabledStatusAsync(long id, bool isEnabled)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    UPDATE ApacheModules 
                    SET IsEnabled = @IsEnabled, UpdatedAt = @UpdatedAt 
                    WHERE Id = @Id";
                
                var rowsAffected = await connection.ExecuteAsync(sql, new 
                { 
                    Id = id, 
                    IsEnabled = isEnabled, 
                    UpdatedAt = DateTime.Now 
                });
                
                var success = rowsAffected > 0;
                if (success)
                {
                    _logger.LogInformation("Updated Apache module {Id} enabled status to {IsEnabled}", id, isEnabled);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Apache module {Id} enabled status", id);
                return false;
            }
        }

        public async Task<bool> UpdateAvailabilityStatusAsync(long id, bool isAvailable)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    UPDATE ApacheModules 
                    SET IsAvailable = @IsAvailable, UpdatedAt = @UpdatedAt 
                    WHERE Id = @Id";
                
                var rowsAffected = await connection.ExecuteAsync(sql, new 
                { 
                    Id = id, 
                    IsAvailable = isAvailable, 
                    UpdatedAt = DateTime.Now 
                });
                
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Apache module {Id} availability status", id);
                return false;
            }
        }

        public async Task<IEnumerable<string>> GetCategoriesAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT DISTINCT Category 
                    FROM ApacheModules 
                    WHERE Category IS NOT NULL 
                    ORDER BY Category";
                
                var categories = await connection.QueryAsync<string>(sql);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Apache module categories");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<ApacheModule>> SearchAsync(string searchTerm)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                const string sql = @"
                    SELECT Id, Name, ModuleName, FileName, Description, Category, 
                           IsEnabled, IsRequired, IsAvailable, ApacheVersion, 
                           CreatedAt, UpdatedAt 
                    FROM ApacheModules 
                    WHERE Name LIKE @SearchTerm 
                       OR ModuleName LIKE @SearchTerm 
                       OR Description LIKE @SearchTerm 
                    ORDER BY Category, Name";
                
                var searchPattern = $"%{searchTerm}%";
                var modules = await connection.QueryAsync<ApacheModule>(sql, new { SearchTerm = searchPattern });
                return modules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Apache modules with term {SearchTerm}", searchTerm);
                return Enumerable.Empty<ApacheModule>();
            }
        }
    }
}
