using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.Core.Interfaces;

/// <summary>
/// Interface for checking service installation status
/// </summary>
public interface IServiceInstallationChecker
{
    /// <summary>
    /// Determines if a service is actually installed based on file system checks
    /// </summary>
    /// <param name="service">The service definition to check</param>
    /// <returns>True if the service is installed, false otherwise</returns>
    bool IsServiceInstalled(ServiceDefinition service);
    
    /// <summary>
    /// Determines if a service is actually installed based on file system checks
    /// </summary>
    /// <param name="servicePath">The path to the service installation</param>
    /// <param name="serviceType">The type of service to check</param>
    /// <returns>True if the service is installed, false otherwise</returns>
    bool IsServiceInstalled(string servicePath, ServiceType serviceType);
    
    /// <summary>
    /// Determines the installation status of a service (Installed/Not Installed/Unknown)
    /// </summary>
    /// <param name="service">The service definition to check</param>
    /// <returns>The installation status string</returns>
    string GetServiceInstallationStatus(ServiceDefinition service);
    
    /// <summary>
    /// Determines the installation status of a service based on path and type
    /// </summary>
    /// <param name="servicePath">The path to the service installation</param>
    /// <param name="serviceType">The type of service to check</param>
    /// <param name="isInstalledInDatabase">Whether the service is marked as installed in the database</param>
    /// <returns>The installation status string</returns>
    string GetServiceInstallationStatus(string servicePath, ServiceType serviceType, bool isInstalledInDatabase = true);
}
