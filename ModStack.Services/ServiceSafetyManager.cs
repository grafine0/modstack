using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.Services.Interfaces;
using System.Diagnostics;

namespace ModStack.Services;

/// <summary>
/// Manages service safety during PHP configuration changes
/// </summary>
public class ServiceSafetyManager(
    IServiceRepository serviceRepository,
    IServiceManager serviceManager,
    ILogger<ServiceSafetyManager> logger) : IServiceSafetyManager
{
    private readonly IServiceRepository _serviceRepository = serviceRepository;
    private readonly IServiceManager _serviceManager = serviceManager;
    private readonly ILogger<ServiceSafetyManager> _logger = logger;

    /// <summary>
    /// Gets services that are currently running and depend on PHP configuration
    /// </summary>
    public async Task<List<ServiceDefinition>> GetRunningDependentServicesAsync(long phpServiceId, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🔍 Checking for running services dependent on PHP service {phpServiceId}");
            _logger.LogInformation("Checking for running services dependent on PHP service {PhpServiceId}", phpServiceId);
            
            var phpService = await _serviceRepository.GetByIdAsync(phpServiceId, cancellationToken);
            if (phpService == null)
            {
                Debug.WriteLine($"❌ PHP service {phpServiceId} not found");
                _logger.LogWarning("PHP service {PhpServiceId} not found", phpServiceId);
                return [];
            }

            // Get all services that could potentially use PHP
            var allServices = await _serviceRepository.GetAllAsync(cancellationToken);
            
            return await Task.Run(() =>
            {
                var webServerServices = allServices.Where(s => IsWebServerService(s)).ToList();
                
                Debug.WriteLine($"🌐 Found {webServerServices.Count} potential web server services: {string.Join(", ", webServerServices.Select(s => s.Name))}");
                
                var runningDependentServices = new List<ServiceDefinition>();
                
                foreach (var service in webServerServices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        // Note: We can't await inside Task.Run, so we'll use synchronous version
                        // This is acceptable since we're already in a background thread
                        var status = _serviceManager.GetServiceStatusAsync(service.Id).GetAwaiter().GetResult();
                        Debug.WriteLine($"🔍 Service {service.Name} status: {status}");
                        
                        if (status == ServiceStatus.Running)
                        {
                            // Check if this web server is configured to use the PHP service
                            var usesPHP = DoesServiceUsePHP(service, phpService);
                            Debug.WriteLine($"🔗 Service {service.Name} uses PHP service {phpService.Name}: {usesPHP}");
                            
                            if (usesPHP)
                            {
                                runningDependentServices.Add(service);
                                Debug.WriteLine($"✅ Added {service.Name} to dependent services list");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ Error checking service {service.Name}: {ex.Message}");
                        _logger.LogWarning(ex, "Error checking status of service {ServiceName}", service.Name);
                    }
                }
                
                Debug.WriteLine($"🎯 Found {runningDependentServices.Count} running dependent services: {string.Join(", ", runningDependentServices.Select(s => s.Name))}");
                _logger.LogInformation("Found {Count} running services dependent on PHP service {PhpServiceId}: {Services}", 
                    runningDependentServices.Count, phpServiceId, string.Join(", ", runningDependentServices.Select(s => s.Name)));
                
                return runningDependentServices;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error getting running dependent services: {ex.Message}");
            _logger.LogError(ex, "Error getting running dependent services for PHP service {PhpServiceId}", phpServiceId);
            return [];
        }
    }

    /// <summary>
    /// Safely executes PHP configuration changes with automatic service lifecycle management
    /// </summary>
    public async Task<ServiceSafetyResult> ExecuteWithServiceSafetyAsync(
        long phpServiceId, 
        Func<CancellationToken, Task<ServiceOperationResult>> operation,
        bool requireUserConsent = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🛡️ Starting safe PHP configuration operation for service {phpServiceId}");
            _logger.LogInformation("Starting safe PHP configuration operation for service {PhpServiceId}", phpServiceId);
            
            // Step 1: Identify running dependent services
            var dependentServices = await GetRunningDependentServicesAsync(phpServiceId, cancellationToken);
            
            if (dependentServices.Count == 0)
            {
                Debug.WriteLine($"✅ No dependent services running, executing operation directly");
                _logger.LogInformation("No dependent services running, executing PHP operation directly");
                
                var result = await operation(cancellationToken);
                return new ServiceSafetyResult(
                    result.Success,
                    result.Message,
                    result.ErrorCode,
                    []
                );
            }

            Debug.WriteLine($"⚠️ Found {dependentServices.Count} running dependent services that need to be managed");
            _logger.LogInformation("Found {Count} running dependent services that need lifecycle management", dependentServices.Count);

            // Step 2: Check if user consent is required
            if (requireUserConsent)
            {
                var serviceNames = string.Join(", ", dependentServices.Select(s => s.DisplayName));
                var consentMessage = $"The following services need to be restarted to safely apply PHP configuration changes:\n\n{serviceNames}\n\nThis will cause a brief service interruption. Do you want to continue?";
                
                Debug.WriteLine($"🤔 User consent required for restarting services: {serviceNames}");
                
                return new ServiceSafetyResult(
                    true,
                    consentMessage,
                    null,
                    dependentServices,
                    RequiredUserConsent: true
                );
            }

            // Step 3: Execute the safe operation workflow
            return await ExecuteSafeWorkflowAsync(dependentServices, operation, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"❌ Safe PHP operation cancelled");
            _logger.LogWarning("Safe PHP configuration operation was cancelled");
            return new ServiceSafetyResult(false, "Operation was cancelled", "OPERATION_CANCELLED");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error during safe PHP operation: {ex.Message}");
            _logger.LogError(ex, "Error during safe PHP configuration operation");
            return new ServiceSafetyResult(false, ex.Message, "SAFE_OPERATION_FAILED");
        }
    }

    /// <summary>
    /// Stops dependent services safely
    /// </summary>
    public async Task<ServiceSafetyResult> StopDependentServicesAsync(List<ServiceDefinition> dependentServices, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🛑 Stopping {dependentServices.Count} dependent services");
            _logger.LogInformation("Stopping {Count} dependent services", dependentServices.Count);
            
            var failedServices = new List<string>();
            var stoppedServices = new List<string>();
            
            foreach (var service in dependentServices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    Debug.WriteLine($"🛑 Stopping service: {service.Name}");
                    var success = await _serviceManager.StopServiceAsync(service.Id);
                    
                    if (success)
                    {
                        stoppedServices.Add(service.Name);
                        Debug.WriteLine($"✅ Successfully stopped service: {service.Name}");
                        _logger.LogInformation("Successfully stopped service {ServiceName}", service.Name);
                    }
                    else
                    {
                        failedServices.Add(service.Name);
                        Debug.WriteLine($"❌ Failed to stop service: {service.Name}");
                        _logger.LogWarning("Failed to stop service {ServiceName}", service.Name);
                    }
                }
                catch (Exception ex)
                {
                    failedServices.Add(service.Name);
                    Debug.WriteLine($"❌ Exception stopping service {service.Name}: {ex.Message}");
                    _logger.LogError(ex, "Exception occurred while stopping service {ServiceName}", service.Name);
                }
            }
            
            if (failedServices.Count > 0)
            {
                var message = $"Failed to stop some services: {string.Join(", ", failedServices)}. Stopped services: {string.Join(", ", stoppedServices)}";
                return new ServiceSafetyResult(false, message, "SERVICE_STOP_PARTIAL_FAILURE", dependentServices);
            }
            
            var successMessage = $"Successfully stopped {stoppedServices.Count} services: {string.Join(", ", stoppedServices)}";
            Debug.WriteLine($"✅ {successMessage}");
            return new ServiceSafetyResult(true, successMessage, null, dependentServices);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error stopping dependent services: {ex.Message}");
            _logger.LogError(ex, "Error stopping dependent services");
            return new ServiceSafetyResult(false, ex.Message, "SERVICE_STOP_FAILED", dependentServices);
        }
    }

    /// <summary>
    /// Restarts dependent services safely
    /// </summary>
    public async Task<ServiceSafetyResult> RestartDependentServicesAsync(List<ServiceDefinition> dependentServices, CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine($"🔄 Restarting {dependentServices.Count} dependent services");
            _logger.LogInformation("Restarting {Count} dependent services", dependentServices.Count);
            
            var failedServices = new List<string>();
            var restartedServices = new List<string>();
            
            // Wait a moment for services to fully stop and configuration to be processed
            await Task.Delay(3000, cancellationToken);
            
            foreach (var service in dependentServices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    Debug.WriteLine($"🔄 Restarting service: {service.Name}");
                    var success = await _serviceManager.StartServiceAsync(service.Id);
                    
                    if (success)
                    {
                        restartedServices.Add(service.Name);
                        Debug.WriteLine($"✅ Successfully restarted service: {service.Name}");
                        _logger.LogInformation("Successfully restarted service {ServiceName}", service.Name);
                    }
                    else
                    {
                        failedServices.Add(service.Name);
                        Debug.WriteLine($"❌ Failed to restart service: {service.Name}");
                        _logger.LogWarning("Failed to restart service {ServiceName}", service.Name);
                    }
                }
                catch (Exception ex)
                {
                    failedServices.Add(service.Name);
                    Debug.WriteLine($"❌ Exception restarting service {service.Name}: {ex.Message}");
                    _logger.LogError(ex, "Exception occurred while restarting service {ServiceName}", service.Name);
                }
            }
            
            if (failedServices.Count > 0)
            {
                var message = $"Failed to restart some services: {string.Join(", ", failedServices)}. Restarted services: {string.Join(", ", restartedServices)}";
                return new ServiceSafetyResult(false, message, "SERVICE_RESTART_PARTIAL_FAILURE", dependentServices);
            }
            
            var successMessage = $"Successfully restarted {restartedServices.Count} services: {string.Join(", ", restartedServices)}";
            Debug.WriteLine($"✅ {successMessage}");
            return new ServiceSafetyResult(true, successMessage, null, dependentServices);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error restarting dependent services: {ex.Message}");
            _logger.LogError(ex, "Error restarting dependent services");
            return new ServiceSafetyResult(false, ex.Message, "SERVICE_RESTART_FAILED", dependentServices);
        }
    }

    /// <summary>
    /// Executes the complete safe workflow: stop → modify → restart
    /// </summary>
    private async Task<ServiceSafetyResult> ExecuteSafeWorkflowAsync(
        List<ServiceDefinition> dependentServices,
        Func<CancellationToken, Task<ServiceOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine($"🔄 Executing complete safe workflow for {dependentServices.Count} services");
        _logger.LogInformation("Executing complete safe workflow for {Count} dependent services", dependentServices.Count);
        
        // Step 1: Stop dependent services
        var stopResult = await StopDependentServicesAsync(dependentServices, cancellationToken);
        if (!stopResult.Success)
        {
            Debug.WriteLine($"❌ Failed to stop services, aborting operation");
            return new ServiceSafetyResult(false, $"Failed to stop dependent services: {stopResult.Message}", stopResult.ErrorCode, dependentServices);
        }

        try
        {
            // Step 2: Execute the PHP configuration operation
            Debug.WriteLine($"⚙️ Executing PHP configuration operation");
            var operationResult = await operation(cancellationToken);
            
            if (!operationResult.Success)
            {
                Debug.WriteLine($"❌ PHP operation failed: {operationResult.Message}");
                _logger.LogError("PHP configuration operation failed: {Message}", operationResult.Message);
                
                // Still attempt to restart services even if operation failed
                Debug.WriteLine($"🔄 Attempting to restart services despite operation failure");
                var emergencyRestartResult = await RestartDependentServicesAsync(dependentServices, cancellationToken);
                
                var combinedMessage = $"PHP operation failed: {operationResult.Message}. Service restart result: {emergencyRestartResult.Message}";
                return new ServiceSafetyResult(false, combinedMessage, operationResult.ErrorCode, dependentServices);
            }

            // Step 3: Restart dependent services
            Debug.WriteLine($"🔄 PHP operation successful, restarting services");
            var restartResult = await RestartDependentServicesAsync(dependentServices, cancellationToken);
            
            if (!restartResult.Success)
            {
                Debug.WriteLine($"⚠️ PHP operation succeeded but service restart failed");
                var combinedMessage = $"PHP configuration updated successfully, but failed to restart some services: {restartResult.Message}";
                return new ServiceSafetyResult(false, combinedMessage, restartResult.ErrorCode, dependentServices);
            }

            // Success!
            var successMessage = $"PHP configuration updated successfully. {restartResult.Message}";
            Debug.WriteLine($"✅ Complete safe workflow successful");
            _logger.LogInformation("Complete safe workflow completed successfully");
            
            return new ServiceSafetyResult(true, successMessage, null, dependentServices);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Exception during safe workflow: {ex.Message}");
            _logger.LogError(ex, "Exception during safe workflow execution");
            
            // Emergency restart attempt
            try
            {
                Debug.WriteLine($"🚨 Attempting emergency service restart");
                await RestartDependentServicesAsync(dependentServices, cancellationToken);
            }
            catch (Exception restartEx)
            {
                Debug.WriteLine($"❌ Emergency restart also failed: {restartEx.Message}");
                _logger.LogError(restartEx, "Emergency service restart also failed");
            }
            
            return new ServiceSafetyResult(false, $"Operation failed with exception: {ex.Message}", "WORKFLOW_EXCEPTION", dependentServices);
        }
    }

    /// <summary>
    /// Determines if a service is a web server that could use PHP
    /// </summary>
    private static bool IsWebServerService(ServiceDefinition service)
    {
        var serviceName = service.Name.ToLowerInvariant();
        return serviceName == "apache" || serviceName == "nginx" || serviceName == "iis";
    }

    /// <summary>
    /// Determines if a web server service is configured to use a specific PHP service
    /// This is a simplified heuristic - in a real implementation, you might check configuration files
    /// </summary>
    private bool DoesServiceUsePHP(ServiceDefinition webServer, ServiceDefinition phpService)
    {
        try
        {
            // For now, we assume any running web server potentially uses any PHP service
            // In a more sophisticated implementation, you could:
            // 1. Parse Apache/Nginx configuration files
            // 2. Check for PHP-FPM pool configurations
            // 3. Look for FastCGI module configurations
            // 4. Check port mappings and service dependencies
            
            Debug.WriteLine($"🔗 Checking if {webServer.Name} uses PHP service {phpService.Name}");
            
            // Simple heuristic: if both services are installed and the web server is running,
            // assume it might be using PHP
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Error checking PHP dependency for {webServer.Name}: {ex.Message}");
            _logger.LogWarning(ex, "Error checking PHP dependency for service {ServiceName}", webServer.Name);
            
            // Default to true for safety - better to restart unnecessarily than to miss a dependency
            return true;
        }
    }
}
