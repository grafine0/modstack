using Microsoft.Extensions.Logging;
using ModStack.Core.Services;

namespace ModStack.GUI.Services;

/// <summary>
/// Helper service for managing service configurations in the GUI
/// </summary>
public class ServiceManagementHelper
{
    private readonly IServiceConfigurationService _serviceConfigurationService;
    private readonly ILogger<ServiceManagementHelper> _logger;
    
    public ServiceManagementHelper(
        IServiceConfigurationService serviceConfigurationService,
        ILogger<ServiceManagementHelper> logger)
    {
        _serviceConfigurationService = serviceConfigurationService;
        _logger = logger;
    }
    
    /// <summary>
    /// Manually configure a Windows service name for a service
    /// </summary>
    /// <param name="serviceId">The service ID</param>
    /// <param name="windowsServiceName">The Windows service name</param>
    public async Task ConfigureWindowsServiceNameAsync(long serviceId, string windowsServiceName)
    {
        try
        {
            await _serviceConfigurationService.UpdateWindowsServiceNameAsync(serviceId, windowsServiceName);
            _logger.LogInformation("Successfully configured Windows service name for service {ServiceId}", serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure Windows service name for service {ServiceId}", serviceId);
            throw;
        }
    }
    
    /// <summary>
    /// Refresh all Windows service name configurations
    /// </summary>
    public async Task RefreshServiceConfigurationsAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing service configurations");
            
            // First try auto-detection
            await _serviceConfigurationService.AutoDetectWindowsServiceNamesAsync();
            
            // Then apply defaults for any that weren't detected
            await _serviceConfigurationService.ConfigureDefaultWindowsServiceNamesAsync();
            
            _logger.LogInformation("Service configurations refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh service configurations");
            throw;
        }
    }
    
    /// <summary>
    /// Gets example Windows service name mappings for common services
    /// </summary>
    public Dictionary<string, string> GetExampleServiceMappings()
    {
        return new Dictionary<string, string>
        {
            ["Apache HTTP Server 2.4.65"] = "Apache2.4",
            ["Apache HTTP Server 2.4.41"] = "Apache2.4",
            ["MySQL 8.0.35"] = "MySQL80",
            ["MySQL 8.4.2"] = "MySQL84",
            ["MariaDB 10.11"] = "MariaDB",
            ["PostgreSQL 16"] = "PostgreSQL",
            ["Nginx 1.25.3"] = "nginx",
            ["Redis 7.2.4"] = "Redis",
            ["MongoDB 7.0"] = "MongoDB"
        };
    }
}
