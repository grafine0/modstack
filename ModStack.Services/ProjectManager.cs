using Microsoft.Extensions.Logging;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;

namespace ModStack.Services;

/// <summary>
/// Implementation of project management operations
/// </summary>
public class ProjectManager : IProjectManager
{
    private readonly IProjectRepository _projectRepository;
    private readonly IVirtualHostRepository _virtualHostRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly ILogger<ProjectManager>? _logger;

    public ProjectManager(
        IProjectRepository projectRepository,
        IVirtualHostRepository virtualHostRepository,
        IStackRepository stackRepository,
        IServiceRepository serviceRepository,
        ILogger<ProjectManager>? logger = null)
    {
        _projectRepository = projectRepository;
        _virtualHostRepository = virtualHostRepository;
        _stackRepository = stackRepository;
        _serviceRepository = serviceRepository;
        _logger = logger;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        _logger?.LogInformation("Getting all projects");
        var projects = await _projectRepository.GetAllAsync();
        
        // Load virtual hosts for each project
        foreach (var project in projects)
        {
            project.VirtualHosts = await _virtualHostRepository.GetByProjectIdAsync(project.Id);
        }
        
        return projects;
    }

    public async Task<Project?> GetProjectByIdAsync(long id)
    {
        _logger?.LogInformation("Getting project with ID {ProjectId}", id);
        var project = await _projectRepository.GetByIdAsync(id);
        
        if (project != null)
        {
            project.VirtualHosts = await _virtualHostRepository.GetByProjectIdAsync(project.Id);
        }
        
        return project;
    }

    public async Task<Project?> GetProjectByNameAsync(string name)
    {
        _logger?.LogInformation("Getting project with name {ProjectName}", name);
        var project = await _projectRepository.GetByNameAsync(name);
        
        if (project != null)
        {
            project.VirtualHosts = await _virtualHostRepository.GetByProjectIdAsync(project.Id);
        }
        
        return project;
    }

    public async Task<List<Project>> GetProjectsByStackAsync(long stackId)
    {
        _logger?.LogInformation("Getting projects for stack {StackId}", stackId);
        var projects = await _projectRepository.GetByStackIdAsync(stackId);
        
        foreach (var project in projects)
        {
            project.VirtualHosts = await _virtualHostRepository.GetByProjectIdAsync(project.Id);
        }
        
        return projects;
    }

    public async Task<Project> CreateProjectAsync(string name, string path, string? domain = null, string? documentRoot = null)
    {
        _logger?.LogInformation("Creating project {ProjectName} at path {ProjectPath}", name, path);
        
        // Validate inputs
        if (!await ValidateProjectNameAsync(name))
        {
            throw new InvalidOperationException($"Project name '{name}' already exists");
        }
        
        if (!await ValidateProjectPathAsync(path))
        {
            throw new InvalidOperationException($"Project path '{path}' is not valid or accessible");
        }
        
        var project = new Project
        {
            Name = name,
            DisplayName = name,
            Path = path,
            Domain = domain,
            DocumentRoot = documentRoot,
            IsActive = true
        };
        
        var createdProject = await _projectRepository.CreateAsync(project);
        _logger?.LogInformation("Project {ProjectName} created successfully with ID {ProjectId}", name, createdProject.Id);
        
        return createdProject;
    }

    public async Task<bool> UpdateProjectAsync(Project project)
    {
        _logger?.LogInformation("Updating project {ProjectId}", project.Id);
        
        // Validate name uniqueness if name changed
        var existingProject = await _projectRepository.GetByIdAsync(project.Id);
        if (existingProject != null && existingProject.Name != project.Name)
        {
            if (!await ValidateProjectNameAsync(project.Name, project.Id))
            {
                throw new InvalidOperationException($"Project name '{project.Name}' already exists");
            }
        }
        
        var updated = await _projectRepository.UpdateAsync(project);
        if (updated)
        {
            _logger?.LogInformation("Project {ProjectId} updated successfully", project.Id);
        }
        else
        {
            _logger?.LogWarning("Failed to update project {ProjectId}", project.Id);
        }
        
        return updated;
    }

    public async Task<bool> DeleteProjectAsync(long id)
    {
        _logger?.LogInformation("Deleting project {ProjectId}", id);
        
        // Delete all virtual hosts for this project first
        await _virtualHostRepository.DeleteByProjectIdAsync(id);
        
        var deleted = await _projectRepository.DeleteAsync(id);
        if (deleted)
        {
            _logger?.LogInformation("Project {ProjectId} deleted successfully", id);
        }
        else
        {
            _logger?.LogWarning("Failed to delete project {ProjectId}", id);
        }
        
        return deleted;
    }

    public async Task<bool> BindProjectToStackAsync(long projectId, long stackId)
    {
        _logger?.LogInformation("Binding project {ProjectId} to stack {StackId}", projectId, stackId);
        
        // Verify stack exists
        var stack = await _stackRepository.GetByIdAsync(stackId);
        if (stack == null)
        {
            throw new InvalidOperationException($"Stack with ID {stackId} not found");
        }
        
        var bound = await _projectRepository.BindToStackAsync(projectId, stackId);
        if (bound)
        {
            _logger?.LogInformation("Project {ProjectId} bound to stack {StackId} successfully", projectId, stackId);
        }
        else
        {
            _logger?.LogWarning("Failed to bind project {ProjectId} to stack {StackId}", projectId, stackId);
        }
        
        return bound;
    }

    public async Task<bool> UnbindProjectFromStackAsync(long projectId)
    {
        _logger?.LogInformation("Unbinding project {ProjectId} from stack", projectId);
        
        var unbound = await _projectRepository.UnbindFromStackAsync(projectId);
        if (unbound)
        {
            _logger?.LogInformation("Project {ProjectId} unbound from stack successfully", projectId);
        }
        else
        {
            _logger?.LogWarning("Failed to unbind project {ProjectId} from stack", projectId);
        }
        
        return unbound;
    }

    public async Task<List<Project>> GetUnboundProjectsAsync()
    {
        _logger?.LogInformation("Getting unbound projects");
        var allProjects = await GetAllProjectsAsync();
        return allProjects.Where(p => p.StackId == null).ToList();
    }

    public async Task<List<Project>> GetActiveProjectsAsync()
    {
        _logger?.LogInformation("Getting active projects");
        var projects = await _projectRepository.GetActiveProjectsAsync();
        
        foreach (var project in projects)
        {
            project.VirtualHosts = await _virtualHostRepository.GetByProjectIdAsync(project.Id);
        }
        
        return projects;
    }

    public async Task<VirtualHost> CreateVirtualHostAsync(long projectId, long serviceId, string serverName, string documentRoot, int port = 80, bool isSSL = false)
    {
        _logger?.LogInformation("Creating virtual host {ServerName} for project {ProjectId}", serverName, projectId);
        
        // Validate inputs
        if (!await ValidateServerNameAsync(serverName))
        {
            throw new InvalidOperationException($"Server name '{serverName}' already exists");
        }
        
        // Verify project and service exist
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            throw new InvalidOperationException($"Project with ID {projectId} not found");
        }
        
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service == null)
        {
            throw new InvalidOperationException($"Service with ID {serviceId} not found");
        }
        
        var virtualHost = new VirtualHost
        {
            ProjectId = projectId,
            ServiceId = serviceId,
            ServerName = serverName,
            DocumentRoot = documentRoot,
            Port = port,
            IsSSL = isSSL,
            IsEnabled = true
        };
        
        var created = await _virtualHostRepository.CreateAsync(virtualHost);
        _logger?.LogInformation("Virtual host {ServerName} created successfully with ID {VirtualHostId}", serverName, created.Id);
        
        return created;
    }

    public async Task<bool> UpdateVirtualHostAsync(VirtualHost virtualHost)
    {
        _logger?.LogInformation("Updating virtual host {VirtualHostId}", virtualHost.Id);
        
        // Validate server name uniqueness if changed
        var existing = await _virtualHostRepository.GetByIdAsync(virtualHost.Id);
        if (existing != null && existing.ServerName != virtualHost.ServerName)
        {
            if (!await ValidateServerNameAsync(virtualHost.ServerName, virtualHost.Id))
            {
                throw new InvalidOperationException($"Server name '{virtualHost.ServerName}' already exists");
            }
        }
        
        var updated = await _virtualHostRepository.UpdateAsync(virtualHost);
        if (updated)
        {
            _logger?.LogInformation("Virtual host {VirtualHostId} updated successfully", virtualHost.Id);
        }
        else
        {
            _logger?.LogWarning("Failed to update virtual host {VirtualHostId}", virtualHost.Id);
        }
        
        return updated;
    }

    public async Task<bool> DeleteVirtualHostAsync(long virtualHostId)
    {
        _logger?.LogInformation("Deleting virtual host {VirtualHostId}", virtualHostId);
        
        var deleted = await _virtualHostRepository.DeleteAsync(virtualHostId);
        if (deleted)
        {
            _logger?.LogInformation("Virtual host {VirtualHostId} deleted successfully", virtualHostId);
        }
        else
        {
            _logger?.LogWarning("Failed to delete virtual host {VirtualHostId}", virtualHostId);
        }
        
        return deleted;
    }

    public async Task<List<VirtualHost>> GetProjectVirtualHostsAsync(long projectId)
    {
        _logger?.LogInformation("Getting virtual hosts for project {ProjectId}", projectId);
        return await _virtualHostRepository.GetByProjectIdAsync(projectId);
    }

    public async Task<VirtualHost?> GetVirtualHostByServerNameAsync(string serverName)
    {
        _logger?.LogInformation("Getting virtual host by server name {ServerName}", serverName);
        return await _virtualHostRepository.GetByServerNameAsync(serverName);
    }

    public async Task<string> GenerateApacheConfigAsync(long projectId)
    {
        _logger?.LogInformation("Generating Apache configuration for project {ProjectId}", projectId);
        
        var project = await GetProjectByIdAsync(projectId);
        if (project == null)
        {
            throw new InvalidOperationException($"Project with ID {projectId} not found");
        }
        
        var configs = new List<string>();
        
        // Generate config for each virtual host
        foreach (var vhost in project.VirtualHosts)
        {
            if (vhost.IsEnabled)
            {
                configs.Add(vhost.GenerateApacheConfig());
                configs.Add(""); // Empty line between virtual hosts
            }
        }
        
        // If no virtual hosts, create a default one
        if (!project.VirtualHosts.Any())
        {
            var defaultVHost = new VirtualHost
            {
                ServerName = project.GetPrimaryDomain(),
                DocumentRoot = project.GetDocumentRoot(),
                Port = 80,
                IsEnabled = true
            };
            configs.Add(defaultVHost.GenerateApacheConfig());
        }
        
        return string.Join(Environment.NewLine, configs);
    }

    public async Task<string> GenerateNginxConfigAsync(long projectId)
    {
        _logger?.LogInformation("Generating Nginx configuration for project {ProjectId}", projectId);
        
        var project = await GetProjectByIdAsync(projectId);
        if (project == null)
        {
            throw new InvalidOperationException($"Project with ID {projectId} not found");
        }
        
        var configs = new List<string>();
        
        // Generate config for each virtual host
        foreach (var vhost in project.VirtualHosts)
        {
            if (vhost.IsEnabled)
            {
                configs.Add(vhost.GenerateNginxConfig());
                configs.Add(""); // Empty line between server blocks
            }
        }
        
        // If no virtual hosts, create a default one
        if (!project.VirtualHosts.Any())
        {
            var defaultVHost = new VirtualHost
            {
                ServerName = project.GetPrimaryDomain(),
                DocumentRoot = project.GetDocumentRoot(),
                Port = 80,
                IsEnabled = true
            };
            configs.Add(defaultVHost.GenerateNginxConfig());
        }
        
        return string.Join(Environment.NewLine, configs);
    }

    public async Task<bool> WriteConfigurationFilesAsync(long projectId, string configDirectory)
    {
        _logger?.LogInformation("Writing configuration files for project {ProjectId} to {ConfigDirectory}", projectId, configDirectory);
        
        try
        {
            var project = await GetProjectByIdAsync(projectId);
            if (project == null)
            {
                return false;
            }
            
            Directory.CreateDirectory(configDirectory);
            
            // Write Apache configuration
            var apacheConfig = await GenerateApacheConfigAsync(projectId);
            var apacheConfigPath = Path.Combine(configDirectory, $"{project.Name}.apache.conf");
            await File.WriteAllTextAsync(apacheConfigPath, apacheConfig);
            
            // Write Nginx configuration
            var nginxConfig = await GenerateNginxConfigAsync(projectId);
            var nginxConfigPath = Path.Combine(configDirectory, $"{project.Name}.nginx.conf");
            await File.WriteAllTextAsync(nginxConfigPath, nginxConfig);
            
            _logger?.LogInformation("Configuration files written successfully for project {ProjectId}", projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write configuration files for project {ProjectId}", projectId);
            return false;
        }
    }

    public async Task<bool> ValidateProjectPathAsync(string path)
    {
        await Task.CompletedTask; // Async for consistency
        
        try
        {
            return Directory.Exists(path) || Path.IsPathRooted(path);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ValidateProjectNameAsync(string name, long? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        
        var existing = await _projectRepository.GetByNameAsync(name);
        return existing == null || (excludeId.HasValue && existing.Id == excludeId.Value);
    }

    public async Task<bool> ValidateServerNameAsync(string serverName, long? excludeVirtualHostId = null)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            return false;
        }
        
        return !await _virtualHostRepository.ServerNameExistsAsync(serverName, excludeVirtualHostId);
    }

    public async Task<string> GetProjectUrlAsync(long projectId)
    {
        var project = await GetProjectByIdAsync(projectId);
        return project?.GetProjectUrl() ?? string.Empty;
    }

    public async Task<List<string>> GetAllDomainsAsync()
    {
        var projects = await GetAllProjectsAsync();
        var domains = new List<string>();
        
        foreach (var project in projects)
        {
            domains.AddRange(project.GetAllDomains());
        }
        
        return domains.Distinct().ToList();
    }

    public async Task<Dictionary<string, string>> GetHostsFileEntriesAsync()
    {
        var domains = await GetAllDomainsAsync();
        var entries = new Dictionary<string, string>();
        
        foreach (var domain in domains)
        {
            entries[domain] = "127.0.0.1";
        }
        
        return entries;
    }
}
