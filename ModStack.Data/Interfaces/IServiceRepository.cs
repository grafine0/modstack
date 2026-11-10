using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.Data.Interfaces;

/// <summary>
/// Repository interface for service definition operations
/// </summary>
public interface IServiceRepository
{
    /// <summary>
    /// Get all service definitions
    /// </summary>
    Task<IEnumerable<ServiceDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all enabled service definitions
    /// </summary>
    Task<IEnumerable<ServiceDefinition>> GetEnabledAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all installed service definitions
    /// </summary>
    Task<IEnumerable<ServiceDefinition>> GetInstalledAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service definition by ID
    /// </summary>
    Task<ServiceDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service definition by name
    /// </summary>
    Task<ServiceDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service definition by name and version
    /// </summary>
    Task<ServiceDefinition?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get service definition by type
    /// </summary>
    Task<ServiceDefinition?> GetByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new service definition
    /// </summary>
    Task<ServiceDefinition> AddAsync(ServiceDefinition service, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new service definition
    /// </summary>
    Task<ServiceDefinition> CreateAsync(ServiceDefinition service, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a service to the context without saving changes immediately
    /// </summary>
    void Add(ServiceDefinition service);
    
    /// <summary>
    /// Update an existing service definition
    /// </summary>
    Task<ServiceDefinition> UpdateAsync(ServiceDefinition service, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a service in the context without saving changes immediately
    /// </summary>
    void Update(ServiceDefinition service);
    
    /// <summary>
    /// Delete a service definition
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark a service for deletion in the context without saving changes immediately
    /// </summary>
    Task<bool> MarkForDeletionAsync(long id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update service installation status
    /// </summary>
    Task UpdateInstallationStatusAsync(long id, bool isInstalled, string? binaryPath = null, string? version = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update service status
    /// </summary>
    Task UpdateStatusAsync(long id, ServiceStatus status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update service configuration
    /// </summary>
    Task UpdateConfigurationAsync(long id, string configurationJson, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a service exists by name
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a service exists by type
    /// </summary>
    Task<bool> ExistsByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default);
}
