namespace ModStack.Common.Interfaces;

/// <summary>
/// Service for managing application path configurations
/// </summary>
public interface IPathConfigurationService
{
    /// <summary>
    /// Gets the configured root directory path for ModStack
    /// </summary>
    string GetRootDirectory();
    
    /// <summary>
    /// Gets the configured projects directory path
    /// </summary>
    string GetProjectsDirectory();
    
    /// <summary>
    /// Gets the configured services directory path
    /// </summary>
    string GetServicesDirectory();
    
    /// <summary>
    /// Gets the configured logs directory path
    /// </summary>
    string GetLogsDirectory();
    
    /// <summary>
    /// Gets the configured config directory path
    /// </summary>
    string GetConfigDirectory();
    
    /// <summary>
    /// Gets the path to a specific service installation
    /// </summary>
    /// <param name="serviceName">The service name (e.g., "Apache", "PHP")</param>
    /// <param name="version">Optional version (e.g., "2.4.65", "8.3.0")</param>
    string GetServicePath(string serviceName, string? version = null);
    
    /// <summary>
    /// Gets common service installation paths for discovery
    /// </summary>
    /// <param name="serviceName">The service name</param>
    IEnumerable<string> GetServiceDiscoveryPaths(string serviceName);
    
    /// <summary>
    /// Generates a default project path for a given project name
    /// </summary>
    /// <param name="projectName">The project name</param>
    string GenerateProjectPath(string projectName);
    
    /// <summary>
    /// Checks if a path is within the managed ModStack directories
    /// </summary>
    /// <param name="path">The path to check</param>
    bool IsModStackManagedPath(string path);
}
