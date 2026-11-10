using System.Diagnostics;
using System.IO;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for checking service installation status
/// </summary>
public class ServiceInstallationChecker : IServiceInstallationChecker
{
    public bool IsServiceInstalled(ServiceDefinition service)
    {
        if (service == null)
            return false;
            
        var servicePath = service.BinaryPath;
        return IsServiceInstalled(servicePath, service.ServiceType);
    }
    
    public bool IsServiceInstalled(string servicePath, ServiceType serviceType)
    {
        try
        {
            if (string.IsNullOrEmpty(servicePath))
                return false;
                
            if (!Directory.Exists(servicePath))
                return false;
            
            // Check for service-specific executable files
            return serviceType switch
            {
                ServiceType.PHP => File.Exists(Path.Combine(servicePath, "php.exe")) ||
                                  File.Exists(Path.Combine(servicePath, "php-cgi.exe")),
                ServiceType.Apache => File.Exists(Path.Combine(servicePath, "bin", "httpd.exe")) ||
                                     File.Exists(Path.Combine(servicePath, "httpd.exe")),
                ServiceType.Nginx => File.Exists(Path.Combine(servicePath, "nginx.exe")),
                ServiceType.MySQL => File.Exists(Path.Combine(servicePath, "bin", "mysqld.exe")) ||
                                    File.Exists(Path.Combine(servicePath, "mysqld.exe")),
                _ => Directory.Exists(servicePath)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error checking service installation for {serviceType} at {servicePath}: {ex.Message}");
            return false;
        }
    }
    
    public string GetServiceInstallationStatus(ServiceDefinition service)
    {
        if (service == null)
            return "Unknown";
            
        // First check if the service is marked as installed in the database
        if (!service.IsInstalled)
        {
            return "Not Installed";
        }
        
        // If marked as installed, verify the installation actually exists
        var servicePath = service.BinaryPath;
        if (string.IsNullOrEmpty(servicePath))
        {
            return "Not Installed";
        }
        
        // Check if the service directory and executable files exist
        bool isActuallyInstalled = IsServiceInstalled(servicePath, service.ServiceType);
        return isActuallyInstalled ? "Installed" : "Not Installed";
    }
    
    public string GetServiceInstallationStatus(string servicePath, ServiceType serviceType, bool isInstalledInDatabase = true)
    {
        try
        {
            // First check if the service is marked as installed in the database
            if (!isInstalledInDatabase)
            {
                return "Not Installed";
            }
            
            // If marked as installed, verify the installation actually exists
            if (string.IsNullOrEmpty(servicePath))
            {
                return "Not Installed";
            }
            
            // Check if the service directory and executable files exist
            bool isActuallyInstalled = IsServiceInstalled(servicePath, serviceType);
            return isActuallyInstalled ? "Installed" : "Not Installed";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error determining service installation status for {serviceType} at {servicePath}: {ex.Message}");
            return "Unknown";
        }
    }
}
