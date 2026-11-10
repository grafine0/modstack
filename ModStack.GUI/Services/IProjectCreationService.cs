using ModStack.Common.Models;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for handling project creation workflow
/// </summary>
public interface IProjectCreationService
{
    /// <summary>
    /// Creates a new project with all associated configuration
    /// </summary>
    /// <param name="request">Project creation request</param>
    Task<ProjectCreationResult> CreateProjectAsync(ProjectCreationRequest request);

    /// <summary>
    /// Updates an existing project with new configuration
    /// </summary>
    /// <param name="request">Project update request</param>
    Task<ProjectCreationResult> UpdateProjectAsync(ProjectUpdateRequest request);

    /// <summary>
    /// Validates project creation parameters
    /// </summary>
    /// <param name="request">Project creation request</param>
    Task<ProjectValidationResult> ValidateProjectRequestAsync(ProjectCreationRequest request);
}

/// <summary>
/// Request for creating a new project
/// </summary>
public class ProjectCreationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? DocumentRoot { get; set; }
    public string? ServerAliases { get; set; }
    public int Port { get; set; } = 80;
    public bool EnableSSL { get; set; }
    public bool CreateDirectory { get; set; } = true;
    public bool UpdateHostsFile { get; set; } = true;
    public bool CreateVirtualHost { get; set; } = true;
    
    // Stack configuration
    public long? StackId { get; set; }
    
    // Service configuration (when no stack is selected)
    public long? WebServerId { get; set; }
    public long? DatabaseId { get; set; }
    public long? PHPRuntimeId { get; set; }
    public long? AdditionalServiceId { get; set; }
}

/// <summary>
/// Request for updating an existing project
/// </summary>
public class ProjectUpdateRequest : ProjectCreationRequest
{
    public long ProjectId { get; set; }
}

/// <summary>
/// Result of project creation or update
/// </summary>
public class ProjectCreationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
    
    // Additional results
    public VirtualHostResult? VirtualHostResult { get; set; }
    public bool HostsFileUpdated { get; set; }
    public bool SSLConfigured { get; set; }
    public List<string> RestartedServices { get; set; } = new();
}

/// <summary>
/// Validation result for project creation
/// </summary>
public class ProjectValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
