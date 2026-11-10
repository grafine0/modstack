using ModStack.Common.Models;

namespace ModStack.Services.Interfaces;

/// <summary>
/// Simple operation result for service safety operations
/// </summary>
public record ServiceOperationResult(bool Success, string Message, string? ErrorCode = null);

/// <summary>
/// Result of a service safety operation
/// </summary>
public record ServiceSafetyResult(
    bool Success,
    string Message,
    string? ErrorCode = null,
    List<ServiceDefinition>? AffectedServices = null,
    bool RequiredUserConsent = false
);

/// <summary>
/// Manages service safety during configuration changes
/// </summary>
public interface IServiceSafetyManager
{
    /// <summary>
    /// Checks which services are running that depend on PHP configuration
    /// </summary>
    /// <param name="phpServiceId">The PHP service ID being modified</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of running dependent services</returns>
    Task<List<ServiceDefinition>> GetRunningDependentServicesAsync(long phpServiceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Safely executes PHP configuration changes with service lifecycle management
    /// </summary>
    /// <param name="phpServiceId">The PHP service ID</param>
    /// <param name="operation">The configuration operation to execute</param>
    /// <param name="requireUserConsent">Whether to require user consent for service restarts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the safe operation</returns>
    Task<ServiceSafetyResult> ExecuteWithServiceSafetyAsync(
        long phpServiceId, 
        Func<CancellationToken, Task<ServiceOperationResult>> operation,
        bool requireUserConsent = true,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops services that depend on PHP configuration
    /// </summary>
    /// <param name="dependentServices">Services to stop</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the stop operation</returns>
    Task<ServiceSafetyResult> StopDependentServicesAsync(List<ServiceDefinition> dependentServices, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Restarts services that were stopped for configuration changes
    /// </summary>
    /// <param name="dependentServices">Services to restart</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the restart operation</returns>
    Task<ServiceSafetyResult> RestartDependentServicesAsync(List<ServiceDefinition> dependentServices, CancellationToken cancellationToken = default);
}
