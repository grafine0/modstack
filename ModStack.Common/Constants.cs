using System.Linq;
using System.Reflection;

namespace ModStack.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    public const string ApplicationName = "ModStack";
    public const string ApplicationDisplayName = "ModStack - Modular Web Development Stack Manager";
    public const string ConfigFileName = "modstack.config.json";
    public const string DatabaseFileName = "modstack.db";
    
    /// <summary>
    /// Gets the application version from the entry assembly (the main executable)
    /// </summary>
    public static string ApplicationVersion
    {
        get
        {
            // Get the entry assembly (the main executable, e.g., ModStack.GUI.exe)
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            
            // Try to get AssemblyInformationalVersionAttribute first
            var infoVersionAttr = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as AssemblyInformationalVersionAttribute;
            
            if (infoVersionAttr != null && !string.IsNullOrEmpty(infoVersionAttr.InformationalVersion))
            {
                return infoVersionAttr.InformationalVersion;
            }
            
            // Fall back to Assembly version
            var version = assembly.GetName().Version;
            if (version != null)
            {
                return version.ToString(3); // Major.Minor.Build format
            }
            
            // Fall back to default
            return "1.0.0";
        }
    }
    
    /// <summary>
    /// Default ports for common services
    /// </summary>
    public static class DefaultPorts
    {
        public const int Apache = 80;
        public const int ApacheSSL = 443;
        public const int Nginx = 80;
        public const int NginxSSL = 443;
        public const int MySQL = 3306;
        public const int MariaDB = 3306;
        public const int PostgreSQL = 5432;
        public const int MongoDB = 27017;
        public const int Redis = 6379;
        public const int Memcached = 11211;
        public const int Elasticsearch = 9200;
        public const int NodeJS = 3000;
    }
    
    /// <summary>
    /// Directory names
    /// </summary>
    public static class Directories
    {
        public const string Data = "Data";
        public const string Services = "Services";
        public const string Logs = "Logs";
        public const string Config = "Config";
        public const string Projects = "Projects";
        public const string Temp = "Temp";
        public const string Backups = "Backups";
    }
    
    /// <summary>
    /// Service names for Windows Services
    /// </summary>
    public static class ServiceNames
    {
        public const string Apache = "ModStack_Apache";
        public const string Nginx = "ModStack_Nginx";
        public const string MySQL = "ModStack_MySQL";
        public const string MariaDB = "ModStack_MariaDB";
        public const string PostgreSQL = "ModStack_PostgreSQL";
        public const string MongoDB = "ModStack_MongoDB";
        public const string Redis = "ModStack_Redis";
        public const string PHP = "ModStack_PHP";
    }
    
    /// <summary>
    /// Configuration keys
    /// </summary>
    public static class ConfigKeys
    {
        public const string DataDirectory = "Application.DataDirectory";
        public const string ServicesDirectory = "Application.ServicesDirectory";
        public const string LogsDirectory = "Application.LogsDirectory";
        public const string ProjectsDirectory = "Application.ProjectsDirectory";
        public const string AutoStartServices = "Application.AutoStartServices";
        public const string DefaultHttpPort = "Network.DefaultHttpPort";
        public const string DefaultHttpsPort = "Network.DefaultHttpsPort";
        public const string PortRangeStart = "Network.PortRangeStart";
        public const string PortRangeEnd = "Network.PortRangeEnd";
    }
    
    /// <summary>
    /// Default directory paths (cross-platform)
    /// </summary>
    public static class DefaultPaths
    {
        /// <summary>
        /// Gets the default application root directory
        /// </summary>
        public static string ApplicationRoot => 
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
            
        /// <summary>
        /// Gets the default ModStack root directory
        /// </summary>
        public static string ModStackRoot => @"C:\ModStack";
        
        /// <summary>
        /// Gets the default projects directory
        /// </summary>
        public static string Projects => Path.Combine(ModStackRoot, Directories.Projects);
        
        /// <summary>
        /// Gets the default services directory
        /// </summary>
        public static string Services => Path.Combine(ModStackRoot, Directories.Services);
        
        /// <summary>
        /// Gets the default logs directory
        /// </summary>
        public static string Logs => Path.Combine(ModStackRoot, Directories.Logs);
        
        /// <summary>
        /// Gets the default config directory
        /// </summary>
        public static string Config => Path.Combine(ModStackRoot, Directories.Config);
    }
}
