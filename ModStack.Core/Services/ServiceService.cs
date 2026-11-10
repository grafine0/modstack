using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ModStack.Core.Services;

/// <summary>
/// Centralized service management service
/// </summary>
public partial class ServiceService(IServiceRepository serviceRepository) : IServiceService
{
    private readonly IServiceRepository _serviceRepository = serviceRepository;

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+\.\d+(?:\.\d+)?)")]
    private static partial System.Text.RegularExpressions.Regex VersionPatternRegex();

    public async Task<IEnumerable<ServiceDisplayItem>> GetAllServiceDisplayItemsAsync()
    {
        var services = await _serviceRepository.GetEnabledAsync();
        var result = new List<ServiceDisplayItem>();

        foreach (var service in services)
        {
            var serviceDisplayItem = await ConvertToServiceDisplayItemAsync(service);
            result.Add(serviceDisplayItem);
        }

        return result;
    }

    public async Task<IEnumerable<ServiceDisplayItem>> GetInstalledServicesAsync()
    {
        var services = await _serviceRepository.GetInstalledAsync();
        var result = new List<ServiceDisplayItem>();

        foreach (var service in services)
        {
            var serviceDisplayItem = await ConvertToServiceDisplayItemAsync(service);
            result.Add(serviceDisplayItem);
        }

        return result;
    }

    public async Task<ServiceDisplayItem?> GetServiceByIdAsync(int id)
    {
        var service = await _serviceRepository.GetByIdAsync(id);
        return service != null ? await ConvertToServiceDisplayItemAsync(service) : null;
    }

    public async Task<ServiceDisplayItem?> GetServiceByNameAsync(string name)
    {
        var service = await _serviceRepository.GetByNameAsync(name);
        return service != null ? await ConvertToServiceDisplayItemAsync(service) : null;
    }

    public async Task<IEnumerable<ServiceDefinition>> GetAllServicesAsync()
    {
        return await _serviceRepository.GetAllAsync();
    }

    public async Task<IEnumerable<ServiceDefinition>> GetServicesByTypeAsync(ServiceType serviceType)
    {
        var allServices = await _serviceRepository.GetAllAsync();
        return allServices.Where(s => s.ServiceType == serviceType);
    }

    public async Task<ServiceDefinition?> GetServiceByIdAsync(long id)
    {
        return await _serviceRepository.GetByIdAsync(id);
    }

    public async Task DetectAndUpdateInstallationStatusAsync()
    {
        var services = await _serviceRepository.GetEnabledAsync();
        
        foreach (var service in services)
        {
            await DetectAndUpdateInstallationStatusAsync(service.Id);
        }
    }

    public async Task DetectAndUpdateInstallationStatusAsync(long serviceId)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service == null) return;

        Console.WriteLine($"🔍 DETECTION: Checking service '{service.Name}' (ID: {serviceId}) at path '{service.BinaryPath}'");
        
        var isInstalled = await IsServiceInstalledAsync(service.Name, service.BinaryPath);
        string? version = null;
        string? bunaryPath = service.BinaryPath;

        Console.WriteLine($"📊 DETECTION RESULT: Service '{service.Name}' - IsInstalled: {isInstalled}");

        if (isInstalled)
        {
            version = await GetInstalledServiceVersionAsync(service.Name, service.BinaryPath);
            Console.WriteLine($"📋 VERSION DETECTED: Service '{service.Name}' - Version: {version}");
        }

        await _serviceRepository.UpdateInstallationStatusAsync(serviceId, isInstalled, bunaryPath, version);
        Console.WriteLine($"💾 DATABASE UPDATED: Service '{service.Name}' - IsInstalled: {isInstalled}, Version: {version}");
    }

    public async Task DetectAndUpdateServiceStatusAsync()
    {
        var installedServices = await _serviceRepository.GetInstalledAsync();
        
        foreach (var service in installedServices)
        {
            await DetectAndUpdateServiceStatusAsync(service.Id);
        }
    }

    public async Task DetectAndUpdateServiceStatusAsync(long serviceId)
    {
        var service = await _serviceRepository.GetByIdAsync(serviceId);
        if (service == null || !service.IsInstalled) return;

        var isRunning = await IsServiceRunningAsync(service.DefaultPort ?? 0);
        var status = isRunning ? ServiceStatus.Running : ServiceStatus.Stopped;

        await _serviceRepository.UpdateStatusAsync(serviceId, status);
    }

    public async Task<ServiceDisplayItem> AddServiceAsync(ServiceDefinition serviceDefinition)
    {
        var addedService = await _serviceRepository.AddAsync(serviceDefinition);
        return await ConvertToServiceDisplayItemAsync(addedService);
    }

    public async Task UpdateServiceConfigurationAsync(long serviceId, string configurationJson)
    {
        await _serviceRepository.UpdateConfigurationAsync(serviceId, configurationJson);
    }


    public async Task<bool> IsServiceInstalledAsync(string serviceName, string servicePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(servicePath))
                    return false;

                var serviceNameLower = serviceName.ToLower();
                return serviceNameLower switch
                {
                    "apache" => CheckApacheInstallation(servicePath),
                    var name when name.StartsWith("apache-") => CheckApacheInstallation(servicePath), // Handle apache-2.4.65, etc.
                    "nginx" => File.Exists(Path.Combine(servicePath, "nginx.exe")) ||
                              File.Exists(Path.Combine(servicePath, "sbin", "nginx.exe")) ||
                              Directory.GetDirectories(servicePath, "nginx-*").Any(d => 
                                  File.Exists(Path.Combine(d, "nginx.exe")) ||
                                  File.Exists(Path.Combine(d, "sbin", "nginx.exe"))),
                    "mysql" => File.Exists(Path.Combine(servicePath, "bin", "mysqld.exe")) ||
                              Directory.GetDirectories(servicePath, "mysql-*").Any(d => File.Exists(Path.Combine(d, "bin", "mysqld.exe"))),
                    "php" => File.Exists(Path.Combine(servicePath, "php.exe")) ||
                            Directory.GetDirectories(servicePath, "php-*").Any(d => File.Exists(Path.Combine(d, "php.exe"))),
                    "phpmyadmin" => File.Exists(Path.Combine(servicePath, "index.php")) ||
                                   Directory.GetDirectories(servicePath, "phpMyAdmin-*").Any(d => File.Exists(Path.Combine(d, "index.php"))) ||
                                   Directory.GetDirectories(servicePath, "phpmyadmin-*").Any(d => File.Exists(Path.Combine(d, "index.php"))),
                    "postgresql" => File.Exists(Path.Combine(servicePath, "bin", "postgres.exe")) ||
                                   Directory.GetDirectories(servicePath, "pgsql*").Any(d => File.Exists(Path.Combine(d, "bin", "postgres.exe"))) ||
                                   Directory.GetDirectories(servicePath, "postgresql-*").Any(d => File.Exists(Path.Combine(d, "bin", "postgres.exe"))),
                    "redis" => File.Exists(Path.Combine(servicePath, "redis-server.exe")) ||
                              File.Exists(Path.Combine(servicePath, "redis.exe")) ||
                              Directory.GetDirectories(servicePath, "redis-*").Any(d => 
                                  File.Exists(Path.Combine(d, "redis-server.exe")) ||
                                  File.Exists(Path.Combine(d, "redis.exe"))),
                    _ => Directory.GetFiles(servicePath, "*.exe", SearchOption.AllDirectories).Length > 0
                };
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<string?> GetInstalledServiceVersionAsync(string serviceName, string servicePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var subDirs = Directory.GetDirectories(servicePath);
                foreach (var dir in subDirs)
                {
                    var dirName = Path.GetFileName(dir);
                    // Extract base service name (apache-2.4.65 -> apache, php-8.3.0 -> php)
                    var baseServiceName = serviceName.Split('-')[0].Split(' ')[0];
                    if (dirName.Contains(baseServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = dirName.Split('-');
                        if (parts.Length > 1 && char.IsDigit(parts[1][0]))
                        {
                            return parts[1];
                        }
                        
                        var match = VersionPatternRegex().Match(dirName);
                        if (match.Success)
                        {
                            return match.Groups[1].Value;
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        });
    }

    public async Task<bool> IsServiceRunningAsync(int port)
    {
        return await Task.Run(async () =>
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("localhost", port);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    private static Task<ServiceDisplayItem> ConvertToServiceDisplayItemAsync(ServiceDefinition service)
    {
        return Task.Run(() => new ServiceDisplayItem
        {
            Id = service.Id,
            Name = service.DisplayName,
            Version = service.Version,
            Description = service.Description ?? string.Empty,
            Type = service.ServiceType,
            Status = service.Status.ToString(),
            Port = service.DefaultPort ?? 0,
            Path = service.BinaryPath,
            AutoStart = service.AutoStart
        });
    }

    private static bool CheckApacheInstallation(string apachePath)
    {
        try
        {
            var possiblePaths = new[]
            {
                Path.Combine(apachePath, "bin", "httpd.exe"),
                Path.Combine(apachePath, "httpd.exe"),
                Path.Combine(apachePath, "Apache24", "bin", "httpd.exe"),
                Path.Combine(apachePath, "Apache2.4", "bin", "httpd.exe")
            };

            return possiblePaths.Any(File.Exists) || 
                   Directory.GetDirectories(apachePath, "Apache*").Any(dir => 
                       File.Exists(Path.Combine(dir, "bin", "httpd.exe")));
        }
        catch
        {
            return false;
        }
    }
}
