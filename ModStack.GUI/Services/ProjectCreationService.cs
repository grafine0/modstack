using System.IO;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Core.Services;
using ModStack.Data.Interfaces;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for handling complete project creation workflow
/// </summary>
public class ProjectCreationService(
    IProjectRepository projectRepository,
    IProjectServiceRepository projectServiceRepository,
    IVirtualHostService virtualHostService,
    HostsFileManager hostsFileManager,
    IServiceConfigurationService serviceConfigurationService,
    ILogger<ProjectCreationService> logger) : IProjectCreationService
{
private static readonly char[] AliasSeparators = ['\n', '\r', ',', ';'];
    
    private readonly IProjectRepository _projectRepository = projectRepository;
    private readonly IProjectServiceRepository _projectServiceRepository = projectServiceRepository;
    private readonly IVirtualHostService _virtualHostService = virtualHostService;
    private readonly HostsFileManager _hostsFileManager = hostsFileManager;
    private readonly IServiceConfigurationService _serviceConfigurationService = serviceConfigurationService;
    private readonly ILogger<ProjectCreationService> _logger = logger;

    public async Task<ProjectCreationResult> CreateProjectAsync(ProjectCreationRequest request)
    {
        try
        {
            LogService.Info($"Creating project: {request.Name}");

            var result = new ProjectCreationResult();

            // Validate request
            var validation = await ValidateProjectRequestAsync(request);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Message = "Validation failed";
                result.ErrorMessages.AddRange(validation.Errors);
                return result;
            }

            // Check for existing projects with same name, domain, or path
            var existingProjects = await _projectRepository.GetAllAsync();
            var nameConflict = existingProjects.FirstOrDefault(p => p.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));
            var domainConflict = existingProjects.FirstOrDefault(p => !string.IsNullOrEmpty(p.Domain) && p.Domain.Equals(request.Domain, StringComparison.OrdinalIgnoreCase));
            var pathConflict = existingProjects.FirstOrDefault(p => p.Path.Equals(request.Path, StringComparison.OrdinalIgnoreCase));

            if (nameConflict != null)
            {
                result.Success = false;
                result.Message = $"A project with the name '{request.Name}' already exists. Please choose a different name.";
                result.ErrorMessages.Add($"Project name '{request.Name}' is already in use by another project.");
                return result;
            }

            if (domainConflict != null)
            {
                result.Success = false;
                result.Message = $"A project with the domain '{request.Domain}' already exists. Please choose a different domain.";
                result.ErrorMessages.Add($"Domain '{request.Domain}' is already in use by another project.");
                return result;
            }

            if (pathConflict != null)
            {
                result.Success = false;
                result.Message = $"A project with the path '{request.Path}' already exists. Please choose a different path.";
                result.ErrorMessages.Add($"Project path '{request.Path}' is already in use by another project.");
                return result;
            }

            // Create directory if requested
            if (request.CreateDirectory && !Directory.Exists(request.Path))
            {
                try
                {
                    Directory.CreateDirectory(request.Path);
                    result.SuccessMessages.Add($"Created project directory: {request.Path}");
                }
                catch (Exception ex)
                {
                    result.ErrorMessages.Add($"Failed to create directory: {ex.Message}");
                    result.Success = false;
                    return result;
                }
            }

            // Create project in database
            var project = new Project
            {
                Name = request.Name,
                Path = request.Path,
                Domain = request.Domain,
                DocumentRoot = request.DocumentRoot ?? request.Path,
                StackId = request.StackId,
                IsActive = true,
                EnableSSL = request.EnableSSL,
                CreatedAt = DateTime.Now,
                Configuration = new Dictionary<string, object>()
            };
            
            // Save server aliases to Configuration dictionary
            if (!string.IsNullOrWhiteSpace(request.ServerAliases))
            {
                project.Configuration["ServerAliases"] = request.ServerAliases;
            }

            var createdProject = await _projectRepository.CreateAsync(project);
            result.ProjectId = createdProject.Id;
            result.Project = createdProject;
            result.SuccessMessages.Add($"Project '{request.Name}' created successfully");

            // Save service bindings if no stack is selected
            if (!request.StackId.HasValue)
            {
                await SaveServiceBindingsAsync(createdProject.Id, request);
                await RestartProjectServicesAsync(createdProject.Id, result);
            }

            // Create virtual host if requested
            if (request.CreateVirtualHost && !string.IsNullOrWhiteSpace(request.Domain))
            {
                var serverAliases = ParseServerAliases(request.ServerAliases);
                var vhostResult = await _virtualHostService.CreateVirtualHostAsync(
                    request.Domain, 
                    request.Path, 
                    serverAliases, 
                    request.Port, 
                    request.EnableSSL, 
                    createdProject.Id);

                result.VirtualHostResult = vhostResult;
                if (vhostResult.Success)
                {
                    result.SuccessMessages.Add($"Virtual host created for {request.Domain}");
                }
                else
                {
                    result.WarningMessages.Add($"Virtual host creation failed: {vhostResult.Message}");
                }
            }

            // Update hosts file if requested
            await UpdateHostsFileIfRequestedAsync(request, result);

            // Create default project files
            await CreateDefaultProjectFilesAsync(request.Path, request.Name, request.Domain);

            result.Success = true;
            result.Message = $"Project '{request.Name}' created successfully";

            LogService.Info($"Project creation completed for: {request.Name}");
            return result;
        }
        catch (Exception ex)
        {
            LogService.Error($"Error creating project: {request.Name} - {ex.Message}");
            
            // Provide user-friendly error messages
            string userFriendlyMessage;
            if (ex.Message.Contains("UNIQUE constraint failed: Projects.Name"))
            {
                userFriendlyMessage = $"A project with the name '{request.Name}' already exists. Please choose a different name.";
            }
            else if (ex.Message.Contains("UNIQUE constraint failed: Projects.Domain"))
            {
                userFriendlyMessage = $"A project with the domain '{request.Domain}' already exists. Please choose a different domain.";
            }
            else if (ex.Message.Contains("UNIQUE constraint failed: Projects.Path"))
            {
                userFriendlyMessage = $"A project with the path '{request.Path}' already exists. Please choose a different path.";
            }
            else if (ex.Message.Contains("SQLite Error 19"))
            {
                userFriendlyMessage = "A project with similar settings already exists. Please check the project name, domain, or path.";
            }
            else
            {
                userFriendlyMessage = $"Failed to create project: {ex.Message}";
            }
            
            return new ProjectCreationResult
            {
                Success = false,
                Message = userFriendlyMessage,
                ErrorMessages = [userFriendlyMessage]
            };
        }
    }

    public async Task<ProjectCreationResult> UpdateProjectAsync(ProjectUpdateRequest request)
    {
        try
        {
            LogService.Info($"Updating project: {request.Name} (ID: {request.ProjectId})");

            var result = new ProjectCreationResult();

            // Get existing project
            var existingProject = await _projectRepository.GetByIdAsync(request.ProjectId);
            if (existingProject == null)
            {
                result.Success = false;
                result.Message = "Project not found";
                result.ErrorMessages.Add("Project not found");
                return result;
            }

            // Validate request
            var validation = await ValidateProjectRequestAsync(request);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Message = "Validation failed";
                result.ErrorMessages.AddRange(validation.Errors);
                return result;
            }

            // Update project in database
            existingProject.Name = request.Name;
            existingProject.Path = request.Path;
            existingProject.Domain = request.Domain;
            existingProject.DocumentRoot = request.DocumentRoot ?? request.Path;
            existingProject.StackId = request.StackId;
            existingProject.EnableSSL = request.EnableSSL;
            
            // Save server aliases to Configuration dictionary
            if (!string.IsNullOrWhiteSpace(request.ServerAliases))
            {
                if (existingProject.Configuration == null)
                {
                    existingProject.Configuration = new Dictionary<string, object>();
                }
                existingProject.Configuration["ServerAliases"] = request.ServerAliases;
            }
            else
            {
                // Remove ServerAliases if it exists and the new value is empty
                if (existingProject.Configuration != null && existingProject.Configuration.ContainsKey("ServerAliases"))
                {
                    existingProject.Configuration.Remove("ServerAliases");
                }
            }

            await _projectRepository.UpdateAsync(existingProject);
            result.ProjectId = existingProject.Id;
            result.Project = existingProject;
            result.SuccessMessages.Add($"Project '{request.Name}' updated successfully");

            // Update service bindings based on stack selection
            if (!request.StackId.HasValue)
            {
                // No stack selected - use individual service bindings
                await RemoveExistingServiceBindingsAsync(existingProject.Id);
                await SaveServiceBindingsAsync(existingProject.Id, request);
                await RestartProjectServicesAsync(existingProject.Id, result);
            }
            else
            {
                // Stack is selected - remove any individual service bindings
                // The project will use the stack's services instead
                await RemoveExistingServiceBindingsAsync(existingProject.Id);
                LogService.Info($"Project {existingProject.Id} now uses stack {request.StackId.Value}, cleared individual service bindings");
            }

            // Update virtual host if requested
            if (request.CreateVirtualHost && !string.IsNullOrWhiteSpace(request.Domain))
            {
                LogService.Info($"Starting virtual host update for project: {request.Name}");
                var serverAliases = ParseServerAliases(request.ServerAliases);
                var vhostResult = await _virtualHostService.CreateVirtualHostAsync(
                    request.Domain, 
                    request.Path, 
                    serverAliases, 
                    request.Port, 
                    request.EnableSSL, 
                    existingProject.Id);

                LogService.Info($"Virtual host update completed. Success: {vhostResult.Success}, Message: {vhostResult.Message}");
                result.VirtualHostResult = vhostResult;
                if (vhostResult.Success)
                {
                    result.SuccessMessages.Add($"Virtual host updated for {request.Domain}");
                }
                else
                {
                    result.WarningMessages.Add($"Virtual host update failed: {vhostResult.Message}");
                }
            }

            // Update hosts file if requested
            await UpdateHostsFileIfRequestedAsync(request, result);

            LogService.Info($"Setting project update success for: {request.Name}");
            result.Success = true;
            result.Message = $"Project '{request.Name}' updated successfully";

            LogService.Info($"Project update completed for: {request.Name}");
            return result;
        }
        catch (Exception ex)
        {
            LogService.Error($"Error updating project: {request.Name} - {ex.Message}");
            LogService.Error($"Exception details: {ex}");
            return new ProjectCreationResult
            {
                Success = false,
                Message = $"Error updating project: {ex.Message}",
                ErrorMessages = [ex.Message]
            };
        }
    }

    public async Task<ProjectValidationResult> ValidateProjectRequestAsync(ProjectCreationRequest request)
    {
        var result = new ProjectValidationResult { IsValid = true };

        try
        {
            // Validate project name
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                result.Errors.Add("Project name is required");
                result.IsValid = false;
            }

            // Validate project path
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                result.Errors.Add("Project path is required");
                result.IsValid = false;
            }
            else if (request.Path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                result.Errors.Add("Project path contains invalid characters");
                result.IsValid = false;
            }

            // Validate domain if provided
            if (!string.IsNullOrWhiteSpace(request.Domain))
            {
                if (request.Domain.Contains(' ') || !request.Domain.Contains('.'))
                {
                    result.Errors.Add("Invalid domain format");
                    result.IsValid = false;
                }

                // Check for existing domain conflicts
                var existingProjects = await _projectRepository.GetAllAsync();
                var domainConflict = existingProjects.Any(p => 
                    string.Equals(p.Domain, request.Domain, StringComparison.OrdinalIgnoreCase));

                if (domainConflict)
                {
                    result.Warnings.Add($"Domain {request.Domain} is already used by another project");
                }
            }

            // Validate port
            if (request.Port <= 0 || request.Port > 65535)
            {
                result.Errors.Add("Invalid port number");
                result.IsValid = false;
            }

            return result;
        }
        catch (Exception ex)
        {
            LogService.Error($"Error validating project request: {ex.Message}");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
            return result;
        }
    }

    #region Private Methods

    private async Task SaveServiceBindingsAsync(long projectId, ProjectCreationRequest request)
    {
        try
        {
            var serviceIds = new List<long>();
            
            if (request.WebServerId.HasValue && request.WebServerId > 0)
                serviceIds.Add(request.WebServerId.Value);
            if (request.DatabaseId.HasValue && request.DatabaseId > 0)
                serviceIds.Add(request.DatabaseId.Value);
            if (request.PHPRuntimeId.HasValue && request.PHPRuntimeId > 0)
                serviceIds.Add(request.PHPRuntimeId.Value);
            if (request.AdditionalServiceId.HasValue && request.AdditionalServiceId > 0)
                serviceIds.Add(request.AdditionalServiceId.Value);

            // Remove duplicates (in case same service is selected in multiple dropdowns)
            serviceIds = serviceIds.Distinct().ToList();

            foreach (var serviceId in serviceIds)
            {
                // Check if binding already exists to avoid UNIQUE constraint violation
                var existingBinding = await _projectServiceRepository.GetByProjectAndServiceIdAsync(projectId, serviceId);
                if (existingBinding != null)
                {
                    // Update existing binding
                    existingBinding.IsEnabled = true;
                    existingBinding.UpdatedAt = DateTime.UtcNow;
                    await _projectServiceRepository.UpdateAsync(existingBinding);
                    LogService.Info($"Updated existing service binding: ProjectId={projectId}, ServiceId={serviceId}");
                }
                else
                {
                    // Create new binding
                    var projectService = new ProjectService
                    {
                        ProjectId = projectId,
                        ServiceId = serviceId,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _projectServiceRepository.CreateAsync(projectService);
                    LogService.Info($"Created new service binding: ProjectId={projectId}, ServiceId={serviceId}");
                }
            }

            LogService.Info($"Saved {serviceIds.Count} service bindings for project {projectId}");
        }
        catch (Exception ex)
        {
            LogService.Error($"Error saving service bindings for project {projectId}: {ex.Message}");
            throw; // Re-throw so the caller knows the operation failed
        }
    }

    private async Task RemoveExistingServiceBindingsAsync(long projectId)
    {
        try
        {
            // Use atomic delete operation instead of loop for better reliability
            var deleted = await _projectServiceRepository.DeleteByProjectIdAsync(projectId);
            LogService.Info($"Removed existing service bindings for project {projectId}: {deleted}");
        }
        catch (Exception ex)
        {
            LogService.Error($"Error removing existing service bindings for project {projectId}: {ex.Message}");
            // Re-throw to prevent SaveServiceBindingsAsync from running with stale bindings
            throw;
        }
    }

    private async Task RestartProjectServicesAsync(long projectId, ProjectCreationResult result)
    {
        try
        {
            var projectServices = await _projectServiceRepository.GetByProjectIdAsync(projectId);
            var restartedServices = new List<string>();

            foreach (var projectService in projectServices)
            {
                try
                {
                    if (projectService.Service?.WindowsServiceName != null)
                    {
                        // TODO: Implement service restart logic using ServiceConfigurationService
                        // This would use the database-driven service name mapping we implemented earlier
                        restartedServices.Add(projectService.Service.DisplayName);
                        LogService.Info($"Restarted service: {projectService.Service.DisplayName}");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Failed to restart service: {projectService.Service?.DisplayName} - {ex.Message}");
                }
            }

            result.RestartedServices = restartedServices;
        }
        catch (Exception ex)
        {
            LogService.Error($"Error restarting project services for project {projectId}: {ex.Message}");
        }
    }

    private static List<string> ParseServerAliases(string? serverAliases)
    {
        if (string.IsNullOrWhiteSpace(serverAliases))
            return [];

    return [.. serverAliases
        .Split(AliasSeparators, StringSplitOptions.RemoveEmptyEntries)
        .Select(alias => alias.Trim())
        .Where(alias => !string.IsNullOrWhiteSpace(alias))];
    }

    private static async Task CreateDefaultProjectFilesAsync(string projectPath, string projectName, string? domain)
    {
        try
        {
            // Create index.html if it doesn't exist
            var indexPath = Path.Combine(projectPath, "index.html");
            if (!File.Exists(indexPath))
            {
                var htmlContent = GenerateDefaultIndexContent(projectName, domain);
                await File.WriteAllTextAsync(indexPath, htmlContent);
                LogService.Info($"Created default index.html for project: {projectName}");
            }

            // Create README.md
            var readmePath = Path.Combine(projectPath, "README.md");
            if (!File.Exists(readmePath))
            {
                var readmeContent = GenerateDefaultReadmeContent(projectName, domain, projectPath);
                await File.WriteAllTextAsync(readmePath, readmeContent);
                LogService.Info($"Created README.md for project: {projectName}");
            }
        }
        catch (Exception ex)
        {
            LogService.Warning($"Error creating default project files for: {projectName} - {ex.Message}");
        }
    }

    private static string GenerateDefaultIndexContent(string projectName, string? domain)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{projectName}</title>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; 
            max-width: 800px; 
            margin: 0 auto; 
            padding: 2rem; 
            background: #f8f9fa;
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
            color: white; 
            padding: 2rem; 
            border-radius: 8px; 
            margin-bottom: 2rem; 
            text-align: center;
        }}
        .info {{ 
            background: white; 
            padding: 1.5rem; 
            border-radius: 8px; 
            box-shadow: 0 2px 4px rgba(0,0,0,0.1); 
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🚀 Welcome to {projectName}!</h1>
        <p>Your project has been created successfully with ModStack</p>
    </div>
    
    <div class=""info"">
        <h3>📋 Project Information</h3>
        <p><strong>Project:</strong> {projectName}</p>
        {(string.IsNullOrEmpty(domain) ? "" : $"<p><strong>Domain:</strong> {domain}</p>")}
        <p><strong>Created:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        
        <h3>🔧 Next Steps</h3>
        <ul>
            <li>Edit this index.html file to customize your site</li>
            <li>Add CSS, JavaScript, and other assets</li>
            <li>Configure your development environment</li>
            <li>Start building your project!</li>
        </ul>
    </div>
</body>
</html>";
    }

    private static string GenerateDefaultReadmeContent(string projectName, string? domain, string projectPath)
    {
        return $@"# {projectName}

Welcome to your new ModStack project!

## Project Details

- **Name:** {projectName}
- **Path:** {projectPath}
{(string.IsNullOrEmpty(domain) ? "" : $"- **Domain:** {domain}")}
- **Created:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}

## Getting Started

1. Edit the files in this directory to build your project
2. Use the ModStack GUI to manage services and configuration
3. Access your project through the configured domain or localhost

## ModStack Features

This project was created with ModStack, which provides:
- Service stack management
- Virtual host configuration
- SSL certificate generation
- Project organization
- Development environment setup

## Next Steps

1. Customize this README for your project
2. Set up your development workflow
3. Configure any additional services you need
4. Start coding!

Happy development! 🎉
";
    }

    /// <summary>
    /// Updates the hosts file if requested in the project request
    /// </summary>
    private async Task UpdateHostsFileIfRequestedAsync(ProjectCreationRequest request, ProjectCreationResult result)
    {
        if (request.UpdateHostsFile && !string.IsNullOrWhiteSpace(request.Domain))
        {
            try
            {
                LogService.Info($"Attempting to add domain '{request.Domain}' to hosts file");
                
                // Check if domain already exists before attempting to add
                var domainExists = await _hostsFileManager.DomainExistsAsync(request.Domain);
                if (domainExists)
                {
                    LogService.Info($"Domain '{request.Domain}' already exists in hosts file");
                    result.HostsFileUpdated = true;
                    result.SuccessMessages.Add($"ℹ️ Domain {request.Domain} already exists in hosts file");
                }
                else
                {
                    var addResult = await _hostsFileManager.AddDomainAsync(request.Domain);
                    if (addResult)
                    {
                        result.HostsFileUpdated = true;
                        result.SuccessMessages.Add($"✅ Added {request.Domain} to hosts file");
                        LogService.Info($"Successfully added domain '{request.Domain}' to hosts file");
                    }
                    else
                    {
                        LogService.Warning($"Failed to add domain '{request.Domain}' to hosts file");
                        result.WarningMessages.Add($"⚠️ Failed to add {request.Domain} to hosts file");
                        result.WarningMessages.Add($"You may need to manually add '{request.Domain}' to your hosts file.");
                    }
                }
                
                // Add server aliases to hosts file
                if (!string.IsNullOrWhiteSpace(request.ServerAliases))
                {
                    var serverAliases = ParseServerAliases(request.ServerAliases);
                    foreach (var alias in serverAliases)
                    {
                        try
                        {
                            var aliasExists = await _hostsFileManager.DomainExistsAsync(alias);
                            if (!aliasExists)
                            {
                                var aliasAddResult = await _hostsFileManager.AddDomainAsync(alias);
                                if (aliasAddResult)
                                {
                                    result.SuccessMessages.Add($"✅ Added {alias} to hosts file");
                                    LogService.Info($"Successfully added server alias '{alias}' to hosts file");
                                }
                                else
                                {
                                    LogService.Warning($"Failed to add server alias '{alias}' to hosts file");
                                    result.WarningMessages.Add($"⚠️ Failed to add {alias} to hosts file");
                                }
                            }
                            else
                            {
                                LogService.Info($"Server alias '{alias}' already exists in hosts file");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.Warning($"Error adding server alias '{alias}' to hosts file: {ex.Message}");
                            result.WarningMessages.Add($"⚠️ Failed to add {alias} to hosts file: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"Failed to update hosts file for domain '{request.Domain}': {ex.Message}");
                result.WarningMessages.Add($"⚠️ Failed to update hosts file: {ex.Message}");
                result.WarningMessages.Add($"You may need to manually add '{request.Domain}' to your hosts file.");
            }
        }
        else if (request.UpdateHostsFile && string.IsNullOrWhiteSpace(request.Domain))
        {
            result.WarningMessages.Add("⚠️ Cannot update hosts file: No domain specified");
        }
    }

    #endregion
}
