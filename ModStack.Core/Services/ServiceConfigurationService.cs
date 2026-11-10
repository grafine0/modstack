using Microsoft.Extensions.Logging;
using ModStack.Data.Interfaces;
using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.Core.Services;

/// <summary>
/// Service for managing service-specific configuration including Windows service names
/// </summary>
public class ServiceConfigurationService(
    IServiceRepository serviceRepository,
    ILogger<ServiceConfigurationService> logger) : IServiceConfigurationService
{
    private readonly IServiceRepository _serviceRepository = serviceRepository;
    private readonly ILogger<ServiceConfigurationService> _logger = logger;
    
    /// <summary>
    /// Updates the Windows service name for a service definition
    /// </summary>
    public async Task UpdateWindowsServiceNameAsync(long serviceId, string windowsServiceName)
    {
        try
        {
            var service = await _serviceRepository.GetByIdAsync(serviceId);
            if (service == null)
            {
                _logger.LogWarning("Service with ID {ServiceId} not found", serviceId);
                return;
            }
            
            service.WindowsServiceName = windowsServiceName;
            service.UpdatedAt = DateTime.UtcNow;
            
            await _serviceRepository.UpdateAsync(service);
            _logger.LogInformation("Updated Windows service name for {ServiceName} to {WindowsServiceName}", 
                service.Name, windowsServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Windows service name for service {ServiceId}", serviceId);
            throw;
        }
    }
    
    /// <summary>
    /// Gets the Windows service name for a service
    /// </summary>
    public async Task<string?> GetWindowsServiceNameAsync(long serviceId)
    {
        try
        {
            var service = await _serviceRepository.GetByIdAsync(serviceId);
            return service?.WindowsServiceName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Windows service name for service {ServiceId}", serviceId);
            return null;
        }
    }
    
    /// <summary>
    /// Configures default Windows service names for standard services
    /// </summary>
    public async Task ConfigureDefaultWindowsServiceNamesAsync()
    {
        try
        {
            _logger.LogInformation("Configuring default Windows service names");
            
            var services = await _serviceRepository.GetAllAsync();
            
            foreach (var service in services)
            {
                if (!string.IsNullOrEmpty(service.WindowsServiceName))
                {
                    continue; // Skip if already configured
                }
                
                var defaultServiceName = GetDefaultWindowsServiceName(service.ServiceType);
                if (!string.IsNullOrEmpty(defaultServiceName))
                {
                    await UpdateWindowsServiceNameAsync(service.Id, defaultServiceName);
                }
            }
            
            _logger.LogInformation("Completed configuring default Windows service names");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring default Windows service names");
            throw;
        }
    }
    
    /// <summary>
    /// Auto-detects and configures Windows service names for installed services
    /// </summary>
    public async Task AutoDetectWindowsServiceNamesAsync()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                _logger.LogInformation("Auto-detection only available on Windows");
                return;
            }
            
            _logger.LogInformation("Auto-detecting Windows service names");
            
            var installedServices = GetInstalledWindowsServices();
            var services = await _serviceRepository.GetAllAsync();
            
            foreach (var service in services.Where(s => s.IsInstalled && string.IsNullOrEmpty(s.WindowsServiceName)))
            {
                var detectedServiceName = DetectWindowsServiceName(service, installedServices);
                if (!string.IsNullOrEmpty(detectedServiceName))
                {
                    await UpdateWindowsServiceNameAsync(service.Id, detectedServiceName);
                }
            }
            
            _logger.LogInformation("Completed auto-detecting Windows service names");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error auto-detecting Windows service names");
            throw;
        }
    }
    
    private static string? GetDefaultWindowsServiceName(ServiceType serviceType)
    {
        return serviceType switch
        {
            ServiceType.Apache => GetApacheServiceName(null),
            ServiceType.MySQL => GetMySQLServiceName(null),
            ServiceType.PHP => GetPHPServiceName(null),
            ServiceType.Nginx => "nginx",
            ServiceType.MariaDB => "MariaDB",
            ServiceType.PostgreSQL => "PostgreSQL",
            ServiceType.MongoDB => "MongoDB",
            ServiceType.Redis => "Redis",
            _ => null
        };
    }
    
    private static string? GetApacheServiceName(string? version)
    {
        // Common Apache service names based on version
        if (!string.IsNullOrEmpty(version))
        {
            if (version.StartsWith("2.4"))
                return "Apache2.4";
            if (version.StartsWith("2.2"))
                return "Apache2.2";
        }
        
        // Default fallback
        return "Apache2.4";
    }
    
    private static string? GetMySQLServiceName(string? version)
    {
        // Common MySQL service names based on version
        if (!string.IsNullOrEmpty(version))
        {
            if (version.StartsWith("8.0"))
                return "MySQL80";
            if (version.StartsWith("8.4"))
                return "MySQL84";
            if (version.StartsWith("5.7"))
                return "MySQL57";
        }
        
        // Default fallback
        return "MySQL80";
    }
    
    private static string? GetPHPServiceName(string? _)
    {
        // PHP services are typically not Windows services in ModStack
        // They're usually managed through FastCGI or as Apache modules
        // Return null as PHP doesn't typically run as a Windows service
        return null;
    }
    
    private List<string> GetInstalledWindowsServices()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                return [];
            
            // Use sc.exe command to get Windows services (cross-platform compatible)
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = "query state= all",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return [];
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            var services = new List<string>();
            var lines = output.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.TrimStart().StartsWith("SERVICE_NAME:"))
                {
                    var serviceName = line[(line.IndexOf(':') + 1)..].Trim();
                    if (!string.IsNullOrEmpty(serviceName))
                    {
                        services.Add(serviceName);
                    }
                }
            }
            
            return services;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting installed Windows services");
            return [];
        }
    }
    
    private static string? DetectWindowsServiceName(ServiceDefinition service, List<string> installedServices)
    {
        var serviceName = service.Name.ToLowerInvariant();
        var serviceType = service.ServiceType.ToString().ToLowerInvariant();
        
        // Look for exact matches first
        var exactMatch = installedServices.FirstOrDefault(s => 
            string.Equals(s, service.Name, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return exactMatch;
        
        // Look for partial matches based on service type
        var partialMatches = installedServices.Where(s => 
            s.Contains(serviceType, StringComparison.OrdinalIgnoreCase) || 
            s.Contains(serviceName, StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (partialMatches.Count == 1)
            return partialMatches.First();
        
        // If multiple matches, try to find the best one
        if (partialMatches.Count > 1)
        {
            // Prefer exact service type matches
            var exactTypeMatch = partialMatches.FirstOrDefault(s => 
                s.StartsWith(serviceType, StringComparison.OrdinalIgnoreCase));
            if (exactTypeMatch != null)
                return exactTypeMatch;
            
            // Return the first match as a fallback
            return partialMatches.First();
        }
        
        return null;
    }
}
