using ModStack.Common;
using ModStack.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ModStack.Core.Services;

/// <summary>
/// Service for managing application path configurations with cross-platform support
/// </summary>
public class PathConfigurationService : IPathConfigurationService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<PathConfigurationService> _logger;
    
    public PathConfigurationService(ISettingsService settingsService, ILogger<PathConfigurationService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }
    
    /// <summary>
    /// Gets the configured root directory path for ModStack
    /// </summary>
    public string GetRootDirectory()
    {
        var configuredPath = _settingsService.RootDirectory;
        _logger.LogInformation("🔍 GetRootDirectory: configuredPath from settings = '{ConfiguredPath}'", configuredPath);
        
        if (string.IsNullOrEmpty(configuredPath))
        {
            // Try the user-friendly default first (C:\ModStack)
            var preferredPath = Constants.DefaultPaths.ModStackRoot;
            _logger.LogInformation("🔍 GetRootDirectory: preferredPath = '{PreferredPath}'", preferredPath);
            
            // Check if we can access the preferred path
            if (CanAccessDirectory(preferredPath))
            {
                _logger.LogInformation("✅ GetRootDirectory: Can access preferred path '{PreferredPath}'", preferredPath);
                configuredPath = preferredPath;
            }
            else
            {
                // Fall back to AppData\Roaming if C:\ModStack is not accessible
                var fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModStack");
                _logger.LogWarning("⚠️ Cannot access preferred directory {PreferredPath}, falling back to {FallbackPath}", preferredPath, fallbackPath);
                configuredPath = fallbackPath;
            }
        }
        else
        {
            _logger.LogInformation("🔍 GetRootDirectory: Using configured path '{ConfiguredPath}'", configuredPath);
            
            // Check if the configured path is still accessible
            if (!CanAccessDirectory(configuredPath))
            {
                _logger.LogWarning("⚠️ Configured path '{ConfiguredPath}' is no longer accessible, falling back to AppData", configuredPath);
                var fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModStack");
                configuredPath = fallbackPath;
                
                // Update the settings to reflect the fallback
                try
                {
                    _settingsService.RootDirectory = fallbackPath;
                    _settingsService.SaveSettings();
                    _logger.LogInformation("✅ Updated settings to use fallback path: {FallbackPath}", fallbackPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError("❌ Failed to update settings: {ErrorMessage}", ex.Message);
                }
            }
        }
        
        EnsureDirectoryExists(configuredPath);
        _logger.LogInformation("🔍 GetRootDirectory: Final path = '{FinalPath}'", configuredPath);
        return configuredPath;
    }
    
    /// <summary>
    /// Gets the configured projects directory path
    /// </summary>
    public string GetProjectsDirectory()
    {
        var configuredPath = _settingsService.ProjectsDirectory;
        if (string.IsNullOrEmpty(configuredPath))
        {
            // If no specific projects directory is configured, use Projects subdirectory in root
            var rootDir = GetRootDirectory();
            configuredPath = Path.Combine(rootDir, Constants.Directories.Projects);
        }
        
        EnsureDirectoryExists(configuredPath);
        return configuredPath;
    }
    
    /// <summary>
    /// Gets the configured services directory path
    /// </summary>
    public string GetServicesDirectory()
    {
        // Always use the RootDirectory setting as the primary source
        var rootDir = GetRootDirectory();
        var rootBasedServicesPath = Path.Combine(rootDir, Constants.Directories.Services);
        
        // Check if there's a legacy ServicesDirectory setting that's different from the root-based path
        var legacyServicesPath = _settingsService.ServicesDirectory;
        var legacyInstallPath = _settingsService.InstallPath;
        
        // If legacy settings point to the same location as root-based path, use root-based path
        if (!string.IsNullOrEmpty(legacyServicesPath) && 
            string.Equals(legacyServicesPath, rootBasedServicesPath, StringComparison.OrdinalIgnoreCase))
        {
            return rootBasedServicesPath;
        }
        
        if (!string.IsNullOrEmpty(legacyInstallPath) && 
            string.Equals(legacyInstallPath, rootBasedServicesPath, StringComparison.OrdinalIgnoreCase))
        {
            return rootBasedServicesPath;
        }
        
        // If legacy settings point to AppData\Roaming\ModStack (old default), use root-based path instead
        var oldDefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ModStack", "Services");
        if ((!string.IsNullOrEmpty(legacyServicesPath) && string.Equals(legacyServicesPath, oldDefaultPath, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(legacyInstallPath) && string.Equals(legacyInstallPath, oldDefaultPath, StringComparison.OrdinalIgnoreCase)))
        {
            return rootBasedServicesPath;
        }
        
        // If legacy settings point to a completely different location, warn and use root-based path
        if (!string.IsNullOrEmpty(legacyServicesPath) || !string.IsNullOrEmpty(legacyInstallPath))
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Legacy services directory setting detected but using RootDirectory-based path instead: {rootBasedServicesPath}");
        }
        
        EnsureDirectoryExists(rootBasedServicesPath);
        return rootBasedServicesPath;
    }
    
    /// <summary>
    /// Gets the configured logs directory path
    /// </summary>
    public string GetLogsDirectory()
    {
        var configuredPath = _settingsService.LogsDirectory;
        if (string.IsNullOrEmpty(configuredPath))
        {
            // If no specific logs directory is configured, use Logs subdirectory in root
            var rootDir = GetRootDirectory();
            configuredPath = Path.Combine(rootDir, Constants.Directories.Logs);
        }
        
        EnsureDirectoryExists(configuredPath);
        return configuredPath;
    }
    
    /// <summary>
    /// Gets the configured config directory path
    /// </summary>
    public string GetConfigDirectory()
    {
        // Config directory is always relative to the root directory
        var rootDir = GetRootDirectory();
        var configPath = Path.Combine(rootDir, Constants.Directories.Config);
        EnsureDirectoryExists(configPath);
        return configPath;
    }
    
    /// <summary>
    /// Gets the path to a specific service installation
    /// </summary>
    public string GetServicePath(string serviceName, string? version = null)
    {
        var servicesRoot = GetServicesDirectory();
        var serviceDir = Path.Combine(servicesRoot, serviceName);
        
        if (!string.IsNullOrEmpty(version))
        {
            // Try versioned directory first (e.g., "php-8.3.0")
            var versionedDir = Path.Combine(servicesRoot, serviceName, $"{serviceName.ToLower()}-{version}");
            if (Directory.Exists(versionedDir))
            {
                return versionedDir;
            }
            
            // Try direct version directory (e.g., "PHP/php-8.3.0")
            versionedDir = Path.Combine(serviceDir, $"{serviceName.ToLower()}-{version}");
            if (Directory.Exists(versionedDir))
            {
                return versionedDir;
            }
        }
        
        // Fall back to service directory
        if (Directory.Exists(serviceDir))
        {
            return serviceDir;
        }
        
        // Return expected path even if it doesn't exist
        return serviceDir;
    }
    
    /// <summary>
    /// Gets common service installation paths for discovery (cross-platform)
    /// </summary>
    public IEnumerable<string> GetServiceDiscoveryPaths(string serviceName)
    {
        var paths = new List<string>();
        
        // Add configured ModStack services path
        var servicesPath = GetServicesDirectory();
        paths.Add(Path.Combine(servicesPath, serviceName));
        
        // Add platform-specific common paths
        if (OperatingSystem.IsWindows())
        {
            paths.AddRange(GetWindowsServicePaths(serviceName));
        }
        else if (OperatingSystem.IsLinux())
        {
            paths.AddRange(GetLinuxServicePaths(serviceName));
        }
        else if (OperatingSystem.IsMacOS())
        {
            paths.AddRange(GetMacOSServicePaths(serviceName));
        }
        
        return paths.Where(Directory.Exists);
    }
    
    /// <summary>
    /// Generates a default project path for a given project name
    /// </summary>
    public string GenerateProjectPath(string projectName)
    {
        var projectsDir = GetProjectsDirectory();
        var cleanName = SanitizePathComponent(projectName);
        return Path.Combine(projectsDir, cleanName);
    }
    
    /// <summary>
    /// Checks if a path is within the managed ModStack directories
    /// </summary>
    public bool IsModStackManagedPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var modStackRoot = Path.GetFullPath(Constants.DefaultPaths.ModStackRoot);
            
            return fullPath.StartsWith(modStackRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
    
    private void EnsureDirectoryExists(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogDebug("Created directory: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create directory: {Path}", path);
        }
    }
    
    private static string SanitizePathComponent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "project";
            
        // Remove or replace invalid path characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = input;
        
        foreach (var ch in invalidChars)
        {
            sanitized = sanitized.Replace(ch, '-');
        }
        
        // Replace spaces with hyphens and convert to lowercase
        sanitized = sanitized.Replace(' ', '-').ToLowerInvariant();
        
        // Remove multiple consecutive hyphens
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }
        
        // Remove leading/trailing hyphens
        sanitized = sanitized.Trim('-');
        
        return string.IsNullOrEmpty(sanitized) ? "project" : sanitized;
    }
    
    private static IEnumerable<string> GetWindowsServicePaths(string serviceName)
    {
        return serviceName.ToLowerInvariant() switch
        {
            "apache" => new[]
            {
                @"C:\Apache24",
                @"C:\Apache",
                @"C:\Program Files\Apache Software Foundation\Apache2.4",
                @"C:\Program Files (x86)\Apache Software Foundation\Apache2.4"
            },
            "php" => new[]
            {
                @"C:\PHP",
                @"C:\Program Files\PHP",
                @"C:\Program Files (x86)\PHP"
            },
            "mysql" => new[]
            {
                @"C:\Program Files\MySQL\MySQL Server 8.0",
                @"C:\Program Files\MySQL\MySQL Server 5.7",
                @"C:\Program Files (x86)\MySQL\MySQL Server 8.0"
            },
            "nginx" => new[]
            {
                @"C:\nginx",
                @"C:\Program Files\nginx",
                @"C:\Program Files (x86)\nginx"
            },
            _ => Array.Empty<string>()
        };
    }
    
    private static IEnumerable<string> GetLinuxServicePaths(string serviceName)
    {
        return serviceName.ToLowerInvariant() switch
        {
            "apache" => new[]
            {
                "/usr/bin/apache2",
                "/usr/bin/httpd",
                "/usr/local/apache2",
                "/opt/apache2"
            },
            "php" => new[]
            {
                "/usr/bin/php",
                "/usr/local/bin/php",
                "/opt/php"
            },
            "mysql" => new[]
            {
                "/usr/bin/mysql",
                "/usr/local/mysql",
                "/opt/mysql"
            },
            "nginx" => new[]
            {
                "/usr/bin/nginx",
                "/usr/local/nginx",
                "/opt/nginx"
            },
            _ => Array.Empty<string>()
        };
    }
    
    private static IEnumerable<string> GetMacOSServicePaths(string serviceName)
    {
        return serviceName.ToLowerInvariant() switch
        {
            "apache" => new[]
            {
                "/usr/local/apache2",
                "/opt/homebrew/bin/httpd",
                "/usr/local/bin/httpd"
            },
            "php" => new[]
            {
                "/usr/local/bin/php",
                "/opt/homebrew/bin/php",
                "/usr/bin/php"
            },
            "mysql" => new[]
            {
                "/usr/local/mysql",
                "/opt/homebrew/bin/mysql",
                "/usr/local/bin/mysql"
            },
            "nginx" => new[]
            {
                "/usr/local/nginx",
                "/opt/homebrew/bin/nginx",
                "/usr/local/bin/nginx"
            },
            _ => Array.Empty<string>()
        };
    }
    
    /// <summary>
    /// Checks if a directory can be accessed (created and written to)
    /// </summary>
    private bool CanAccessDirectory(string path)
    {
        try
        {
            _logger.LogInformation("🔍 CanAccessDirectory: Testing access to '{Path}'", path);
            
            // Try to create the directory if it doesn't exist
            if (!Directory.Exists(path))
            {
                _logger.LogInformation("🔍 CanAccessDirectory: Directory doesn't exist, creating '{Path}'", path);
                Directory.CreateDirectory(path);
                _logger.LogInformation("✅ CanAccessDirectory: Directory created successfully");
            }
            else
            {
                _logger.LogInformation("🔍 CanAccessDirectory: Directory already exists");
            }
            
            // Try to write a test file
            var testFile = Path.Combine(path, ".modstack_test");
            _logger.LogInformation("🔍 CanAccessDirectory: Writing test file '{TestFile}'", testFile);
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            _logger.LogInformation("✅ CanAccessDirectory: Test file write/delete successful");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ CanAccessDirectory: Failed to access '{Path}' - {ErrorMessage}", path, ex.Message);
            return false;
        }
    }
}
