using System.Collections.Generic;

namespace ModStack.Common.Models
{
    /// <summary>
    /// Configuration specific to project-type packages
    /// </summary>
    public class ProjectPackageConfiguration
    {
        /// <summary>
        /// Default domain for the project (e.g., "phpmyadmin.local")
        /// </summary>
        public string DefaultDomain { get; set; } = string.Empty;

        /// <summary>
        /// Default port for the project (usually 80 or custom port)
        /// </summary>
        public int DefaultPort { get; set; } = 80;

        /// <summary>
        /// Whether to enable SSL by default
        /// </summary>
        public bool EnableSSL { get; set; } = false;

        /// <summary>
        /// Required database configuration for the project
        /// </summary>
        public DatabaseConfiguration? DatabaseConfig { get; set; }

        /// <summary>
        /// Required services that must be installed before this project
        /// </summary>
        public List<string> RequiredServices { get; set; } = new();

        /// <summary>
        /// Custom configuration files that need to be created/updated
        /// </summary>
        public List<ConfigurationFile> ConfigurationFiles { get; set; } = new();

        /// <summary>
        /// Environment variables to be set
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        /// <summary>
        /// Post-installation setup commands
        /// </summary>
        public List<string> PostInstallCommands { get; set; } = new();
    }

    /// <summary>
    /// Database configuration for project packages
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Database type (MySQL, PostgreSQL, SQLite)
        /// </summary>
        public string DatabaseType { get; set; } = "MySQL";

        /// <summary>
        /// Database name to create
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Database username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Database password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Database host (usually localhost)
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Database port
        /// </summary>
        public int Port { get; set; } = 3306;

        /// <summary>
        /// Whether to create the database automatically
        /// </summary>
        public bool CreateDatabase { get; set; } = true;

        /// <summary>
        /// Whether to create the user automatically
        /// </summary>
        public bool CreateUser { get; set; } = true;
    }

    /// <summary>
    /// Configuration file that needs to be created or updated
    /// </summary>
    public class ConfigurationFile
    {
        /// <summary>
        /// Relative path from project root
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// Template content for the configuration file
        /// </summary>
        public string TemplateContent { get; set; } = string.Empty;

        /// <summary>
        /// Variables to replace in the template
        /// </summary>
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>
        /// Whether to overwrite existing file
        /// </summary>
        public bool OverwriteExisting { get; set; } = false;
    }
}
