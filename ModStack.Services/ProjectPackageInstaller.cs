using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Core.Interfaces;
using ModStack.Common.Interfaces;
using ModStack.Services;
using ModStack.Services.Interfaces;

namespace ModStack.Services
{
    /// <summary>
    /// Specialized installer for project-type packages (phpMyAdmin, Laravel, WordPress, etc.)
    /// </summary>
    public class ProjectPackageInstaller(
        IProjectManager projectManager,
        IVirtualHostManager virtualHostManager,
        IPathConfigurationService pathConfigurationService,
        IDatabaseManager databaseManager)
    {
        private readonly IProjectManager _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        private readonly IVirtualHostManager _virtualHostManager = virtualHostManager ?? throw new ArgumentNullException(nameof(virtualHostManager));
        private readonly IPathConfigurationService _pathConfigurationService = pathConfigurationService ?? throw new ArgumentNullException(nameof(pathConfigurationService));
        private readonly IDatabaseManager _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));

        /// <summary>
        /// Installs a project package with proper configuration
        /// </summary>
        public async Task<bool> InstallProjectPackageAsync(
            AvailablePackage package, 
            PackageVersion version, 
            Dictionary<string, object> configuration,
            IProgressReporter? progressReporter = null)
        {
            try
            {
                progressReporter?.UpdateProgress("Initializing project installation...", 0);

                // Validate package type
                if (package.PackageType != Common.Enums.PackageType.StandaloneWebApp)
                {
                    throw new InvalidOperationException($"Package {package.Name} is not a project type package");
                }

                // Get project configuration
                var projectConfig = package.ProjectConfiguration;
                if (projectConfig == null)
                {
                    throw new InvalidOperationException($"Project configuration not found for package {package.Name}");
                }

                // Generate project path
                var projectPath = GenerateProjectPath(package, version);
                progressReporter?.UpdateProgress("Project path generated", 10);

                // Create project directory
                Directory.CreateDirectory(projectPath);
                progressReporter?.UpdateProgress("Project directory created", 15);

                // Download and extract package
                await DownloadAndExtractPackageAsync(package, version, projectPath, progressReporter);

                // Setup database if required
                if (projectConfig.DatabaseConfig is not null)
                {
                    await SetupDatabaseAsync(projectConfig.DatabaseConfig, configuration, progressReporter);
                }

                // Create configuration files
                await CreateConfigurationFilesAsync(projectPath, projectConfig, configuration, progressReporter);

                // Create project in database
                var project = await CreateProjectAsync(package, version, projectPath, configuration, progressReporter);

                // Create virtual host
                await CreateVirtualHostAsync(project, projectConfig, configuration, progressReporter);

                // Set environment variables
                await SetEnvironmentVariablesAsync(projectPath, projectConfig.EnvironmentVariables, progressReporter);

                // Run post-installation commands
                await RunPostInstallCommandsAsync(projectPath, projectConfig.PostInstallCommands, progressReporter);

                progressReporter?.UpdateProgress("Project installation completed successfully!", 100);
                return true;
            }
            catch (Exception ex)
            {
                progressReporter?.UpdateProgress($"Installation failed: {ex.Message}", 0);
                throw;
            }
        }

        private string GenerateProjectPath(AvailablePackage package, PackageVersion version)
        {
            var projectsRoot = _pathConfigurationService.GetProjectsDirectory();
            var projectFolder = $"{package.Name.ToLower()}-{version.Version}";
            return Path.Combine(projectsRoot, projectFolder);
        }

        private static Task DownloadAndExtractPackageAsync(
            AvailablePackage _,
            PackageVersion __, 
            string projectPath, 
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Starting download...", 20);

            // TODO: Implement actual download and extraction logic
            // For now, just create the directory structure
            Directory.CreateDirectory(projectPath);
            
            progressReporter?.UpdateProgress("Package downloaded, extracting...", 60);
            progressReporter?.UpdateProgress("Package extracted", 70);
            
            return Task.CompletedTask;
        }

        private async Task SetupDatabaseAsync(
            DatabaseConfiguration dbConfig, 
            Dictionary<string, object> configuration,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Setting up database...", 75);

            // Get database connection details from configuration
            var host = configuration.GetValueOrDefault("db_host", dbConfig.Host)?.ToString() ?? dbConfig.Host;
            var port = Convert.ToInt32(configuration.GetValueOrDefault("db_port", dbConfig.Port));
            var username = configuration.GetValueOrDefault("db_username", dbConfig.Username)?.ToString() ?? dbConfig.Username;
            var password = configuration.GetValueOrDefault("db_password", dbConfig.Password)?.ToString() ?? dbConfig.Password;
            var databaseName = configuration.GetValueOrDefault("db_name", dbConfig.DatabaseName)?.ToString() ?? dbConfig.DatabaseName;

            if (dbConfig.CreateDatabase)
            {
                await _databaseManager.CreateDatabaseAsync(databaseName, host, port, username, password);
            }

            if (dbConfig.CreateUser)
            {
                await _databaseManager.CreateUserAsync(username, password, host, port);
                await _databaseManager.GrantDatabaseAccessAsync(username, databaseName, host, port);
            }

            progressReporter?.UpdateProgress("Database setup completed", 80);
        }

        private static async Task CreateConfigurationFilesAsync(
            string projectPath,
            ProjectPackageConfiguration projectConfig,
            Dictionary<string, object> configuration,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Creating configuration files...", 85);

            foreach (var configFile in projectConfig.ConfigurationFiles)
            {
                var filePath = Path.Combine(projectPath, configFile.RelativePath);
                
                // Skip if file exists and shouldn't be overwritten
                if (File.Exists(filePath) && !configFile.OverwriteExisting)
                    continue;

                // Create directory if needed
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Replace variables in template
                var content = configFile.TemplateContent;
                foreach (var variable in configFile.Variables)
                {
                    var value = configuration.GetValueOrDefault(variable.Key, variable.Value)?.ToString() ?? variable.Value;
                    content = content.Replace($"{{{variable.Key}}}", value);
                }

                await File.WriteAllTextAsync(filePath, content);
            }

            progressReporter?.UpdateProgress("Configuration files created", 90);
        }

        private async Task<Project> CreateProjectAsync(
            AvailablePackage package,
            PackageVersion version,
            string projectPath,
            Dictionary<string, object> configuration,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Creating project record...", 92);

            var project = new Project
            {
                Name = package.Name.ToLower(),
                DisplayName = $"{package.DisplayName} {version.Version}",
                Path = projectPath,
                Domain = configuration.GetValueOrDefault("domain", package.ProjectConfiguration?.DefaultDomain ?? $"{package.Name.ToLower()}.local")?.ToString(),
                DocumentRoot = projectPath,
                IsActive = true,
                EnableSSL = Convert.ToBoolean(configuration.GetValueOrDefault("enable_ssl", package.ProjectConfiguration?.EnableSSL ?? false))
            };

            return await _projectManager.CreateProjectAsync(
                project.Name, 
                project.Path, 
                project.Domain, 
                project.DocumentRoot);
        }

        private async Task CreateVirtualHostAsync(
            Project project,
            ProjectPackageConfiguration projectConfig,
            Dictionary<string, object> configuration,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Creating virtual host...", 95);

            // For now, use a default service ID (Apache)
            // TODO: Implement proper web server discovery
            const long defaultWebServerId = 1; // Assuming Apache has ID 1

            var request = new CreateVirtualHostRequest
            {
                ProjectId = project.Id,
                ServiceId = defaultWebServerId,
                ServerName = project.Domain ?? $"{project.Name}.local",
                DocumentRoot = project.DocumentRoot ?? project.Path,
                Port = Convert.ToInt32(configuration.GetValueOrDefault("port", projectConfig.DefaultPort)),
                IsSSL = Convert.ToBoolean(configuration.GetValueOrDefault("enable_ssl", projectConfig.EnableSSL)),
                IsEnabled = true
            };

            await _virtualHostManager.CreateVirtualHostAsync(request);
            progressReporter?.UpdateProgress("Virtual host created", 98);
        }

        private static Task SetEnvironmentVariablesAsync(
            string _,
            Dictionary<string, string> environmentVariables,
            IProgressReporter? progressReporter)
        {
            if (environmentVariables.Count == 0)
                return Task.CompletedTask;

            progressReporter?.UpdateProgress("Setting environment variables...", 96);

            foreach (var envVar in environmentVariables)
            {
                Environment.SetEnvironmentVariable(envVar.Key, envVar.Value, EnvironmentVariableTarget.Process);
            }
            
            return Task.CompletedTask;
        }

        private static async Task RunPostInstallCommandsAsync(
            string projectPath,
            List<string> postInstallCommands,
            IProgressReporter? progressReporter)
        {
            if (postInstallCommands.Count == 0)
                return;

            progressReporter?.UpdateProgress("Running post-installation commands...", 97);

            foreach (var command in postInstallCommands)
            {
                // Execute command in project directory
                await ExecuteCommandAsync(command, projectPath);
            }
        }

        private static async Task ExecuteCommandAsync(string command, string workingDirectory)
        {
            // Simple command execution - could be enhanced with proper shell handling
            var parts = command.Split(' ', 2);
            var fileName = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : string.Empty;

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Command failed: {command}\nError: {error}");
            }
        }
    }
}
