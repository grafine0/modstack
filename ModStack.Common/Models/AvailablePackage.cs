using System;
using System.Collections.Generic;
using ModStack.Common.Enums;

namespace ModStack.Common.Models
{
    /// <summary>
    /// Represents a package available for download and installation
    /// </summary>
    public class AvailablePackage
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // Web Server, Database, Runtime, etc.
        public string Icon { get; set; } = string.Empty; // Icon path or emoji
        public bool IsPopular { get; set; }
        public long DownloadSize { get; set; } // Size in bytes
        public List<PackageVersion> AvailableVersions { get; set; } = new();
        public PackageConfiguration DefaultConfiguration { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string Homepage { get; set; } = string.Empty;
        public string Documentation { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public bool IsInstalled { get; set; }
        public string? InstalledVersion { get; set; }
        public List<string> InstalledVersions { get; set; } = new();
        
        /// <summary>
        /// Type of package (Service, Project, Tool)
        /// </summary>
        public PackageType PackageType { get; set; } = PackageType.Service;
        
        /// <summary>
        /// Project-specific configuration (only for Project type packages)
        /// </summary>
        public ProjectPackageConfiguration? ProjectConfiguration { get; set; }
        
        /// <summary>
        /// Indicates if this is a system package (shipped with application)
        /// System packages can only have their basic info viewed, not edited or deleted
        /// </summary>
        public bool IsSystemPackage { get; set; } = false;
    }

    /// <summary>
    /// Represents a specific version of a package
    /// </summary>
    public class PackageVersion
    {
        public long Id { get; set; }
        public long PackageId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public bool IsStable { get; set; } = true;
        public bool IsLTS { get; set; }
        public bool IsRecommended { get; set; }
        public List<string> DownloadUrls { get; set; } = new();
        public string Checksum { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ReleaseNotes { get; set; } = string.Empty;
        public List<string> Requirements { get; set; } = new();
        
        /// <summary>
        /// Indicates if this is a system version (shipped with application)
        /// System versions cannot be edited or deleted by users
        /// </summary>
        public bool IsSystemVersion { get; set; } = false;
    }

    /// <summary>
    /// Configuration options for a package
    /// </summary>
    public class PackageConfiguration
    {
        public int Port { get; set; }
        public string InstallPath { get; set; } = string.Empty;
        public bool AutoStart { get; set; }
        public bool RegisterAsService { get; set; } = true;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public List<ConfigOption> AvailableOptions { get; set; } = new();
    }

    /// <summary>
    /// Individual configuration option
    /// </summary>
    public class ConfigOption
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "text", "number", "boolean", "select"
        public object DefaultValue { get; set; } = new();
        public List<string> Options { get; set; } = new(); // For select type
        public bool IsRequired { get; set; }
    }
}
