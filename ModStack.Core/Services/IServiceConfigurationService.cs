namespace ModStack.Core.Services;

/// <summary>
/// Service for managing service-specific configuration including Windows service names
/// </summary>
public interface IServiceConfigurationService
{
    /// <summary>
    /// Updates the Windows service name for a service definition
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="windowsServiceName">The Windows service name</param>
    Task UpdateWindowsServiceNameAsync(long serviceId, string windowsServiceName);
    
    /// <summary>
    /// Gets the Windows service name for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    Task<string?> GetWindowsServiceNameAsync(long serviceId);
    
    /// <summary>
    /// Configures default Windows service names for standard services
    /// </summary>
    Task ConfigureDefaultWindowsServiceNamesAsync();
    
    /// <summary>
    /// Auto-detects and configures Windows service names for installed services
    /// </summary>
    Task AutoDetectWindowsServiceNamesAsync();
}
