using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ModStack.Data.Repositories;

/// <summary>
/// Repository implementation for service definition operations using Dapper
/// </summary>
public class ServiceRepository : IServiceRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ServiceRepository> _logger;

    public ServiceRepository(string connectionString, ILogger<ServiceRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<IEnumerable<ServiceDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, ServiceType, Version, BinaryPath, ConfigPath, DefaultPort,
                   IsInstalled, IsEnabled, InstallDate, CreatedAt, UpdatedAt, Description,
                   Status, AutoStart, Configuration, Dependencies, UniqueId, WindowsServiceName
            FROM Services 
            ORDER BY DisplayName";
        
        var services = await connection.QueryAsync<ServiceDefinition>(sql);
        return services;
    }

    public async Task<IEnumerable<ServiceDefinition>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                   DefaultPort, IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt
            FROM Services 
            WHERE IsEnabled = 1
            ORDER BY DisplayName";
        
        var services = await connection.QueryAsync<ServiceDefinition>(sql);
        return services;
    }

    public async Task<IEnumerable<ServiceDefinition>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, ServiceType, Version, BinaryPath, ConfigPath, DefaultPort,
                   IsInstalled, IsEnabled, InstallDate, CreatedAt, UpdatedAt, Description,
                   Status, AutoStart, Configuration, Dependencies, UniqueId, WindowsServiceName
            FROM Services 
            WHERE IsInstalled = 1 
            ORDER BY DisplayName";
        
        var services = await connection.QueryAsync<ServiceDefinition>(sql);
        return services;
    }

    public async Task<ServiceDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, ServiceType, Version, BinaryPath, ConfigPath, DefaultPort,
                   IsInstalled, IsEnabled, InstallDate, CreatedAt, UpdatedAt, Description,
                   Status, AutoStart, Configuration, Dependencies, UniqueId, WindowsServiceName
            FROM Services 
            WHERE Id = @Id";
        
        var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(sql, new { Id = id });
        return service;
    }

    public async Task<ServiceDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("GetByNameAsync called with null or empty name");
            return null;
        }
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                   DefaultPort, IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt
            FROM Services 
            WHERE Name = @Name";
        
        var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(sql, new { Name = name.Trim() });
        return service;
    }

    public async Task<ServiceDefinition?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
        {
            _logger.LogWarning("GetByNameAndVersionAsync called with null or empty name or version");
            return null;
        }
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                   DefaultPort, IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt
            FROM Services 
            WHERE Name = @Name AND Version = @Version";
        
        var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(sql, new { Name = name.Trim(), Version = version.Trim() });
        return service;
    }

    public async Task<ServiceDefinition?> GetByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            SELECT Id, Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                   DefaultPort, IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt
            FROM Services 
            WHERE ServiceType = @ServiceType";
        
        var service = await connection.QuerySingleOrDefaultAsync<ServiceDefinition>(sql, new { ServiceType = serviceType.ToString() });
        return service;
    }

    public async Task<ServiceDefinition> AddAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        service.CreatedAt = DateTime.UtcNow;
        service.UpdatedAt = DateTime.UtcNow;
        
        var sql = @"
            INSERT INTO Services (Name, DisplayName, Description, ServiceType, Version, BinaryPath, ConfigPath,
                                DefaultPort, IsInstalled, IsEnabled, InstallDate, AutoStart, CreatedAt, UpdatedAt)
            VALUES (@Name, @DisplayName, @Description, @ServiceType, @Version, @BinaryPath, @ConfigPath,
                   @DefaultPort, @IsInstalled, @IsEnabled, @InstallDate, @AutoStart, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";
        
        var id = await connection.QuerySingleAsync<long>(sql, service);
        service.Id = id;
        
        _logger.LogInformation("Service {ServiceName} added (ID: {ServiceId})", service.Name, service.Id);
        
        return service;
    }

    public async Task<ServiceDefinition> CreateAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        return await AddAsync(service, cancellationToken);
    }

    /// <summary>
    /// Adds a service to the context without saving changes immediately.
    /// Call SaveChangesAsync() on the DbContext to persist changes.
    /// </summary>
    public void Add(ServiceDefinition service)
    {
        // This method is kept for compatibility but doesn't make sense with Dapper
        // Consider removing or implementing differently
        _logger.LogWarning("Add method called - this method is not recommended with Dapper implementation");
    }

    public async Task<ServiceDefinition> UpdateAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        service.UpdatedAt = DateTime.UtcNow;
        
        var sql = @"
            UPDATE Services 
            SET Name = @Name, DisplayName = @DisplayName, Description = @Description, ServiceType = @ServiceType,
                Version = @Version, BinaryPath = @BinaryPath, ConfigPath = @ConfigPath, DefaultPort = @DefaultPort,
                IsInstalled = @IsInstalled, IsEnabled = @IsEnabled, InstallDate = @InstallDate, AutoStart = @AutoStart,
                Configuration = @Configuration, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
        
        var affected = await connection.ExecuteAsync(sql, service);
        
        if (affected == 0)
        {
            _logger.LogWarning("No service found with ID {ServiceId} to update", service.Id);
        }
        else
        {
            _logger.LogInformation("Service {ServiceName} updated (ID: {ServiceId})", service.Name, service.Id);
        }
        
        return service;
    }

    /// <summary>
    /// Updates a service in the context without saving changes immediately.
    /// Call SaveChangesAsync() on the DbContext to persist changes.
    /// </summary>
    public void Update(ServiceDefinition service)
    {
        // This method is kept for compatibility but doesn't make sense with Dapper
        // Consider removing or implementing differently
        _logger.LogWarning("Update method called - this method is not recommended with Dapper implementation");
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = "DELETE FROM Services WHERE Id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = id });
        
        if (affected == 0)
        {
            _logger.LogWarning("Attempted to delete service with ID {ServiceId} but service was not found", id);
            return false;
        }
        
        _logger.LogInformation("Successfully deleted service with ID {ServiceId}", id);
        return true;
    }

    /// <summary>
    /// Marks a service for deletion in the context without saving changes immediately.
    /// Call SaveChangesAsync() on the DbContext to persist changes.
    /// </summary>
    public Task<bool> MarkForDeletionAsync(long id, CancellationToken cancellationToken = default)
    {
        // This method is kept for compatibility but doesn't make sense with Dapper
        // Consider removing or implementing differently
        _logger.LogWarning("MarkForDeletionAsync method called - this method is not recommended with Dapper implementation");
        return Task.FromResult(false);
    }

    public async Task UpdateInstallationStatusAsync(long id, bool isInstalled, string? binaryPath = null, string? version = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            UPDATE Services 
            SET IsInstalled = @IsInstalled, UpdatedAt = @UpdatedAt, InstallDate = @InstallDate
            WHERE Id = @Id";
        
        var parameters = new 
        { 
            Id = id, 
            IsInstalled = isInstalled, 
            UpdatedAt = DateTime.UtcNow,
            InstallDate = isInstalled ? DateTime.UtcNow : (DateTime?)null
        };
        
        var affected = await connection.ExecuteAsync(sql, parameters);
        
        if (affected == 0)
        {
            _logger.LogWarning("Attempted to update installation status for service with ID {ServiceId} but service was not found", id);
        }
        else
        {
            _logger.LogInformation("Updated installation status for service with ID {ServiceId} to {IsInstalled}", id, isInstalled);
        }
    }

    public async Task UpdateStatusAsync(long id, ServiceStatus status, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            UPDATE Services 
            SET Status = @Status, UpdatedAt = CURRENT_TIMESTAMP 
            WHERE Id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Status = (int)status });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated status for service with ID {ServiceId} to {Status}", id, status);
        }
        else
        {
            _logger.LogWarning("No service found with ID {ServiceId} to update status", id);
        }
    }

    public async Task UpdateConfigurationAsync(long id, string configurationJson, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = @"
            UPDATE Services 
            SET Configuration = @Configuration, UpdatedAt = CURRENT_TIMESTAMP 
            WHERE Id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, Configuration = configurationJson });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated configuration for service with ID {ServiceId}", id);
        }
        else
        {
            _logger.LogWarning("No service found with ID {ServiceId} to update configuration", id);
        }
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("ExistsByNameAsync called with null or empty name");
            return false;
        }
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = "SELECT COUNT(*) FROM Services WHERE Name = @Name";
        var count = await connection.QuerySingleAsync<int>(sql, new { Name = name.Trim() });
        
        return count > 0;
    }

    public async Task<bool> ExistsByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        var sql = "SELECT COUNT(*) FROM Services WHERE ServiceType = @ServiceType";
        var count = await connection.QuerySingleAsync<int>(sql, new { ServiceType = serviceType.ToString() });
        
        return count > 0;
    }
    
}
