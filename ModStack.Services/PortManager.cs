using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Common;
using System.Net.NetworkInformation;

namespace ModStack.Services;

/// <summary>
/// Manages port allocations and conflict detection
/// </summary>
public class PortManager(
    ILogger<PortManager> logger,
    IPortAllocationRepository portAllocationRepository,
    IServiceRepository serviceRepository)
{
    private readonly ILogger<PortManager> _logger = logger;
    private readonly IPortAllocationRepository _portAllocationRepository = portAllocationRepository;
    private readonly IServiceRepository _serviceRepository = serviceRepository;

    /// <summary>
    /// Allocates a port for a service
    /// </summary>
    public async Task<PortAllocation> AllocatePortAsync(AllocatePortRequest request)
    {
        try
        {
            _logger.LogInformation("Allocating port {Port} for service {ServiceId}", request.Port, request.ServiceId);

            // Check if port is already allocated
            var existingAllocation = await _portAllocationRepository.GetByPortAsync(request.Port);
            if (existingAllocation != null && existingAllocation.IsReserved)
            {
                throw new InvalidOperationException($"Port {request.Port} is already allocated to service {existingAllocation.ServiceId}");
            }

            // Check if port is actually in use on the system
            if (await IsPortInUseAsync(request.Port))
            {
                throw new InvalidOperationException($"Port {request.Port} is currently in use on the system");
            }

            // Create or update port allocation
            var allocation = existingAllocation ?? new PortAllocation
            {
                Port = request.Port,
                Protocol = Enum.Parse<Protocol>(request.Protocol),
                Description = request.Description,
                CreatedAt = DateTime.Now
            };

            allocation.ServiceId = request.ServiceId;
            allocation.StackId = request.StackId;
            allocation.IsReserved = true;

            var result = existingAllocation == null 
                ? await _portAllocationRepository.CreateAsync(allocation)
                : allocation;
            
            if (existingAllocation != null)
            {
                await _portAllocationRepository.UpdateAsync(allocation);
            }

            _logger.LogInformation("Port {Port} allocated successfully", request.Port);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate port {Port}", request.Port);
            throw;
        }
    }

    /// <summary>
    /// Releases a port allocation
    /// </summary>
    public async Task ReleasePortAsync(int port)
    {
        try
        {
            _logger.LogInformation("Releasing port {Port}", port);

            var allocation = await _portAllocationRepository.GetByPortAsync(port);
            if (allocation == null)
            {
                _logger.LogWarning("Port {Port} is not allocated", port);
                return;
            }

            allocation.IsReserved = false;
            allocation.ServiceId = null;
            allocation.StackId = null;

            await _portAllocationRepository.UpdateAsync(allocation);

            _logger.LogInformation("Port {Port} released successfully", port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release port {Port}", port);
            throw;
        }
    }

    /// <summary>
    /// Finds an available port in a range
    /// </summary>
    public async Task<int> FindAvailablePortAsync(int startPort, int endPort, int? excludePort = null)
    {
        try
        {
            _logger.LogInformation("Finding available port in range {StartPort}-{EndPort}", startPort, endPort);

            for (int port = startPort; port <= endPort; port++)
            {
                if (excludePort.HasValue && port == excludePort.Value)
                    continue;

                // Check if port is allocated in database
                var allocation = await _portAllocationRepository.GetByPortAsync(port);
                if (allocation != null && allocation.IsReserved)
                    continue;

                // Check if port is in use on system
                if (await IsPortInUseAsync(port))
                    continue;

                _logger.LogInformation("Found available port {Port}", port);
                return port;
            }

            throw new InvalidOperationException($"No available ports found in range {startPort}-{endPort}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find available port");
            throw;
        }
    }

    /// <summary>
    /// Gets all port allocations
    /// </summary>
    public async Task<List<PortAllocation>> GetAllPortAllocationsAsync()
    {
        return await _portAllocationRepository.GetAllAsync();
    }

    /// <summary>
    /// Gets port allocations for a service
    /// </summary>
    public async Task<List<PortAllocation>> GetPortAllocationsForServiceAsync(long serviceId)
    {
        return await _portAllocationRepository.GetByServiceIdAsync(serviceId);
    }

    /// <summary>
    /// Gets port allocations for a stack
    /// </summary>
    public async Task<List<PortAllocation>> GetPortAllocationsForStackAsync(int stackId)
    {
        return await _portAllocationRepository.GetByStackIdAsync(stackId);
    }

    /// <summary>
    /// Checks if a port is available
    /// </summary>
    public async Task<bool> IsPortAvailableAsync(int port)
    {
        // Check database allocation
        var allocation = await _portAllocationRepository.GetByPortAsync(port);
        if (allocation != null && allocation.IsReserved)
            return false;

        // Check system usage
        return !await IsPortInUseAsync(port);
    }

    /// <summary>
    /// Reserves a port without assigning it to a service
    /// </summary>
    public async Task<PortAllocation> ReservePortAsync(int port, string description)
    {
        try
        {
            _logger.LogInformation("Reserving port {Port}", port);

            var existingAllocation = await _portAllocationRepository.GetByPortAsync(port);
            if (existingAllocation != null && existingAllocation.IsReserved)
            {
                throw new InvalidOperationException($"Port {port} is already reserved");
            }

            var allocation = existingAllocation ?? new PortAllocation
            {
                Port = port,
                Protocol = Protocol.TCP,
                Description = description,
                CreatedAt = DateTime.Now
            };

            allocation.IsReserved = true;

            var result = existingAllocation == null 
                ? await _portAllocationRepository.CreateAsync(allocation)
                : allocation;
            
            if (existingAllocation != null)
            {
                await _portAllocationRepository.UpdateAsync(allocation);
            }

            _logger.LogInformation("Port {Port} reserved successfully", port);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reserve port {Port}", port);
            throw;
        }
    }

    /// <summary>
    /// Gets port usage statistics
    /// </summary>
    public async Task<PortUsageStatistics> GetPortUsageStatisticsAsync()
    {
        var allocations = await _portAllocationRepository.GetAllAsync();
        var reservedPorts = allocations.Count(a => a.IsReserved);
        var availablePorts = allocations.Count(a => !a.IsReserved);

        return new PortUsageStatistics
        {
            TotalAllocatedPorts = allocations.Count,
            ReservedPorts = reservedPorts,
            AvailablePorts = availablePorts,
            PortsByService = allocations
                .Where(a => a.IsReserved && a.ServiceId.HasValue)
                .GroupBy(a => a.ServiceId!.Value)
                .ToDictionary(g => g.Key, g => g.Count()),
            PortsByStack = allocations
                .Where(a => a.IsReserved && a.StackId.HasValue)
                .GroupBy(a => a.StackId!.Value)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    private async Task<bool> IsPortInUseAsync(int port)
    {
        try
        {
            return await Task.Run(() =>
            {
                // Check TCP connections
                var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
                if (tcpConnections.Any(conn => conn.LocalEndPoint.Port == port))
                    return true;

                // Check TCP listeners
                var tcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                if (tcpListeners.Any(listener => listener.Port == port))
                    return true;

                // Check UDP listeners
                var udpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
                if (udpListeners.Any(listener => listener.Port == port))
                    return true;

                return false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if port {Port} is in use", port);
            return false; // Assume port is available if we can't check
        }
    }
}

/// <summary>
/// Request model for port allocation
/// </summary>
public class AllocatePortRequest
{
    public int Port { get; set; }
    public int? ServiceId { get; set; }
    public int? StackId { get; set; }
    public string Protocol { get; set; } = "TCP";
    public string? Description { get; set; }
}

/// <summary>
/// Port usage statistics
/// </summary>
public class PortUsageStatistics
{
    public int TotalAllocatedPorts { get; set; }
    public int ReservedPorts { get; set; }
    public int AvailablePorts { get; set; }
    public Dictionary<long, int> PortsByService { get; set; } = [];
    public Dictionary<long, int> PortsByStack { get; set; } = [];
}
