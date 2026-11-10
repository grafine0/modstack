using ModStack.Common.Models;

namespace ModStack.Core.Interfaces;

/// <summary>
/// Interface for project management operations
/// </summary>
public interface IProjectManager
{
    // Project CRUD operations
    Task<List<Project>> GetAllProjectsAsync();
    Task<Project?> GetProjectByIdAsync(long id);
    Task<Project?> GetProjectByNameAsync(string name);
    Task<List<Project>> GetProjectsByStackAsync(long stackId);
    Task<Project> CreateProjectAsync(string name, string path, string? domain = null, string? documentRoot = null);
    Task<bool> UpdateProjectAsync(Project project);
    Task<bool> DeleteProjectAsync(long id);
    
    // Project binding operations
    Task<bool> BindProjectToStackAsync(long projectId, long stackId);
    Task<bool> UnbindProjectFromStackAsync(long projectId);
    Task<List<Project>> GetUnboundProjectsAsync();
    Task<List<Project>> GetActiveProjectsAsync();
    
    // Virtual host operations
    Task<VirtualHost> CreateVirtualHostAsync(long projectId, long serviceId, string serverName, 
        string documentRoot, int port = 80, bool isSSL = false);
    Task<bool> UpdateVirtualHostAsync(VirtualHost virtualHost);
    Task<bool> DeleteVirtualHostAsync(long virtualHostId);
    Task<List<VirtualHost>> GetProjectVirtualHostsAsync(long projectId);
    Task<VirtualHost?> GetVirtualHostByServerNameAsync(string serverName);
    
    // Configuration generation
    Task<string> GenerateApacheConfigAsync(long projectId);
    Task<string> GenerateNginxConfigAsync(long projectId);
    Task<bool> WriteConfigurationFilesAsync(long projectId, string configDirectory);
    
    // Project validation
    Task<bool> ValidateProjectPathAsync(string path);
    Task<bool> ValidateProjectNameAsync(string name, long? excludeId = null);
    Task<bool> ValidateServerNameAsync(string serverName, long? excludeVirtualHostId = null);
    
    // Utility operations
    Task<string> GetProjectUrlAsync(long projectId);
    Task<List<string>> GetAllDomainsAsync();
    Task<Dictionary<string, string>> GetHostsFileEntriesAsync();
}