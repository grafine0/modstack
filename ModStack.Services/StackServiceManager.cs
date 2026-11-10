using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

namespace ModStack.Services;

/// <summary>
/// Manages relationships between stacks and services
/// </summary>
public class StackServiceManager
{
    private readonly ILogger<StackServiceManager> _logger;
    private readonly IStackServiceRepository _stackServiceRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IServiceRepository _serviceRepository;
    public StackServiceManager(
        ILogger<StackServiceManager> logger,
        IStackServiceRepository stackServiceRepository,
        IStackRepository stackRepository,
        IServiceRepository serviceRepository)
    {
        _logger = logger;
        _stackServiceRepository = stackServiceRepository;
        _stackRepository = stackRepository;
        _serviceRepository = serviceRepository;
    }

    /// <summary>
    /// Adds a service to a stack
    /// </summary>
    public async Task<StackService> AddServiceToStackAsync(AddServiceToStackRequest request)
    {
        try
        {
            _logger.LogInformation("Adding service {ServiceId} to stack {StackId}", request.ServiceId, request.StackId);

            // Validate stack exists
            var stack = await _stackRepository.GetByIdAsync(request.StackId);
            if (stack == null)
                throw new ArgumentException($"Stack with ID {request.StackId} not found");

            // Validate service exists
            var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
            if (service == null)
                throw new ArgumentException($"Service with ID {request.ServiceId} not found");

            // Check if service is already in stack
            var existingStackService = await _stackServiceRepository.GetByStackAndServiceAsync(request.StackId, request.ServiceId);
            if (existingStackService != null)
                throw new InvalidOperationException($"Service {service.Name} is already in stack {stack.DisplayName}");

            // Port allocation functionality removed - ports are managed manually

            // Create stack service relationship
            var stackService = new StackService
            {
                StackId = request.StackId,
                ServiceId = request.ServiceId,
                Port = request.Port,
                Configuration = new Dictionary<string, object>(),
                IsEnabled = true
            };

            var result = await _stackServiceRepository.CreateAsync(stackService);

            _logger.LogInformation("Service {ServiceId} added to stack {StackId} successfully", request.ServiceId, request.StackId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add service to stack");
            throw;
        }
    }

    /// <summary>
    /// Removes a service from a stack
    /// </summary>
    public async Task RemoveServiceFromStackAsync(long stackId, long serviceId)
    {
        try
        {
            _logger.LogInformation("Removing service {ServiceId} from stack {StackId}", serviceId, stackId);

            var stackService = await _stackServiceRepository.GetByStackAndServiceAsync(stackId, serviceId);
            if (stackService == null)
                throw new ArgumentException($"Service {serviceId} is not in stack {stackId}");

            // Port release functionality removed - ports are managed manually

            // Remove from database
            await _stackServiceRepository.DeleteAsync(stackService.Id);

            _logger.LogInformation("Service {ServiceId} removed from stack {StackId} successfully", serviceId, stackId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove service from stack");
            throw;
        }
    }

    /// <summary>
    /// Updates a stack service configuration
    /// </summary>
    public async Task<StackService> UpdateStackServiceAsync(long stackServiceId, UpdateStackServiceRequest request)
    {
        try
        {
            _logger.LogInformation("Updating stack service {StackServiceId}", stackServiceId);

            var stackService = await _stackServiceRepository.GetByIdAsync(stackServiceId);
            if (stackService == null)
                throw new ArgumentException($"Stack service with ID {stackServiceId} not found");

            // Handle port changes
            if (stackService.Port != request.Port)
            {
                // Port management functionality removed - ports are managed manually
            }

            // Update stack service
            stackService.Port = request.Port;
            // Configuration is handled separately
            stackService.IsEnabled = request.IsEnabled;

            await _stackServiceRepository.UpdateAsync(stackService);

            _logger.LogInformation("Stack service {StackServiceId} updated successfully", stackServiceId);
            return stackService;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stack service");
            throw;
        }
    }

    /// <summary>
    /// Gets all services in a stack
    /// </summary>
    public async Task<List<StackServiceWithDetails>> GetServicesInStackAsync(long stackId)
    {
        var stackServices = await _stackServiceRepository.GetByStackIdAsync(stackId);
        var result = new List<StackServiceWithDetails>();

        foreach (var stackService in stackServices)
        {
            var service = await _serviceRepository.GetByIdAsync(stackService.ServiceId);
            if (service != null)
            {
                result.Add(new StackServiceWithDetails
                {
                    StackService = stackService,
                    Service = service
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all stacks that contain a service
    /// </summary>
    public async Task<List<StackServiceWithDetails>> GetStacksForServiceAsync(long serviceId)
    {
        var stackServices = await _stackServiceRepository.GetByServiceIdAsync(serviceId);
        var result = new List<StackServiceWithDetails>();

        foreach (var stackService in stackServices)
        {
            var stack = await _stackRepository.GetByIdAsync(stackService.StackId);
            if (stack != null)
            {
                result.Add(new StackServiceWithDetails
                {
                    StackService = stackService,
                    Stack = stack
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Enables or disables a service in a stack
    /// </summary>
    public async Task ToggleStackServiceAsync(long stackServiceId, bool isEnabled)
    {
        try
        {
            var stackService = await _stackServiceRepository.GetByIdAsync(stackServiceId);
            if (stackService == null)
                throw new ArgumentException($"Stack service with ID {stackServiceId} not found");

            stackService.IsEnabled = isEnabled;
            await _stackServiceRepository.UpdateAsync(stackService);

            _logger.LogInformation("Stack service {StackServiceId} {Status}", stackServiceId, isEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle stack service");
            throw;
        }
    }

    /// <summary>
    /// Gets stack service statistics
    /// </summary>
    public async Task<StackServiceStatistics> GetStackServiceStatisticsAsync()
    {
        var stacks = await _stackRepository.GetAllAsync();
        var services = await _serviceRepository.GetAllAsync();
        var stackServices = await _stackServiceRepository.GetAllAsync();

        return new StackServiceStatistics
        {
            TotalStacks = stacks.Count(),
            TotalServices = services.Count(),
            TotalStackServiceRelationships = stackServices.Count(),
            ActiveStackServiceRelationships = stackServices.Count(ss => ss.IsEnabled),
            ServicesByStack = stackServices
                .GroupBy(ss => ss.StackId)
                .ToDictionary(g => g.Key, g => g.Count()),
            StacksByService = stackServices
                .GroupBy(ss => ss.ServiceId)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }
}

/// <summary>
/// Request model for adding service to stack
/// </summary>
public class AddServiceToStackRequest
{
    public long StackId { get; set; }
    public long ServiceId { get; set; }
    public int? Port { get; set; }
    public string? Configuration { get; set; }
}

/// <summary>
/// Request model for updating stack service
/// </summary>
public class UpdateStackServiceRequest
{
    public int? Port { get; set; }
    public string? Configuration { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Stack service with additional details
/// </summary>
public class StackServiceWithDetails
{
    public StackService StackService { get; set; } = null!;
    public ServiceDefinition? Service { get; set; }
    public Stack? Stack { get; set; }
}

/// <summary>
/// Stack service statistics
/// </summary>
public class StackServiceStatistics
{
    public int TotalStacks { get; set; }
    public int TotalServices { get; set; }
    public int TotalStackServiceRelationships { get; set; }
    public int ActiveStackServiceRelationships { get; set; }
    public Dictionary<long, int> ServicesByStack { get; set; } = new();
    public Dictionary<long, int> StacksByService { get; set; } = new();
}
