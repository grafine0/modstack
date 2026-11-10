using ModStack.Common;
using ModStack.Common.Models;

namespace ModStack.Core.Interfaces;

/// <summary>
/// Interface for managing Windows services
/// </summary>
public interface IServiceManager
{
    /// <summary>
    /// Gets all available service definitions
    /// </summary>
    Task<List<ServiceDefinition>> GetServicesAsync();
    
    /// <summary>
    /// Gets a service definition by ID
    /// </summary>
    Task<ServiceDefinition?> GetServiceAsync(long id);
    
    /// <summary>
    /// Gets a service definition by name and version
    /// </summary>
    Task<ServiceDefinition?> GetServiceAsync(string name, string version);
    
    /// <summary>
    /// Installs a new service
    /// </summary>
    Task<bool> InstallServiceAsync(ServiceDefinition service);
    
    /// <summary>
    /// Uninstalls a service
    /// </summary>
    Task<bool> UninstallServiceAsync(long serviceId);
    
    /// <summary>
    /// Starts a service
    /// </summary>
    Task<bool> StartServiceAsync(long serviceId);
    
    /// <summary>
    /// Stops a service
    /// </summary>
    Task<bool> StopServiceAsync(long serviceId);
    
    /// <summary>
    /// Restarts a service
    /// </summary>
    Task<bool> RestartServiceAsync(long serviceId);
    
    /// <summary>
    /// Gets the current status of a service
    /// </summary>
    Task<ServiceStatus> GetServiceStatusAsync(long serviceId);
    
    /// <summary>
    /// Gets the current status of all services
    /// </summary>
    Task<Dictionary<long, ServiceStatus>> GetAllServiceStatusesAsync();
    
    /// <summary>
    /// Checks if a port is available
    /// </summary>
    Task<bool> IsPortAvailableAsync(int port, Protocol protocol = Protocol.TCP);
    
    /// <summary>
    /// Gets all port allocations
    /// </summary>
    Task<List<PortAllocation>> GetPortAllocationsAsync();
    
    /// <summary>
    /// Reserves a port for a service
    /// </summary>
    Task<bool> ReservePortAsync(int port, long serviceId, Protocol protocol = Protocol.TCP);
    
    /// <summary>
    /// Releases a port reservation
    /// </summary>
    Task<bool> ReleasePortAsync(int port);
    
    /// <summary>
    /// Gets service logs
    /// </summary>
    Task<List<ServiceLog>> GetServiceLogsAsync(long serviceId, int limit = 100);
    
    /// <summary>
    /// Adds a log entry for a service
    /// </summary>
    Task AddServiceLogAsync(long serviceId, LogLevel level, string message, string? details = null);
}
