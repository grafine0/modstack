using System;
using System.IO;
using ModStack.Common.Models;
using ModStack.Common;

namespace ModStack.Core.Extensions;

/// <summary>
/// Extension methods for ServiceType to encapsulate service-specific logic
/// </summary>
public static class ServiceTypeExtensions
{
    /// <summary>
    /// Gets the correct executable path for different service types
    /// </summary>
    public static string? GetExecutablePath(this ServiceType serviceType, string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return null;

        return serviceType switch
        {
            ServiceType.PHP => GetPhpExecutablePath(basePath),
            ServiceType.Apache => GetApacheExecutablePath(basePath),
            ServiceType.MySQL => GetMySqlExecutablePath(basePath),
            ServiceType.Nginx => GetNginxExecutablePath(basePath),
            ServiceType.PostgreSQL => GetPostgreSqlExecutablePath(basePath),
            _ => File.Exists(basePath) ? basePath : null
        };
    }

    /// <summary>
    /// Gets service-specific startup arguments
    /// </summary>
    public static string? GetStartArguments(this ServiceType serviceType, ServiceDisplayItem service)
    {
        return serviceType switch
        {
            ServiceType.PHP => $"-b 127.0.0.1:{service.Port}",
            ServiceType.Apache => GetApacheStartArguments(service.Path),
            ServiceType.MySQL => GetMySqlStartArguments(service.Path),
            ServiceType.PostgreSQL => GetPostgreSqlStartArguments(service),
            _ => null
        };
    }

    /// <summary>
    /// Gets the process name to look for when checking service status
    /// </summary>
    public static string GetProcessName(this ServiceType serviceType, string serviceName)
    {
        return serviceType switch
        {
            ServiceType.PHP => "php-cgi",
            ServiceType.Apache => "httpd",
            ServiceType.MySQL => "mysqld",
            ServiceType.Nginx => "nginx",
            ServiceType.PostgreSQL => "postgres",
            _ => GetProcessNameFromServiceName(serviceName)
        };
    }

    private static string? GetPhpExecutablePath(string basePath)
    {
        // For PHP, use php-cgi.exe for web services
        var phpCgiPath = Path.Combine(basePath, "php-cgi.exe");
        if (File.Exists(phpCgiPath))
            return phpCgiPath;

        // Fallback to php.exe
        var phpPath = Path.Combine(basePath, "php.exe");
        return File.Exists(phpPath) ? phpPath : null;
    }

    private static string? GetApacheExecutablePath(string basePath)
    {
        // For Apache, use httpd.exe
        var httpdBinPath = Path.Combine(basePath, "bin", "httpd.exe");
        if (File.Exists(httpdBinPath))
            return httpdBinPath;

        var httpdPath = Path.Combine(basePath, "httpd.exe");
        return File.Exists(httpdPath) ? httpdPath : null;
    }

    private static string? GetMySqlExecutablePath(string basePath)
    {
        // For MySQL, use mysqld.exe
        var mysqldBinPath = Path.Combine(basePath, "bin", "mysqld.exe");
        if (File.Exists(mysqldBinPath))
            return mysqldBinPath;

        var mysqldPath = Path.Combine(basePath, "mysqld.exe");
        return File.Exists(mysqldPath) ? mysqldPath : null;
    }

    private static string? GetNginxExecutablePath(string basePath)
    {
        // For Nginx, use nginx.exe
        var nginxPath = Path.Combine(basePath, "nginx.exe");
        return File.Exists(nginxPath) ? nginxPath : null;
    }

    private static string? GetApacheStartArguments(string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return null;

        var configPath = Path.Combine(basePath, "conf", "httpd.conf");
        return File.Exists(configPath) ? $"-f \"{configPath}\"" : null;
    }

    private static string? GetMySqlStartArguments(string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return null;

        var dataPath = Path.Combine(basePath, "data");
        return Directory.Exists(dataPath) ? $"--datadir=\"{dataPath}\"" : null;
    }

    private static string? GetPostgreSqlExecutablePath(string basePath)
    {
        // PostgreSQL uses pg_ctl.exe for service control or postgres.exe directly
        // Try bin directory first (standard layout from ZIP binaries)
        var pgCtlBinPath = Path.Combine(basePath, "bin", "pg_ctl.exe");
        if (File.Exists(pgCtlBinPath))
            return pgCtlBinPath;

        // Try pgsql subdirectory (common in some distributions)
        var pgsqlBinPath = Path.Combine(basePath, "pgsql", "bin", "pg_ctl.exe");
        if (File.Exists(pgsqlBinPath))
            return pgsqlBinPath;

        // Try postgres.exe directly as fallback
        var postgresBinPath = Path.Combine(basePath, "bin", "postgres.exe");
        if (File.Exists(postgresBinPath))
            return postgresBinPath;

        return null;
    }

    /// <summary>
    /// Gets the PostgreSQL base directory that contains bin, data, lib folders
    /// </summary>
    private static string GetPostgreSqlBaseDir(string servicePath)
    {
        // Check if pgsql subdirectory exists (EDB binaries structure)
        var pgsqlPath = Path.Combine(servicePath, "pgsql");
        if (Directory.Exists(pgsqlPath) && File.Exists(Path.Combine(pgsqlPath, "bin", "pg_ctl.exe")))
        {
            return pgsqlPath;
        }
        
        // Standard structure where bin is directly in the service path
        return servicePath;
    }

    private static string? GetPostgreSqlStartArguments(ServiceDisplayItem service)
    {
        if (string.IsNullOrEmpty(service.Path))
            return null;

        var baseDir = GetPostgreSqlBaseDir(service.Path);
        var dataPath = Path.Combine(baseDir, "data");
        var pgCtlPath = Path.Combine(baseDir, "bin", "pg_ctl.exe");
        
        if (File.Exists(pgCtlPath))
        {
            // Using pg_ctl: pg_ctl start -D "datadir" -l "logfile" -w
            var logPath = Path.Combine(baseDir, "logs", "postgresql.log");
            var logsDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logsDir) && !Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
            
            return $"start -D \"{dataPath}\" -l \"{logPath}\" -w -o \"-p {service.Port}\"";
        }
        
        // Fallback: using postgres.exe directly
        return $"-D \"{dataPath}\" -p {service.Port}";
    }

    /// <summary>
    /// Checks if PostgreSQL database needs initialization and runs initdb if needed
    /// </summary>
    public static async Task<bool> EnsurePostgreSqlInitializedAsync(string servicePath)
    {
        var baseDir = GetPostgreSqlBaseDir(servicePath);
        var dataPath = Path.Combine(baseDir, "data");
        var pgVersionFile = Path.Combine(dataPath, "PG_VERSION");
        
        // If PG_VERSION exists, database is already initialized
        if (File.Exists(pgVersionFile))
            return true;

        // Check pgsql subdirectory for data as well (EDB binaries may have data there)
        var pgsqlDataPath = Path.Combine(servicePath, "pgsql", "data");
        var pgsqlPgVersionFile = Path.Combine(pgsqlDataPath, "PG_VERSION");
        if (File.Exists(pgsqlPgVersionFile))
            return true;

        // Find initdb.exe
        var initDbPath = Path.Combine(baseDir, "bin", "initdb.exe");
        if (!File.Exists(initDbPath))
            return false;

        // Create data directory if it doesn't exist
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // Run initdb to initialize the database
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = initDbPath,
            Arguments = $"-D \"{dataPath}\" -U postgres -E UTF8 --locale=C",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(initDbPath)
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
            return false;

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static string GetProcessNameFromServiceName(string serviceName)
    {
        // For unknown services, try to extract from the service name
        var name = serviceName.ToLower();
        if (name.Contains("apache")) return "httpd";
        if (name.Contains("nginx")) return "nginx";
        if (name.Contains("mysql")) return "mysqld";
        if (name.Contains("php")) return "php-cgi";
        if (name.Contains("postgres")) return "postgres";
        return serviceName;
    }
}
