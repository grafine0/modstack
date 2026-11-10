using System.Collections.Generic;

namespace ModStack.Common.Models
{
    /// <summary>
    /// Configuration for installing packages within projects
    /// </summary>
    public class ProjectPackageInstallationConfig
    {
        /// <summary>
        /// The target project where the package will be installed
        /// </summary>
        public Project TargetProject { get; set; } = new();

        /// <summary>
        /// Installation path within the project (relative to project root)
        /// </summary>
        public string InstallationPath { get; set; } = string.Empty;

        /// <summary>
        /// Whether to create a subdomain for this installation
        /// </summary>
        public bool CreateSubdomain { get; set; } = false;

        /// <summary>
        /// Subdomain name (e.g., "admin" for admin.mysite.local)
        /// </summary>
        public string Subdomain { get; set; } = string.Empty;

        /// <summary>
        /// Whether to create a separate virtual host
        /// </summary>
        public bool CreateVirtualHost { get; set; } = true;

        /// <summary>
        /// Custom domain for the installation (overrides subdomain logic)
        /// </summary>
        public string? CustomDomain { get; set; }

        /// <summary>
        /// Custom port for the installation (0 = use default)
        /// </summary>
        public int CustomPort { get; set; } = 0;

        /// <summary>
        /// Whether to enable SSL for this installation
        /// </summary>
        public bool EnableSSL { get; set; } = false;

        /// <summary>
        /// Whether to setup database for this installation
        /// </summary>
        public bool SetupDatabase { get; set; } = true;

        /// <summary>
        /// Whether to auto-start after installation
        /// </summary>
        public bool AutoStart { get; set; } = true;

        /// <summary>
        /// Database configuration for this installation
        /// </summary>
        public DatabaseConfiguration? DatabaseConfig { get; set; }

        /// <summary>
        /// Configuration files to be created/updated
        /// </summary>
        public List<ConfigurationFile> ConfigurationFiles { get; set; } = new();

        /// <summary>
        /// Environment variables for this installation
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        /// <summary>
        /// Post-installation commands
        /// </summary>
        public List<string> PostInstallCommands { get; set; } = new();

        /// <summary>
        /// Whether to overwrite existing files
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;
    }

    /// <summary>
    /// Installation options for different package types
    /// </summary>
    public class PackageInstallationOptions
    {
        /// <summary>
        /// Available projects for installation
        /// </summary>
        public List<Project> AvailableProjects { get; set; } = new();

        /// <summary>
        /// Whether this package can be installed multiple times
        /// </summary>
        public bool AllowMultipleInstallations { get; set; } = false;

        /// <summary>
        /// Whether this package requires project selection
        /// </summary>
        public bool RequiresProjectSelection { get; set; } = false;

        /// <summary>
        /// Whether this package creates its own project
        /// </summary>
        public bool CreatesOwnProject { get; set; } = false;

        /// <summary>
        /// Default installation path within projects
        /// </summary>
        public string DefaultInstallationPath { get; set; } = string.Empty;

        /// <summary>
        /// Whether to show subdomain options
        /// </summary>
        public bool ShowSubdomainOptions { get; set; } = false;

        /// <summary>
        /// Whether to show virtual host options
        /// </summary>
        public bool ShowVirtualHostOptions { get; set; } = false;

        /// <summary>
        /// Whether to show database configuration options
        /// </summary>
        public bool ShowDatabaseOptions { get; set; } = false;
    }
}
