using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ModStack.GUI.Services;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Handles project form logic including auto-generation and validation
    /// </summary>
    public class ProjectFormHandler
    {
        private readonly ILogger<ProjectFormHandler> _logger;
        private bool _isEditMode;

        public ProjectFormHandler(ILogger<ProjectFormHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sets the edit mode state
        /// </summary>
        public void SetEditMode(bool isEditMode)
        {
            _isEditMode = isEditMode;
        }

        /// <summary>
        /// Updates project fields based on project name input
        /// </summary>
        public void UpdateProjectFields(TextBox projectNameTextBox, TextBox domainTextBox, TextBox projectPathTextBox, TextBox documentRootTextBox)
        {
            try
            {
                if (projectNameTextBox?.Text == null || string.IsNullOrWhiteSpace(projectNameTextBox.Text))
                    return;

                var projectName = projectNameTextBox.Text.Trim();
                
                // Only auto-generate if we're not in edit mode (preserve existing values when editing)
                if (_isEditMode)
                    return;

                // Generate clean project name for file system use
                var cleanName = GenerateCleanProjectName(projectName);
                
                // Auto-generate Domain (only if empty)
                if (domainTextBox != null && string.IsNullOrWhiteSpace(domainTextBox.Text))
                {
                    domainTextBox.Text = $"{cleanName}.local";
                    Debug.WriteLine($"🏷️ Auto-generated domain: {cleanName}.local");
                }
                
                // Auto-generate Project Path (only if empty)
                if (projectPathTextBox != null && string.IsNullOrWhiteSpace(projectPathTextBox.Text))
                {
                    var defaultProjectsPath = @"C:\ModStack\Projects";
                    var generatedPath = Path.Combine(defaultProjectsPath, cleanName);
                    projectPathTextBox.Text = generatedPath;
                    Debug.WriteLine($"📁 Auto-generated project path: {generatedPath}");
                }
                
                // Auto-generate Document Root (only if empty)
                if (documentRootTextBox != null && string.IsNullOrWhiteSpace(documentRootTextBox.Text))
                {
                    // Use the same path as project path by default (can be changed by user)
                    var projectPath = projectPathTextBox?.Text ?? "";
                    if (!string.IsNullOrWhiteSpace(projectPath))
                    {
                        // For certain project types, suggest a 'public' subfolder
                        var documentRoot = SuggestDocumentRoot(projectName, projectPath);
                        documentRootTextBox.Text = documentRoot;
                        Debug.WriteLine($"🌐 Auto-generated document root: {documentRoot}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle auto-generation errors
                Debug.WriteLine($"❌ Error updating project fields: {ex.Message}");
                _logger?.LogWarning(ex, "Error updating project fields for project name: {ProjectName}", projectNameTextBox?.Text);
            }
        }

        /// <summary>
        /// Updates document root when project path changes
        /// </summary>
        public void UpdateDocumentRootFromProjectPath(TextBox projectPathTextBox, TextBox documentRootTextBox)
        {
            try
            {
                // Only auto-update document root if we're not in edit mode
                if (_isEditMode)
                    return;
                    
                if (projectPathTextBox?.Text != null && documentRootTextBox != null)
                {
                    var projectPath = projectPathTextBox.Text.Trim();
                    
                    // Only update if document root is empty or currently matches the previous project path
                    if (string.IsNullOrWhiteSpace(documentRootTextBox.Text) || 
                        documentRootTextBox.Text.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        documentRootTextBox.Text = projectPath;
                        Debug.WriteLine($"🌐 Updated document root to match project path: {projectPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently handle document root update errors
                Debug.WriteLine($"❌ Error updating document root: {ex.Message}");
                _logger?.LogWarning(ex, "Error updating document root from project path");
            }
        }

        /// <summary>
        /// Validates project form inputs
        /// </summary>
        public ValidationResult ValidateProjectForm(TextBox projectNameTextBox, TextBox projectPathTextBox, TextBox domainTextBox, bool isEditMode)
        {
            var result = new ValidationResult();

            // Validate project name
            if (string.IsNullOrWhiteSpace(projectNameTextBox?.Text))
            {
                result.AddError("Please enter a project name.");
                return result;
            }

            // Validate project path
            if (string.IsNullOrWhiteSpace(projectPathTextBox?.Text))
            {
                result.AddError("Please enter a project path.");
                return result;
            }

            // Validate domain format if provided
            if (!string.IsNullOrWhiteSpace(domainTextBox?.Text))
            {
                var domain = domainTextBox.Text.Trim();
                if (!IsValidDomain(domain))
                {
                    result.AddError("Please enter a valid domain name (e.g., example.local).");
                    return result;
                }
            }

            // Validate project path format
            try
            {
                var path = projectPathTextBox.Text.Trim();
                if (!Path.IsPathRooted(path))
                {
                    result.AddWarning("Project path should be an absolute path.");
                }

                // Check for invalid path characters
                var invalidChars = Path.GetInvalidPathChars();
                if (path.IndexOfAny(invalidChars) >= 0)
                {
                    result.AddError("Project path contains invalid characters.");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.AddError($"Invalid project path: {ex.Message}");
                return result;
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// Validates that a domain name is suitable for hosts file usage
        /// </summary>
        private bool IsValidDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return false;

            // Basic domain validation
            if (domain.Length > 253)
                return false;

            // Check for invalid characters
            var invalidChars = new[] { ' ', '\t', '\r', '\n', '#' };
            if (domain.IndexOfAny(invalidChars) >= 0)
                return false;

            // Must contain at least one dot for local development domains
            if (!domain.Contains('.'))
                return false;

            return true;
        }

        /// <summary>
        /// Validates for conflicts with existing projects
        /// </summary>
        public async Task<ValidationResult> ValidateProjectConflictsAsync(string projectName, string projectPath, string domain, bool isEditMode, long? currentProjectId = null)
        {
            var result = new ValidationResult();

            try
            {
                // Get project repository from service locator
                var projectRepository = ServiceLocator.GetService<ModStack.Data.Interfaces.IProjectRepository>();
                if (projectRepository == null)
                {
                    result.AddWarning("Could not validate project conflicts - repository not available.");
                    return result;
                }

                // Check for existing project with same name
                var existingProjectByName = await projectRepository.GetByNameAsync(projectName);
                if (existingProjectByName != null && (!isEditMode || existingProjectByName.Id != currentProjectId))
                {
                    result.AddError($"A project with the name '{projectName}' already exists.");
                    return result;
                }

                // Get all projects for domain and path validation
                var allProjects = await projectRepository.GetAllAsync();

                // Check for existing project with same domain (if domain provided)
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    var existingProjectByDomain = allProjects.FirstOrDefault(p => 
                        !string.IsNullOrWhiteSpace(p.Domain) && 
                        p.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) &&
                        (!isEditMode || p.Id != currentProjectId));

                    if (existingProjectByDomain != null)
                    {
                        result.AddError($"A project with the domain '{domain}' already exists (Project: {existingProjectByDomain.Name}).");
                        return result;
                    }
                }

                // Check for existing project with same path
                var existingProjectByPath = allProjects.FirstOrDefault(p => 
                    !string.IsNullOrWhiteSpace(p.Path) && 
                    p.Path.Equals(projectPath, StringComparison.OrdinalIgnoreCase) &&
                    (!isEditMode || p.Id != currentProjectId));

                if (existingProjectByPath != null)
                {
                    result.AddError($"A project with the path '{projectPath}' already exists (Project: {existingProjectByPath.Name}).");
                    return result;
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error validating project conflicts: {ex.Message}");
                _logger?.LogError(ex, "Error validating project conflicts for project: {ProjectName}", projectName);
                result.AddWarning("Could not validate project conflicts - please check manually.");
            }

            return result;
        }

        /// <summary>
        /// Suggests an appropriate document root based on project name and path
        /// </summary>
        private string SuggestDocumentRoot(string projectName, string projectPath)
        {
            try
            {
                // Check project name for common frameworks that use 'public' folder
                var nameLower = projectName.ToLowerInvariant();
                
                // Laravel, Symfony, and similar PHP frameworks typically use 'public' folder
                if (nameLower.Contains("laravel") || nameLower.Contains("symfony") || 
                    nameLower.Contains("api") || nameLower.Contains("app"))
                {
                    var publicPath = Path.Combine(projectPath, "public");
                    Debug.WriteLine($"🎯 Suggested 'public' subfolder for framework project: {publicPath}");
                    return publicPath;
                }
                
                // React, Vue, Angular projects typically use 'dist' or 'build' folder for production
                if (nameLower.Contains("react") || nameLower.Contains("vue") || 
                    nameLower.Contains("angular") || nameLower.Contains("frontend"))
                {
                    var distPath = Path.Combine(projectPath, "dist");
                    Debug.WriteLine($"🎯 Suggested 'dist' subfolder for frontend project: {distPath}");
                    return distPath;
                }
                
                // For most other projects, use the project path directly
                return projectPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error suggesting document root: {ex.Message}");
                return projectPath; // Fallback to project path
            }
        }

        /// <summary>
        /// Generates a clean project name suitable for file system use
        /// </summary>
        private string GenerateCleanProjectName(string projectName)
        {
            // Convert to lowercase
            var cleanName = projectName.ToLowerInvariant();
            
            // Remove invalid characters and replace with hyphens
            cleanName = Regex.Replace(cleanName, @"[^a-z0-9\-_]", "-");
            
            // Replace multiple consecutive hyphens with single hyphen
            cleanName = Regex.Replace(cleanName, @"-+", "-");
            
            // Remove leading/trailing hyphens
            cleanName = cleanName.Trim('-');
            
            // Ensure it's not empty
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "project";
            }
            
            return cleanName;
        }

        /// <summary>
        /// Creates a project directory if it doesn't exist
        /// </summary>
        public bool EnsureProjectDirectoryExistsAsync(string projectPath, bool createIfNotExists)
        {
            try
            {
                if (Directory.Exists(projectPath))
                {
                    Debug.WriteLine($"✅ Project directory already exists: {projectPath}");
                    return true;
                }

                if (createIfNotExists)
                {
                    Directory.CreateDirectory(projectPath);
                    Debug.WriteLine($"📁 Created project directory: {projectPath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error creating project directory: {ex.Message}");
                _logger?.LogError(ex, "Failed to create project directory: {ProjectPath}", projectPath);
                return false;
            }
        }

        /// <summary>
        /// Creates a basic project structure with common files
        /// </summary>
        public async Task CreateProjectStructureAsync(string projectPath, string projectName, string domain)
        {
            try
            {
                // Create basic index.html file
                var indexPath = Path.Combine(projectPath, "index.html");
                if (!File.Exists(indexPath))
                {
                    var htmlContent = GenerateIndexHtmlContent(projectName, domain);
                    await File.WriteAllTextAsync(indexPath, htmlContent);
                    Debug.WriteLine($"📝 Created index.html: {indexPath}");
                }

                // Create a basic README.md
                var readmePath = Path.Combine(projectPath, "README.md");
                if (!File.Exists(readmePath))
                {
                    var readmeContent = GenerateReadmeContent(projectName, domain);
                    await File.WriteAllTextAsync(readmePath, readmeContent);
                    Debug.WriteLine($"📝 Created README.md: {readmePath}");
                }

                // Create basic project scripts
                await CreateProjectScriptsAsync(projectPath, projectName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error creating project structure: {ex.Message}");
                _logger?.LogError(ex, "Failed to create project structure for: {ProjectPath}", projectPath);
            }
        }

        private string GenerateIndexHtmlContent(string projectName, string domain)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <title>{projectName}</title>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 800px; margin: 0 auto; padding: 2rem; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 2rem; border-radius: 8px; margin-bottom: 2rem; }}
        .info {{ background: white; padding: 1.5rem; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        .next-steps {{ background: #f8f9fa; padding: 1.5rem; border-radius: 8px; margin-top: 1rem; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>🚀 Welcome to {projectName}!</h1>
        <p>Your project has been created successfully with ModStack</p>
    </div>
    
    <div class=""info"">
        <h3>📋 Project Information</h3>
        <p><strong>Domain:</strong> {domain}</p>
        <p><strong>Status:</strong> <span style=""color: #28a745; font-weight: bold;"">✅ Ready for Development</span></p>
    </div>

    <div class=""next-steps"">
        <h3>🔧 Next Steps</h3>
        <ul>
            <li>Edit this index.html file to start building your project</li>
            <li>Add CSS files in a 'css' directory</li>
            <li>Add JavaScript files in a 'js' directory</li>
            <li>Configure your development stack in ModStack</li>
        </ul>
        
        <h3>📚 Resources</h3>
        <ul>
            <li><a href=""https://developer.mozilla.org/en-US/docs/Web/HTML"">HTML Documentation</a></li>
            <li><a href=""https://developer.mozilla.org/en-US/docs/Web/CSS"">CSS Documentation</a></li>
            <li><a href=""https://developer.mozilla.org/en-US/docs/Web/JavaScript"">JavaScript Documentation</a></li>
        </ul>
    </div>
</body>
</html>";
        }

        private string GenerateReadmeContent(string projectName, string domain)
        {
            return $@"# {projectName}

This project was created with ModStack for local development.

## 🌐 Access

- **Local URL:** http://{domain}
- **Development Server:** Configured with ModStack

## 🚀 Getting Started

1. Edit `index.html` to start building your project
2. Add your CSS files to a `css/` directory  
3. Add your JavaScript files to a `js/` directory
4. Configure additional services in ModStack as needed

## 📁 Project Structure

```
{projectName}/
├── index.html          # Main HTML file
├── README.md           # This file
├── css/               # Stylesheets (create as needed)
├── js/                # JavaScript files (create as needed)
└── assets/            # Images and other assets (create as needed)
```

## 🔧 Development

This project is configured to work with:
- Apache web server (via ModStack)
- Local domain: {domain}
- Document root: Current directory

## 📝 Notes

- This project was created with ModStack
- The local domain has been added to your hosts file
- Apache virtual host has been configured automatically

Happy coding! 🎉
";
        }

        private async Task CreateProjectScriptsAsync(string projectPath, string projectName)
        {
            // Create a simple batch file for Windows users
            var serverBatPath = Path.Combine(projectPath, "start-server.bat");
            var serverScript = $@"@echo off
echo.
echo ===============================================
echo   {projectName} - Development Server
echo ===============================================
echo.
echo This project is configured to work with ModStack.
echo.
echo To run this project, you need to:
echo 1. Install Python (https://python.org) or Node.js (https://nodejs.org)
echo 2. Or use another web server like Apache, Nginx, or IIS
echo 3. Or use a tool like XAMPP, WAMP, or similar
echo.
echo For now, you can double-click index.html to open it in your browser
echo (but it will use file:// protocol instead of http://)
echo.
pause

:end
";
            await File.WriteAllTextAsync(serverBatPath, serverScript);

            // Create a PowerShell version too
            var serverPsPath = Path.Combine(projectPath, "start-server.ps1");
            var psScript = $@"# {projectName} - Development Server Script

Write-Host ""==============================================="" -ForegroundColor Cyan
Write-Host ""   {projectName} - Development Server"" -ForegroundColor Cyan  
Write-Host ""==============================================="" -ForegroundColor Cyan
Write-Host """"
Write-Host ""This project is configured to work with ModStack."" -ForegroundColor Green
Write-Host """"
Write-Host ""To run this project, you need to:""
Write-Host ""1. Install Python (https://python.org) or Node.js (https://nodejs.org)""
Write-Host ""2. Or use another web server like Apache, Nginx, or IIS""
Write-Host ""3. Or use a tool like XAMPP, WAMP, or similar""
Write-Host """"
Write-Host ""For now, you can open index.html directly in your browser""
Write-Host ""(but it will use file:// protocol instead of http://)""
Pause
";
            await File.WriteAllTextAsync(serverPsPath, psScript);
        }
    }

    /// <summary>
    /// Represents the result of form validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; } = false;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        public string GetErrorMessage()
        {
            return string.Join(Environment.NewLine, Errors);
        }

        public string GetWarningMessage()
        {
            return string.Join(Environment.NewLine, Warnings);
        }
    }
}
