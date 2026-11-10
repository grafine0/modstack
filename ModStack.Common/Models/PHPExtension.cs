using ModStack.Common;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a PHP extension that can be installed
/// </summary>
public class PHPExtension
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
    public bool IsZendExtension { get; set; } = false;
    public string? DllName { get; set; }
    public List<string> SupportedPHPVersions { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public string? DownloadUrl { get; set; }
    public string? Hash { get; set; }
    public string? HashAlgorithm { get; set; } // SHA256, SHA1, MD5
    public string? ConfigurationTemplate { get; set; }
    public bool IsPopular { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents an installed PHP extension for a specific PHP version
/// </summary>
public class InstalledPHPExtension
{
    public long Id { get; set; }
    public long ServiceId { get; set; } // PHP service ID
    public long ExtensionId { get; set; }
    public string PHPVersion { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? Configuration { get; set; }
    public DateTime InstalledAt { get; set; }
    
    /// <summary>
    /// Navigation properties
    /// </summary>
    public ServiceDefinition? Service { get; set; }
    public PHPExtension? Extension { get; set; }
}

/// <summary>
/// Request model for installing a PHP extension
/// </summary>
public class InstallPHPExtensionRequest
{
    public long ServiceId { get; set; }
    public long ExtensionId { get; set; }
    public string PHPVersion { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public bool EnableAfterInstall { get; set; } = true;
}

/// <summary>
/// Categories for PHP extensions
/// </summary>
public static class PHPExtensionCategories
{
    public const string Database = "Database";
    public const string WebServices = "Web Services";
    public const string ImageProcessing = "Image Processing";
    public const string Compression = "Compression";
    public const string Encryption = "Encryption";
    public const string Development = "Development";
    public const string Performance = "Performance";
    public const string FileSystem = "File System";
    public const string Network = "Network";
    public const string Other = "Other";
}
