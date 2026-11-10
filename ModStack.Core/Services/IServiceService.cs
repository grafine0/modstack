using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.Core.Services;

/// <summary>
/// Service interface for centralized service management
/// </summary>
public interface IServiceService
{
    /// <summary>
    /// Get all services with their current status for UI display
    /// </summary>
    Task<IEnumerable<ServiceDisplayItem>> GetAllServiceDisplayItemsAsync();
    
    /// <summary>
    /// Get only installed services with their current status
    /// </summary>
    Task<IEnumerable<ServiceDisplayItem>> GetInstalledServicesAsync();
    
    /// <summary>
    /// Get service by ID
    /// </summary>
    Task<ServiceDisplayItem?> GetServiceByIdAsync(int id);
    
    /// <summary>
    /// Get service by name
    /// </summary>
    Task<ServiceDisplayItem?> GetServiceByNameAsync(string name);
    
    /// <summary>
    /// Get all services (raw database objects)
    /// </summary>
    Task<IEnumerable<ServiceDefinition>> GetAllServicesAsync();
    
    /// <summary>
    /// Get services by type (e.g., Apache, Nginx)
    /// </summary>
    Task<IEnumerable<ServiceDefinition>> GetServicesByTypeAsync(ServiceType serviceType);
    
    /// <summary>
    /// Get service by ID (raw database object)
    /// </summary>
    Task<ServiceDefinition?> GetServiceByIdAsync(long id);
    
    /// <summary>
    /// Detect and update installation status for all services
    /// </summary>
    Task DetectAndUpdateInstallationStatusAsync();
    
    /// <summary>
    /// Detect and update installation status for a specific service
    /// </summary>
    Task DetectAndUpdateInstallationStatusAsync(long serviceId);
    
    /// <summary>
    /// Detect and update service status for all installed services
    /// </summary>
    Task DetectAndUpdateServiceStatusAsync();
    
    /// <summary>
    /// Detect and update service status for a specific service
    /// </summary>
    Task DetectAndUpdateServiceStatusAsync(long serviceId);
    
    /// <summary>
    /// Add a new service definition
    /// </summary>
    Task<ServiceDisplayItem> AddServiceAsync(ServiceDefinition serviceDefinition);
    
    /// <summary>
    /// Update service configuration
    /// </summary>
    Task UpdateServiceConfigurationAsync(long serviceId, string configurationJson);
    
    /// <summary>
    /// Check if a service is actually installed on the system
    /// </summary>
    Task<bool> IsServiceInstalledAsync(string serviceName, string servicePath);
    
    /// <summary>
    /// Get the installed version of a service
    /// </summary>
    Task<string?> GetInstalledServiceVersionAsync(string serviceName, string servicePath);
    
    /// <summary>
    /// Check if a service is currently running
    /// </summary>
    Task<bool> IsServiceRunningAsync(int port);
}
