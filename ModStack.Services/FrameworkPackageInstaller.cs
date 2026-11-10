using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Core.Interfaces;
using ModStack.Common.Interfaces;
using ModStack.Common.Security;
using ModStack.Services;
using ModStack.Services.Interfaces;

namespace ModStack.Services
{
    /// <summary>
    /// Specialized installer for framework-type packages (Laravel, WordPress, etc.)
    /// Installs frameworks within existing projects
    /// </summary>
    public class FrameworkPackageInstaller(
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
        /// Installs a framework package within an existing project
        /// </summary>
        public async Task<bool> InstallFrameworkPackageAsync(
            AvailablePackage package,
            PackageVersion version,
            ProjectPackageInstallationConfig installationConfig,
            IProgressReporter? progressReporter = null)
        {
            try
            {
                progressReporter?.UpdateProgress("Initializing framework installation...", 0);

                // Validate package type
                if (package.PackageType != Common.Enums.PackageType.Framework)
                {
                    throw new InvalidOperationException($"Package {package.Name} is not a framework type package");
                }

                // Validate target project
                if (installationConfig.TargetProject == null || installationConfig.TargetProject.Id == 0)
                {
                    throw new InvalidOperationException("Target project is required for framework installation");
                }

                // Generate installation path
                var fullInstallPath = GenerateInstallationPath(package, installationConfig);
                progressReporter?.UpdateProgress("Installation path generated", 10);

                // Create installation directory
                Directory.CreateDirectory(fullInstallPath);
                progressReporter?.UpdateProgress("Installation directory created", 15);

                // Download and extract package
                await DownloadAndExtractFrameworkAsync(package, version, fullInstallPath, progressReporter);

                // Setup database if required
                if (installationConfig.DatabaseConfig != null)
                {
                    await SetupFrameworkDatabaseAsync(package, installationConfig.DatabaseConfig, installationConfig.TargetProject, progressReporter);
                }

                // Create configuration files
                await CreateFrameworkConfigurationFilesAsync(fullInstallPath, installationConfig, progressReporter);

                // Create virtual host or subdomain if required
                // Note: For project-based installations, virtual hosts are typically already configured
                if (installationConfig.CreateVirtualHost)
                {
                    await CreateFrameworkVirtualHostAsync(installationConfig, progressReporter);
                }

                // Set environment variables
                await SetFrameworkEnvironmentVariablesAsync(fullInstallPath, installationConfig.EnvironmentVariables, progressReporter);

                // Run post-installation commands
                await RunFrameworkPostInstallCommandsAsync(fullInstallPath, installationConfig.PostInstallCommands, progressReporter);

                // Update project configuration
                await UpdateProjectConfigurationAsync(installationConfig.TargetProject, package, version, installationConfig, progressReporter);

                progressReporter?.UpdateProgress("Framework installation completed successfully!", 100);
                return true;
            }
            catch (Exception ex)
            {
                progressReporter?.UpdateProgress($"Installation failed: {ex.Message}", 0);
                throw;
            }
        }

        /// <summary>
        /// Installs a standalone web app (like phpMyAdmin) with project selection
        /// </summary>
        public async Task<bool> InstallStandaloneWebAppAsync(
            AvailablePackage package,
            PackageVersion version,
            ProjectPackageInstallationConfig installationConfig,
            IProgressReporter? progressReporter = null)
        {
            try
            {
                progressReporter?.UpdateProgress("Initializing web application installation...", 0);

                // Validate package type
                if (package.PackageType != Common.Enums.PackageType.StandaloneWebApp)
                {
                    throw new InvalidOperationException($"Package {package.Name} is not a standalone web application");
                }

                // Generate installation path
                var fullInstallPath = GenerateInstallationPath(package, installationConfig);
                progressReporter?.UpdateProgress("Installation path generated", 10);

                // Create installation directory
                Directory.CreateDirectory(fullInstallPath);
                progressReporter?.UpdateProgress("Installation directory created", 15);

                // Download and extract package
                await DownloadAndExtractFrameworkAsync(package, version, fullInstallPath, progressReporter);

                // Setup database if required
                if (installationConfig.DatabaseConfig != null)
                {
                    await SetupFrameworkDatabaseAsync(package, installationConfig.DatabaseConfig, installationConfig.TargetProject, progressReporter);
                }

                // Create configuration files
                await CreateFrameworkConfigurationFilesAsync(fullInstallPath, installationConfig, progressReporter);

                // Set environment variables
                await SetFrameworkEnvironmentVariablesAsync(fullInstallPath, installationConfig.EnvironmentVariables, progressReporter);

                // Run post-installation commands
                await RunFrameworkPostInstallCommandsAsync(fullInstallPath, installationConfig.PostInstallCommands, progressReporter);

                progressReporter?.UpdateProgress("Web application installation completed successfully!", 100);
                return true;
            }
            catch (Exception ex)
            {
                progressReporter?.UpdateProgress($"Installation failed: {ex.Message}", 0);
                throw;
            }
        }

        private static string GenerateInstallationPath(AvailablePackage package, ProjectPackageInstallationConfig config)
        {
            var projectPath = config.TargetProject.Path;
            var installPath = string.IsNullOrEmpty(config.InstallationPath) 
                ? package.Name.ToLower() 
                : config.InstallationPath;
            
            return Path.Combine(projectPath, installPath);
        }

        private static async Task DownloadAndExtractFrameworkAsync(
            AvailablePackage package,
            PackageVersion version,
            string installPath,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Starting download...", 20);

            try
            {
                // Get download URL
                var downloadUrl = GetDownloadUrl(package, version);
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    throw new Exception($"No download URL available for {package.Name} version {version.Version}");
                }

                // Download package
                var downloadPath = await DownloadPackageAsync(downloadUrl, version.Checksum, progressReporter);
                
                progressReporter?.UpdateProgress("Package downloaded, extracting...", 60);
                
                // Extract package
                await ExtractPackageAsync(downloadPath, installPath, progressReporter);
                
                progressReporter?.UpdateProgress("Package extracted", 70);
                
                // Cleanup downloaded file
                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }
            }
            catch (Exception ex)
            {
                progressReporter?.UpdateProgress($"Download/extraction failed: {ex.Message}", 0);
                throw;
            }
        }

        private static string GetDownloadUrl(AvailablePackage package, PackageVersion version)
        {
            // Check if version has predefined URLs
            if (version.DownloadUrls?.Count > 0)
            {
                return version.DownloadUrls.First();
            }

            // Generate URL based on package name
            return package.Name.ToLower() switch
            {
                "phpmyadmin" => $"https://files.phpmyadmin.net/phpMyAdmin/{version.Version}/phpMyAdmin-{version.Version}-all-languages.zip",
                "wordpress" => $"https://wordpress.org/wordpress-{version.Version}.zip",
                "laravel" => $"https://github.com/laravel/laravel/archive/refs/tags/v{version.Version}.zip",
                _ => throw new Exception($"No download URL pattern defined for package: {package.Name}")
            };
        }

        private static async Task<string> DownloadPackageAsync(string downloadUrl, string? expectedChecksum, IProgressReporter? progressReporter)
        {
            var uri = SecurityValidator.ValidateHttpsUrlOrThrow(downloadUrl);

            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrEmpty(fileName) || SecurityValidator.ContainsPathTraversalOrRootedSegments(fileName))
            {
                fileName = $"package_{Guid.NewGuid():N}.zip";
            }

            var downloadPath = Path.Combine(Path.GetTempPath(), fileName);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);
            
            // Add headers to mimic browser request
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            progressReporter?.UpdateProgress($"Downloading {fileName}...", 25);
            
            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (response.RequestMessage?.RequestUri is { } finalUri &&
                !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Insecure redirect from HTTPS to '{finalUri.Scheme}' blocked.");
            }
            response.EnsureSuccessStatusCode();
            
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            
            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;
                    
                    if (totalBytes > 0)
                    {
                        var progressPercent = (int)((totalBytesRead * 35) / totalBytes) + 25; // 25-60% for download
                        var mbDownloaded = totalBytesRead / (1024 * 1024);
                        var mbTotal = totalBytes / (1024 * 1024);
                        progressReporter?.UpdateProgress($"Downloaded: {mbDownloaded} MB / {mbTotal} MB", progressPercent);
                    }
                }

                await fileStream.FlushAsync();
            }

            var downloadedSize = new FileInfo(downloadPath).Length;
            if (totalBytes > 0 && downloadedSize != totalBytes)
            {
                File.Delete(downloadPath);
                throw new InvalidOperationException($"Downloaded file size mismatch. Expected {totalBytes} bytes, got {downloadedSize} bytes.");
            }

            try
            {
                await SecurityValidator.VerifyFileSha256Async(downloadPath, expectedChecksum, requireChecksum: false);
            }
            catch
            {
                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }
                throw;
            }
            
            return downloadPath;
        }

        private static async Task ExtractPackageAsync(string zipPath, string extractPath, IProgressReporter? progressReporter)
        {
            string rootFolderName;
            using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
            {
                rootFolderName = GetRootFolderName(archive);
            }

            var skipRootFolder = !string.IsNullOrEmpty(rootFolderName);
            var extractionOptions = new ArchiveExtractionOptions
            {
                EntryPathTransformer = skipRootFolder
                    ? rawPath =>
                    {
                        if (rawPath.StartsWith(rootFolderName + "/", StringComparison.Ordinal) ||
                            rawPath.StartsWith(rootFolderName + "\\", StringComparison.Ordinal))
                        {
                            return rawPath[(rootFolderName.Length + 1)..];
                        }
                        return rawPath;
                    }
                    : null,
                ProgressCallback = (extractedEntries, totalEntries) =>
                {
                    if (totalEntries > 0)
                    {
                        var progressPercent = (int)((extractedEntries * 10) / totalEntries) + 60; // 60-70% for extraction
                        progressReporter?.UpdateProgress($"Extracting files... ({extractedEntries}/{totalEntries})", progressPercent);
                    }
                }
            };

            await SafeArchiveExtractor.ExtractZipSafelyAsync(zipPath, extractPath, extractionOptions);
        }

        private static string GetRootFolderName(System.IO.Compression.ZipArchive archive)
        {
            // Find the most common root folder name
            var rootFolders = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.FullName))
                .Select(e => e.FullName.Split('/')[0])
                .Where(folder => !string.IsNullOrEmpty(folder))
                .GroupBy(folder => folder)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return rootFolders?.Key ?? string.Empty;
        }

        private async Task SetupFrameworkDatabaseAsync(
            AvailablePackage package,
            DatabaseConfiguration dbConfig,
            Project targetProject,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Setting up database...", 75);

            // Use project-specific database naming
            var databaseName = string.IsNullOrEmpty(dbConfig.DatabaseName) 
                ? $"{targetProject.Name}_{package.Name.ToLower()}" 
                : dbConfig.DatabaseName;

            if (dbConfig.CreateDatabase)
            {
                await _databaseManager.CreateDatabaseAsync(
                    databaseName, 
                    dbConfig.Host, 
                    dbConfig.Port, 
                    dbConfig.Username, 
                    dbConfig.Password);
            }

            if (dbConfig.CreateUser)
            {
                var username = string.IsNullOrEmpty(dbConfig.Username) 
                    ? $"{targetProject.Name}_{package.Name.ToLower()}" 
                    : dbConfig.Username;

                await _databaseManager.CreateUserAsync(username, dbConfig.Password, dbConfig.Host, dbConfig.Port);
                await _databaseManager.GrantDatabaseAccessAsync(username, databaseName, dbConfig.Host, dbConfig.Port);
            }

            progressReporter?.UpdateProgress("Database setup completed", 80);
        }

        private static async Task CreateFrameworkConfigurationFilesAsync(
            string installPath,
            ProjectPackageInstallationConfig installationConfig,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Creating configuration files...", 85);

            foreach (var configFile in installationConfig.ConfigurationFiles)
            {
                var filePath = Path.Combine(installPath, configFile.RelativePath);
                
                // Skip if file exists and shouldn't be overwritten
                if (File.Exists(filePath) && !installationConfig.OverwriteExisting && !configFile.OverwriteExisting)
                    continue;

                // Create directory if needed
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Replace variables in template
                var content = configFile.TemplateContent;
                var variables = configFile.Variables;
                
                // Add project-specific variables
                variables["project_name"] = installationConfig.TargetProject.Name;
                variables["project_path"] = installationConfig.TargetProject.Path;
                variables["project_domain"] = installationConfig.TargetProject.Domain ?? installationConfig.TargetProject.Name.ToLower() + ".local";
                
                foreach (var variable in variables)
                {
                    content = content.Replace($"{{{variable.Key}}}", variable.Value);
                }

                await File.WriteAllTextAsync(filePath, content);
            }

            progressReporter?.UpdateProgress("Configuration files created", 90);
        }

        private async Task CreateFrameworkVirtualHostAsync(
            ProjectPackageInstallationConfig installationConfig,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Creating virtual host...", 95);

            // For now, use a default service ID (Apache)
            // TODO: Implement proper web server discovery
            const long defaultWebServerId = 1; // Assuming Apache has ID 1

            // Determine server name and document root
            string serverName;
            string documentRoot;

            if (!string.IsNullOrEmpty(installationConfig.CustomDomain))
            {
                serverName = installationConfig.CustomDomain;
                documentRoot = Path.Combine(installationConfig.TargetProject.Path, installationConfig.InstallationPath);
            }
            else if (installationConfig.CreateSubdomain && !string.IsNullOrEmpty(installationConfig.Subdomain))
            {
                var baseDomain = installationConfig.TargetProject.Domain ?? $"{installationConfig.TargetProject.Name.ToLower()}.local";
                serverName = $"{installationConfig.Subdomain}.{baseDomain}";
                documentRoot = Path.Combine(installationConfig.TargetProject.Path, installationConfig.InstallationPath);
            }
            else
            {
                // Install in a subdirectory
                serverName = installationConfig.TargetProject.Domain ?? $"{installationConfig.TargetProject.Name.ToLower()}.local";
                documentRoot = Path.Combine(installationConfig.TargetProject.Path, installationConfig.InstallationPath);
            }

            var request = new CreateVirtualHostRequest
            {
                ProjectId = installationConfig.TargetProject.Id,
                ServiceId = defaultWebServerId,
                ServerName = serverName,
                DocumentRoot = documentRoot,
                Port = installationConfig.CustomPort > 0 ? installationConfig.CustomPort : 80,
                IsSSL = installationConfig.EnableSSL,
                IsEnabled = true
            };

            await _virtualHostManager.CreateVirtualHostAsync(request);
            progressReporter?.UpdateProgress("Virtual host created", 98);
        }

        private static Task SetFrameworkEnvironmentVariablesAsync(
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

        private static async Task RunFrameworkPostInstallCommandsAsync(
            string installPath,
            List<string> postInstallCommands,
            IProgressReporter? progressReporter)
        {
            if (postInstallCommands.Count == 0)
                return;

            progressReporter?.UpdateProgress("Running post-installation commands...", 97);

            foreach (var command in postInstallCommands)
            {
                await ExecuteCommandAsync(command, installPath);
            }
        }

        private async Task UpdateProjectConfigurationAsync(
            Project project,
            AvailablePackage package,
            PackageVersion version,
            ProjectPackageInstallationConfig installationConfig,
            IProgressReporter? progressReporter)
        {
            progressReporter?.UpdateProgress("Updating project configuration...", 99);

            // Add installed framework to project configuration
            var installedFrameworks = project.Configuration.GetValueOrDefault("installed_frameworks", new List<object>()) as List<object> ?? [];
            
            var frameworkInfo = new
            {
                name = package.Name,
                version = version.Version,
                installPath = installationConfig.InstallationPath,
                installedAt = DateTime.UtcNow
            };

            installedFrameworks.Add(frameworkInfo);
            project.Configuration["installed_frameworks"] = installedFrameworks;

            await _projectManager.UpdateProjectAsync(project);
        }

        private static async Task ExecuteCommandAsync(string command, string workingDirectory)
        {
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
