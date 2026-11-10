using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Services.Interfaces;
using ModStack.Core.Interfaces;
using ModStack.Core.Services;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using System.Diagnostics;

namespace ModStack.Services;

/// <summary>
/// Manages stacks and their services
/// </summary>
public class StackManager(
    IStackRepository stackRepository,
    IStackServiceRepository stackServiceRepository,
    IServiceRepository serviceRepository,
    IServiceManager serviceManager,
    ServiceControlManager serviceControlManager,
    ILogger<StackManager> logger) : IStackManager
{
    private readonly IStackRepository _stackRepository = stackRepository;
    private readonly IStackServiceRepository _stackServiceRepository = stackServiceRepository;
    private readonly IServiceRepository _serviceRepository = serviceRepository;
    private readonly IServiceManager _serviceManager = serviceManager;
    private readonly ServiceControlManager _serviceControlManager = serviceControlManager;
    private readonly ILogger<StackManager> _logger = logger;

    public async Task<List<Stack>> GetStacksAsync()
    {
        return await _stackRepository.GetAllAsync();
    }

    public async Task<Stack?> GetStackAsync(long id)
    {
        return await _stackRepository.GetByIdAsync(id);
    }

    public async Task<Stack?> GetStackAsync(string name)
    {
        return await _stackRepository.GetByNameAsync(name);
    }

    public async Task<Stack?> GetActiveStackAsync()
    {
        return await _stackRepository.GetActiveStackAsync();
    }

    public async Task<Stack> CreateStackAsync(string name, string displayName, string? description = null)
    {
        try
        {
            _logger.LogInformation("Creating stack {StackName}", name);

            // Check if stack with same name already exists
            var existingStack = await _stackRepository.GetByNameAsync(name);
            if (existingStack != null)
            {
                throw new InvalidOperationException($"Stack with name '{name}' already exists");
            }

            var stack = new Stack
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                IsActive = false,
                IsDefault = false
            };

            var createdStack = await _stackRepository.CreateAsync(stack);
            _logger.LogInformation("Stack {StackName} created successfully with ID {StackId}", name, createdStack.Id);

            return createdStack;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create stack {StackName}", name);
            throw;
        }
    }

    public async Task<bool> UpdateStackAsync(Stack stack)
    {
        try
        {
            _logger.LogInformation("Updating stack {StackId}", stack.Id);
            var result = await _stackRepository.UpdateAsync(stack);
            
            if (result)
            {
                _logger.LogInformation("Stack {StackId} updated successfully", stack.Id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stack {StackId}", stack.Id);
            return false;
        }
    }

    public async Task<bool> DeleteStackAsync(long stackId)
    {
        try
        {
            _logger.LogInformation("Deleting stack {StackId}", stackId);

            // First check if stack is active and stop it if needed
            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack?.IsActive == true)
            {
                await StopStackAsync(stackId);
            }

            var result = await _stackRepository.DeleteAsync(stackId);
            
            if (result)
            {
                _logger.LogInformation("Stack {StackId} deleted successfully", stackId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete stack {StackId}", stackId);
            return false;
        }
    }

    public async Task<bool> AddServiceToStackAsync(long stackId, long serviceId, int? port = null, int priority = 0)
    {
        try
        {
            _logger.LogInformation("Adding service {ServiceId} to stack {StackId}", serviceId, stackId);

            // Check if stack exists
            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null)
            {
                _logger.LogError("Stack {StackId} not found", stackId);
                return false;
            }

            // Check if service exists
            var service = await _serviceRepository.GetByIdAsync(serviceId);
            if (service == null)
            {
                _logger.LogError("Service {ServiceId} not found", serviceId);
                return false;
            }

            // Check if service is already in stack
            var existingStackService = await _stackServiceRepository.GetByStackAndServiceAsync(stackId, serviceId);
            if (existingStackService != null)
            {
                _logger.LogWarning("Service {ServiceId} is already in stack {StackId}", serviceId, stackId);
                return false;
            }

            // Use provided port or service default port
            var effectivePort = port ?? service.DefaultPort;

            // Check for port conflicts within the stack
            if (port.HasValue)
            {
                var stackServices = await _stackServiceRepository.GetByStackIdAsync(stackId);
                var portConflict = stackServices.Any(ss => ss.Port == effectivePort);
                if (portConflict)
                {
                    _logger.LogError("Port {Port} is already used by another service in stack {StackId}", effectivePort, stackId);
                    return false;
                }
            }

            var stackService = new StackService
            {
                StackId = stackId,
                ServiceId = serviceId,
                Port = effectivePort,
                Priority = priority,
                IsEnabled = true,
                Service = service
            };

            System.Diagnostics.Debug.WriteLine($"🔧 About to create StackService: StackId={stackService.StackId}, ServiceId={stackService.ServiceId}, Port={stackService.Port}, Priority={stackService.Priority}");
            
            var createdStackService = await _stackServiceRepository.CreateAsync(stackService);
            
            System.Diagnostics.Debug.WriteLine($"✅ StackService created with ID: {createdStackService.Id}");
            _logger.LogInformation("Service {ServiceId} added to stack {StackId} successfully", serviceId, stackId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add service {ServiceId} to stack {StackId}", serviceId, stackId);
            return false;
        }
    }

    public async Task<bool> RemoveServiceFromStackAsync(long stackId, long serviceId)
    {
        try
        {
            _logger.LogInformation("Removing service {ServiceId} from stack {StackId}", serviceId, stackId);

            var result = await _stackServiceRepository.DeleteByStackAndServiceAsync(stackId, serviceId);
            
            if (result)
            {
                _logger.LogInformation("Service {ServiceId} removed from stack {StackId} successfully", serviceId, stackId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove service {ServiceId} from stack {StackId}", serviceId, stackId);
            return false;
        }
    }

    public async Task<bool> UpdateStackServiceAsync(StackService stackService)
    {
        try
        {
            _logger.LogInformation("Updating stack service {StackServiceId}", stackService.Id);
            
            var result = await _stackServiceRepository.UpdateAsync(stackService);
            
            if (result)
            {
                _logger.LogInformation("Stack service {StackServiceId} updated successfully", stackService.Id);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stack service {StackServiceId}", stackService.Id);
            return false;
        }
    }

    public async Task<bool> StartStackAsync(long stackId)
    {
        try
        {
            _logger.LogInformation("Starting stack {StackId}", stackId);
            System.Diagnostics.Debug.WriteLine($"🔍 StackManager.StartStackAsync: Starting stack {stackId}");

            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null)
            {
                _logger.LogError("Stack {StackId} not found", stackId);
                System.Diagnostics.Debug.WriteLine($"❌ Stack {stackId} not found in database");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"📋 Stack '{stack.DisplayName}' found with {stack.Services.Count} services");
            foreach (var service in stack.Services)
            {
                System.Diagnostics.Debug.WriteLine($"   Service: {service.Service.Name} v{service.Service.Version} - IsInstalled: {service.Service.IsInstalled}, IsEnabled: {service.IsEnabled}");
            }

            // Get services ordered by priority
            var stackServices = stack.GetServicesByPriority().Where(ss => ss.IsEnabled).ToList();
            
            System.Diagnostics.Debug.WriteLine($"🔄 Found {stackServices.Count} enabled services to start");
            foreach (var ss in stackServices)
            {
                System.Diagnostics.Debug.WriteLine($"   - {ss.Service.Name}: IsInstalled={ss.Service.IsInstalled}, IsEnabled={ss.IsEnabled}, Priority={ss.Priority}");
            }
            
            var startedServices = new List<long>();
            var failedServices = new List<string>();

            foreach (var stackService in stackServices)
            {
                try
                {
                    // Fetch service directly from repository to ensure we have complete data
                    var service = await _serviceRepository.GetByIdAsync(stackService.ServiceId);
                    if (service == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to load service {stackService.ServiceId} from repository");
                        failedServices.Add($"Service ID {stackService.ServiceId} (service not found)");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"🚀 Processing service: {service.Name} (ID: {service.Id})");

                    // Verify service is installed
                    if (!service.IsInstalled)
                    {
                        _logger.LogWarning("Service {ServiceName} is not installed, skipping", service.Name);
                        System.Diagnostics.Debug.WriteLine($"❌ Service {service.Name} is not installed, skipping");
                        failedServices.Add($"{service.Name} (not installed)");
                        continue;
                    }

                    // Convert ServiceDefinition to ServiceDisplayItem for ServiceControlManager
                    var serviceDisplayItem = ConvertToServiceDisplayItem(service, stackService.Port);
                    
                    // Start the service using ServiceControlManager
                    try
                    {
                        await _serviceControlManager.StartServiceAsync(serviceDisplayItem);
                        startedServices.Add(stackService.ServiceId);
                        System.Diagnostics.Debug.WriteLine($"✅ Service {service.Name} started successfully (ID: {stackService.ServiceId})");
                        _logger.LogInformation("Service {ServiceName} started successfully", service.Name);
                    }
                    catch (Exception ex)
                    {
                        var serviceName = service?.Name ?? $"Service ID {stackService.ServiceId}";
                        failedServices.Add(serviceName);
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to start service {serviceName}: {ex.Message}");
                        _logger.LogError(ex, "Exception starting service {ServiceName}", serviceName);
                    }
                }
                catch (Exception ex)
                {
                    failedServices.Add($"Service ID {stackService.ServiceId}");
                    _logger.LogError(ex, "Exception processing service {ServiceId}", stackService.ServiceId);
                }
            }

            // Update stack status
            System.Diagnostics.Debug.WriteLine($"📊 Stack start summary:");
            System.Diagnostics.Debug.WriteLine($"   Started services: {startedServices.Count} (IDs: {string.Join(", ", startedServices)})");
            System.Diagnostics.Debug.WriteLine($"   Failed services: {failedServices.Count} ({string.Join(", ", failedServices)})");
            
            stack.IsActive = startedServices.Count > 0;
            stack.Status = startedServices.Count > 0 ? StackStatus.Active : StackStatus.Inactive;
            System.Diagnostics.Debug.WriteLine($"🔄 Setting stack.IsActive = {stack.IsActive}, Status = {stack.Status}");
            
            await _stackRepository.UpdateAsync(stack);
            System.Diagnostics.Debug.WriteLine($"💾 Stack status updated in database");

            if (failedServices.Count > 0)
            {
                _logger.LogWarning("Stack {StackId} started with {FailedCount} failed services: {FailedServices}", 
                    stackId, failedServices.Count, string.Join(", ", failedServices));
                System.Diagnostics.Debug.WriteLine($"⚠️ Returning false due to failed services");
                return false; // Partial failure
            }

            _logger.LogInformation("Stack {StackId} started successfully", stackId);
            System.Diagnostics.Debug.WriteLine($"✅ Stack {stackId} started successfully, returning true");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stack {StackId}", stackId);
            return false;
        }
    }

    public async Task<bool> StopStackAsync(long stackId)
    {
        try
        {
            _logger.LogInformation("Stopping stack {StackId}", stackId);

            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null)
            {
                _logger.LogError("Stack {StackId} not found", stackId);
                return false;
            }

            // Get services in reverse priority order (stop in reverse start order)
            var stackServices = stack.GetServicesByPriority().Where(ss => ss.IsEnabled).Reverse().ToList();
            
            var stoppedServices = new List<long>();
            var failedServices = new List<string>();

            foreach (var stackService in stackServices)
            {
                try
                {
                    // Fetch service directly from repository to ensure we have complete data
                    var service = await _serviceRepository.GetByIdAsync(stackService.ServiceId);
                    if (service == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to load service {stackService.ServiceId} from repository");
                        failedServices.Add($"Service ID {stackService.ServiceId} (service not found)");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"🛑 Stopping service: {service.Name} (ID: {service.Id})");
                    
                    // Convert ServiceDefinition to ServiceDisplayItem for ServiceControlManager
                    var serviceDisplayItem = ConvertToServiceDisplayItem(service, stackService.Port);
                    
                    // Detect service status first to populate ProcessIds
                    var status = await _serviceControlManager.DetectServiceStatusAsync(serviceDisplayItem);
                    System.Diagnostics.Debug.WriteLine($"📊 Service {service.Name} status: {status}, PIDs: {serviceDisplayItem.ProcessIds}");
                    
                    // Only try to stop if service is running and has ProcessIds
                    if (status == "Running" && !string.IsNullOrEmpty(serviceDisplayItem.ProcessIds))
                    {
                        await _serviceControlManager.StopServiceAsync(serviceDisplayItem);
                        stoppedServices.Add(stackService.ServiceId);
                        System.Diagnostics.Debug.WriteLine($"✅ Service {service.Name} stopped successfully (ID: {stackService.ServiceId})");
                        _logger.LogInformation("Service {ServiceName} stopped successfully", service.Name);
                    }
                    else if (status == "Stopped" || string.IsNullOrEmpty(serviceDisplayItem.ProcessIds))
                    {
                        // Service is already stopped, count as success
                        stoppedServices.Add(stackService.ServiceId);
                        System.Diagnostics.Debug.WriteLine($"ℹ️ Service {service.Name} is already stopped (ID: {stackService.ServiceId})");
                        _logger.LogInformation("Service {ServiceName} is already stopped", service.Name);
                    }
                    else
                    {
                        failedServices.Add(service.Name);
                        System.Diagnostics.Debug.WriteLine($"❌ Failed to stop service {service.Name} (status: {status}, no ProcessIds)");
                        _logger.LogError("Failed to stop service {ServiceName} - status: {Status}", service.Name, status);
                    }
                }
                catch (Exception ex)
                {
                    failedServices.Add($"Service ID {stackService.ServiceId}");
                    _logger.LogError(ex, "Exception stopping service {ServiceId}", stackService.ServiceId);
                }
            }

            // Update stack status
            System.Diagnostics.Debug.WriteLine($"📊 Stack stop summary:");
            System.Diagnostics.Debug.WriteLine($"   Stopped services: {stoppedServices.Count} (IDs: {string.Join(", ", stoppedServices)})");
            System.Diagnostics.Debug.WriteLine($"   Failed services: {failedServices.Count} ({string.Join(", ", failedServices)})");
            
            stack.IsActive = false;
            stack.Status = StackStatus.Inactive;
            System.Diagnostics.Debug.WriteLine($"🔄 Setting stack.IsActive = {stack.IsActive}, Status = {stack.Status}");
            
            await _stackRepository.UpdateAsync(stack);
            System.Diagnostics.Debug.WriteLine($"💾 Stack status updated in database");

            if (failedServices.Count > 0)
            {
                _logger.LogWarning("Stack {StackId} stopped with {FailedCount} failed services: {FailedServices}", 
                    stackId, failedServices.Count, string.Join(", ", failedServices));
                System.Diagnostics.Debug.WriteLine($"⚠️ Some services failed to stop");
            }

            _logger.LogInformation("Stack {StackId} stopped", stackId);
            System.Diagnostics.Debug.WriteLine($"✅ Stack {stackId} stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop stack {StackId}", stackId);
            return false;
        }
    }

    public async Task<bool> SwitchStackAsync(long stackId)
    {
        try
        {
            _logger.LogInformation("Switching to stack {StackId}", stackId);

            // Stop current active stack
            var currentStack = await _stackRepository.GetActiveStackAsync();
            if (currentStack != null && currentStack.Id != stackId)
            {
                _logger.LogInformation("Stopping current active stack {CurrentStackId}", currentStack.Id);
                await StopStackAsync(currentStack.Id);
            }

            // Start the new stack
            var result = await StartStackAsync(stackId);
            
            if (result)
            {
                _logger.LogInformation("Successfully switched to stack {StackId}", stackId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to stack {StackId}", stackId);
            return false;
        }
    }

    public async Task<StackStatus> GetStackStatusAsync(long stackId)
    {
        try
        {
            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null)
            {
                return StackStatus.Inactive;
            }

            if (!stack.IsActive)
            {
                return StackStatus.Inactive;
            }

            var serviceStatuses = await _serviceManager.GetAllServiceStatusesAsync();
            var stackServiceIds = stack.Services.Where(ss => ss.IsEnabled).Select(ss => ss.ServiceId).ToList();
            
            if (stackServiceIds.Count == 0)
            {
                return StackStatus.Inactive;
            }

            var runningServices = stackServiceIds.Count(id => 
                serviceStatuses.GetValueOrDefault<long, ServiceStatus>(id, ServiceStatus.Unknown) == ServiceStatus.Running);
            var stoppedServices = stackServiceIds.Count(id => 
                serviceStatuses.GetValueOrDefault<long, ServiceStatus>(id, ServiceStatus.Unknown) == ServiceStatus.Stopped);
            var startingServices = stackServiceIds.Count(id => 
                serviceStatuses.GetValueOrDefault<long, ServiceStatus>(id, ServiceStatus.Unknown) == ServiceStatus.Starting);
            var stoppingServices = stackServiceIds.Count(id => 
                serviceStatuses.GetValueOrDefault<long, ServiceStatus>(id, ServiceStatus.Unknown) == ServiceStatus.Stopping);
            var errorServices = stackServiceIds.Count(id => 
                serviceStatuses.GetValueOrDefault<long, ServiceStatus>(id, ServiceStatus.Unknown) == ServiceStatus.Error);

            if (errorServices > 0)
            {
                return StackStatus.Error;
            }

            if (startingServices > 0)
            {
                return StackStatus.Starting;
            }

            if (stoppingServices > 0)
            {
                return StackStatus.Stopping;
            }

            if (runningServices == stackServiceIds.Count)
            {
                return StackStatus.Active;
            }

            if (runningServices > 0)
            {
                return StackStatus.PartiallyActive;
            }

            return StackStatus.Inactive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for stack {StackId}", stackId);
            return StackStatus.Error;
        }
    }

    public async Task<List<string>> ValidateStackAsync(long stackId)
    {
        var errors = new List<string>();

        try
        {
            var stack = await _stackRepository.GetByIdAsync(stackId);
            if (stack == null)
            {
                errors.Add($"Stack with ID {stackId} not found");
                return errors;
            }

            // Check if stack has services
            if (stack.Services.Count == 0)
            {
                errors.Add("Stack has no services configured");
            }

            // Check for port conflicts
            if (stack.HasPortConflicts())
            {
                errors.Add("Stack has port conflicts between services");
            }

            // Check if services are installed
            var uninstalledServices = stack.Services.Where(ss => !ss.Service.IsInstalled).ToList();
            foreach (var service in uninstalledServices)
            {
                errors.Add($"Service {service.Service.Name} v{service.Service.Version} is not installed");
            }

            // Check for missing dependencies
            foreach (var stackService in stack.Services)
            {
                if (!string.IsNullOrEmpty(stackService.Service.Dependencies))
                {
                    try
                    {
                        var dependencies = System.Text.Json.JsonSerializer.Deserialize<List<string>>(stackService.Service.Dependencies);
                        if (dependencies != null)
                        {
                            foreach (var dependency in dependencies)
                            {
                                var dependencyExists = stack.Services.Any(ss => 
                                    ss.Service.Name == dependency || ss.Service.UniqueId == dependency);
                                
                                if (!dependencyExists)
                                {
                                    errors.Add($"Service {stackService.Service.Name} depends on {dependency} which is not in the stack");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore invalid JSON in dependencies
                    }
                }
            }

            // Check for circular dependencies
            // TODO: Implement circular dependency detection

            _logger.LogInformation("Stack {StackId} validation completed with {ErrorCount} errors", stackId, errors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate stack {StackId}", stackId);
            errors.Add($"Validation failed: {ex.Message}");
        }

        return errors;
    }

    /// <summary>
    /// Converts a ServiceDefinition to a ServiceDisplayItem for use with ServiceControlManager
    /// </summary>
    private static ServiceDisplayItem ConvertToServiceDisplayItem(ServiceDefinition service, int? port = null)
    {
        // Convert ServiceStatus enum (stored as int) to string, handling Unknown as "Stopped"
        var serviceStatus = (ServiceStatus)service.Status;
        var statusString = serviceStatus == ServiceStatus.Unknown 
            ? "Stopped" 
            : serviceStatus.ToString();

        return new ServiceDisplayItem
        {
            Id = service.Id,
            Name = service.DisplayName ?? service.Name,
            Version = service.Version ?? string.Empty,
            Description = service.Description ?? string.Empty,
            Type = service.ServiceType,
            Status = statusString,
            Port = port ?? service.DefaultPort ?? 0,
            Path = service.BinaryPath ?? string.Empty,
            AutoStart = service.AutoStart
        };
    }
}
