using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;
using ModStackLogLevel = ModStack.Common.LogLevel;

namespace ModStack.Services;

/// <summary>
/// Manages Windows services for ModStack
/// </summary>
public class WindowsServiceManager : IServiceManager
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IPortAllocationRepository _portRepository;
    private readonly IServiceLogRepository _logRepository;
    private readonly ILogger<WindowsServiceManager> _logger;

    public WindowsServiceManager(
        IServiceRepository serviceRepository,
        IPortAllocationRepository portRepository,
        IServiceLogRepository logRepository,
        ILogger<WindowsServiceManager> logger)
    {
        _serviceRepository = serviceRepository;
        _portRepository = portRepository;
        _logRepository = logRepository;
        _logger = logger;
    }

    public async Task<List<ServiceDefinition>> GetServicesAsync()
    {
        var services = await _serviceRepository.GetAllAsync();
        return services.ToList();
    }

    public async Task<ServiceDefinition?> GetServiceAsync(long id)
    {
        return await _serviceRepository.GetByIdAsync(id);
    }

    public async Task<ServiceDefinition?> GetServiceAsync(string name, string version)
    {
        return await _serviceRepository.GetByNameAndVersionAsync(name, version);
    }

    public async Task<bool> InstallServiceAsync(ServiceDefinition service)
    {
        try
        {
            _logger.LogInformation("Installing service {ServiceName} v{Version}", service.Name, service.Version);

            // Validate service binary exists
            var binaryPath = service.BinaryPath;
            if (!File.Exists(binaryPath))
            {
                _logger.LogError("Service binary not found: {BinaryPath}", binaryPath);
                return false;
            }

            // Check if service already exists
            var existingService = await GetServiceAsync(service.Name, service.Version);
            if (existingService?.IsInstalled == true)
            {
                _logger.LogWarning("Service {ServiceName} v{Version} is already installed", service.Name, service.Version);
                return false;
            }

            // Create Windows Service
            var serviceName = service.WindowsServiceName;
            var result = await CreateWindowsServiceAsync(serviceName, binaryPath, service.DisplayName);

            if (result)
            {
                service.IsInstalled = true;
                service.InstallDate = DateTime.UtcNow;

                if (existingService != null)
                {
                    await _serviceRepository.UpdateAsync(service);
                }
                else
                {
                    await _serviceRepository.CreateAsync(service);
                }

                await AddServiceLogAsync(service.Id, ModStackLogLevel.Info, $"Service installed successfully");
                _logger.LogInformation("Service {ServiceName} v{Version} installed successfully", service.Name, service.Version);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install service {ServiceName} v{Version}", service.Name, service.Version);
            await AddServiceLogAsync(service.Id, ModStackLogLevel.Error, "Failed to install service", ex.ToString());
            return false;
        }
    }

    public async Task<bool> UninstallServiceAsync(long serviceId)
    {
        try
        {
            var service = await GetServiceAsync(serviceId);
            if (service == null)
            {
                _logger.LogError("Service with ID {ServiceId} not found", serviceId);
                return false;
            }

            _logger.LogInformation("Uninstalling service {ServiceName} v{Version}", service.Name, service.Version);

            // Stop service if running
            await StopServiceAsync(serviceId);

            // Remove Windows Service
            var serviceName = service.WindowsServiceName;
            var result = await RemoveWindowsServiceAsync(serviceName);

            if (result)
            {
                service.IsInstalled = false;
                await _serviceRepository.UpdateAsync(service);

                // Release any allocated ports
                var ports = await _portRepository.GetByServiceIdAsync(serviceId);
                foreach (var port in ports)
                {
                    await _portRepository.DeleteAsync(port.Id);
                }

                await AddServiceLogAsync(serviceId, ModStackLogLevel.Info, "Service uninstalled successfully");
                _logger.LogInformation("Service {ServiceName} v{Version} uninstalled successfully", service.Name, service.Version);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall service with ID {ServiceId}", serviceId);
            await AddServiceLogAsync(serviceId, ModStackLogLevel.Error, "Failed to uninstall service", ex.ToString());
            return false;
        }
    }

    public async Task<bool> StartServiceAsync(long serviceId)
    {
        try
        {
            var service = await GetServiceAsync(serviceId);
            if (service == null || !service.IsInstalled)
            {
                _logger.LogError("Service with ID {ServiceId} not found or not installed", serviceId);
                return false;
            }

            _logger.LogInformation("Starting service {ServiceName} v{Version}", service.Name, service.Version);

            var serviceName = service.WindowsServiceName;
            using var serviceController = new ServiceController(serviceName);

            if (serviceController.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("Service {ServiceName} is already running", service.Name);
                return true;
            }

            serviceController.Start();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            await AddServiceLogAsync(serviceId, ModStackLogLevel.Info, "Service started successfully");
            _logger.LogInformation("Service {ServiceName} v{Version} started successfully", service.Name, service.Version);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service with ID {ServiceId}", serviceId);
            await AddServiceLogAsync(serviceId, ModStackLogLevel.Error, "Failed to start service", ex.ToString());
            return false;
        }
    }

    public async Task<bool> StopServiceAsync(long serviceId)
    {
        try
        {
            var service = await GetServiceAsync(serviceId);
            if (service == null || !service.IsInstalled)
            {
                _logger.LogError("Service with ID {ServiceId} not found or not installed", serviceId);
                return false;
            }

            _logger.LogInformation("Stopping service {ServiceName} v{Version}", service.Name, service.Version);

            var serviceName = service.WindowsServiceName;
            using var serviceController = new ServiceController(serviceName);

            if (serviceController.Status == ServiceControllerStatus.Stopped)
            {
                _logger.LogInformation("Service {ServiceName} is already stopped", service.Name);
                return true;
            }

            serviceController.Stop();
            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));

            await AddServiceLogAsync(serviceId, ModStackLogLevel.Info, "Service stopped successfully");
            _logger.LogInformation("Service {ServiceName} v{Version} stopped successfully", service.Name, service.Version);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service with ID {ServiceId}", serviceId);
            await AddServiceLogAsync(serviceId, ModStackLogLevel.Error, "Failed to stop service", ex.ToString());
            return false;
        }
    }

    public async Task<bool> RestartServiceAsync(long serviceId)
    {
        var stopResult = await StopServiceAsync(serviceId);
        if (!stopResult) return false;

        // Wait a moment for the service to fully stop
        await Task.Delay(2000);

        return await StartServiceAsync(serviceId);
    }

    public async Task<ServiceStatus> GetServiceStatusAsync(long serviceId)
    {
        try
        {
            var service = await GetServiceAsync(serviceId);
            if (service == null)
            {
                _logger.LogWarning("Service with ID {ServiceId} not found", serviceId);
                return ServiceStatus.Unknown;
            }

            _logger.LogDebug("Checking status for service {ServiceName} (ID: {ServiceId}) - IsInstalled: {IsInstalled}, IsEnabled: {IsEnabled}", 
                service.Name, serviceId, service.IsInstalled, service.IsEnabled);

            if (!service.IsInstalled)
            {
                _logger.LogDebug("Service {ServiceName} is not installed, returning Unknown status", service.Name);
                return ServiceStatus.Unknown;
            }

            var serviceName = service.WindowsServiceName;
            using var serviceController = new ServiceController(serviceName);
            
            var windowsStatus = serviceController.Status;
            var modStackStatus = windowsStatus switch
            {
                ServiceControllerStatus.Running => ServiceStatus.Running,
                ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                ServiceControllerStatus.StartPending => ServiceStatus.Starting,
                ServiceControllerStatus.StopPending => ServiceStatus.Stopping,
                _ => ServiceStatus.Unknown
            };

            _logger.LogDebug("Service {ServiceName} Windows status: {WindowsStatus}, ModStack status: {ModStackStatus}", 
                service.Name, windowsStatus, modStackStatus);

            return modStackStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for service ID {ServiceId}", serviceId);
            return ServiceStatus.Error;
        }
    }

    public async Task<Dictionary<long, ServiceStatus>> GetAllServiceStatusesAsync()
    {
        var services = await GetServicesAsync();
        var statuses = new Dictionary<long, ServiceStatus>();

        foreach (var service in services)
        {
            statuses[service.Id] = await GetServiceStatusAsync(service.Id);
        }

        return statuses;
    }

    public async Task<bool> IsPortAvailableAsync(int port, Protocol protocol = Protocol.TCP)
    {
        // Check if port is allocated in database
        var allocation = await _portRepository.GetByPortAsync(port);
        if (allocation?.IsInUse == true)
        {
            return false;
        }

        // Check if port is actually in use by the system
        return !IsPortInUse(port, protocol);
    }

    public async Task<List<PortAllocation>> GetPortAllocationsAsync()
    {
        return await _portRepository.GetAllAsync();
    }

    public async Task<bool> ReservePortAsync(int port, long serviceId, Protocol protocol = Protocol.TCP)
    {
        try
        {
            var existing = await _portRepository.GetByPortAsync(port);
            if (existing != null)
            {
                _logger.LogWarning("Port {Port} is already allocated", port);
                return false;
            }

            var allocation = new PortAllocation
            {
                Port = port,
                ServiceId = serviceId,
                Protocol = protocol,
                CreatedAt = DateTime.UtcNow
            };

            await _portRepository.CreateAsync(allocation);
            _logger.LogInformation("Port {Port} reserved for service ID {ServiceId}", port, serviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reserve port {Port} for service ID {ServiceId}", port, serviceId);
            return false;
        }
    }

    public async Task<bool> ReleasePortAsync(int port)
    {
        try
        {
            var allocation = await _portRepository.GetByPortAsync(port);
            if (allocation == null)
            {
                _logger.LogWarning("No allocation found for port {Port}", port);
                return false;
            }

            await _portRepository.DeleteAsync(allocation.Id);
            _logger.LogInformation("Port {Port} released", port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release port {Port}", port);
            return false;
        }
    }

    public async Task<List<ServiceLog>> GetServiceLogsAsync(long serviceId, int limit = 100)
    {
        return await _logRepository.GetByServiceIdAsync(serviceId, limit);
    }

    public async Task AddServiceLogAsync(long serviceId, ModStackLogLevel level, string message, string? details = null)
    {
        try
        {
            var log = new ServiceLog
            {
                ServiceId = serviceId,
                LogLevel = level,
                Message = message,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _logRepository.CreateAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add log entry for service ID {ServiceId}", serviceId);
        }
    }

    private async Task<bool> CreateWindowsServiceAsync(string serviceName, string binaryPath, string displayName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create \"{serviceName}\" binPath= \"{binaryPath}\" DisplayName= \"{displayName}\" start= demand",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Windows service {ServiceName}", serviceName);
            return false;
        }
    }

    private async Task<bool> RemoveWindowsServiceAsync(string serviceName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete \"{serviceName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove Windows service {ServiceName}", serviceName);
            return false;
        }
    }

    private static bool IsPortInUse(int port, Protocol protocol)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();

        if (protocol == Protocol.TCP)
        {
            var tcpListeners = properties.GetActiveTcpListeners();
            return tcpListeners.Any(endpoint => endpoint.Port == port);
        }
        else
        {
            var udpListeners = properties.GetActiveUdpListeners();
            return udpListeners.Any(endpoint => endpoint.Port == port);
        }
    }
}
