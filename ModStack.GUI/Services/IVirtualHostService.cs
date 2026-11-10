namespace ModStack.GUI.Services;

/// <summary>
/// Service for managing Apache virtual host configuration
/// </summary>
public interface IVirtualHostService
{
    /// <summary>
    /// Creates an Apache virtual host configuration for a project
    /// </summary>
    /// <param name="domain">The domain name</param>
    /// <param name="projectPath">The project path</param>
    /// <param name="serverAliases">List of server aliases</param>
    /// <param name="port">HTTP port (default 80)</param>
    /// <param name="enableSSL">Whether to enable SSL</param>
    /// <param name="projectId">The project ID for service binding lookup</param>
    Task<VirtualHostResult> CreateVirtualHostAsync(
        string domain, 
        string projectPath, 
        List<string>? serverAliases = null, 
        int port = 80, 
        bool enableSSL = false, 
        long? projectId = null);

    /// <summary>
    /// Removes an Apache virtual host configuration
    /// </summary>
    /// <param name="domain">The domain name to remove</param>
    Task<bool> RemoveVirtualHostAsync(string domain);

    /// <summary>
    /// Validates virtual host configuration
    /// </summary>
    /// <param name="domain">The domain name</param>
    /// <param name="projectPath">The project path</param>
    Task<VirtualHostValidationResult> ValidateVirtualHostAsync(string domain, string projectPath);

    /// <summary>
    /// Gets all configured virtual hosts
    /// </summary>
    Task<IEnumerable<VirtualHostInfo>> GetVirtualHostsAsync();

    /// <summary>
    /// Fixes virtual host files with invalid directives (like ProxyFCGIBackendType)
    /// </summary>
    Task<int> FixInvalidVirtualHostDirectivesAsync();

    /// <summary>
    /// Cleans up orphaned virtual host files (files that reference non-existent DocumentRoots)
    /// </summary>
    Task<int> CleanupOrphanedVirtualHostsAsync();

    /// <summary>
    /// Updates FcgidMaxRequestLen in all vhost files that reference the given PHP service.
    /// Called when PHP configuration (post_max_size, upload_max_filesize) changes.
    /// </summary>
    Task<int> UpdateVhostFcgidMaxRequestLenForPhpServiceAsync(ModStack.Common.Models.ServiceDefinition phpService);
}

/// <summary>
/// Result of virtual host creation
/// </summary>
public class VirtualHostResult
{
    public bool Success { get; set; }
    public bool ConfigurationValidated { get; set; }
    public string? Message { get; set; }
    public string? VirtualHostFilePath { get; set; }
    public string? SSLCertificatePath { get; set; }
    public string? SSLKeyPath { get; set; }
}

/// <summary>
/// Information about a configured virtual host
/// </summary>
public class VirtualHostInfo
{
    public string Domain { get; set; } = string.Empty;
    public string DocumentRoot { get; set; } = string.Empty;
    public List<string> ServerAliases { get; set; } = new();
    public int Port { get; set; }
    public bool SSLEnabled { get; set; }
    public string ConfigFilePath { get; set; } = string.Empty;
}

/// <summary>
/// Validation result for virtual host configuration
/// </summary>
public class VirtualHostValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
