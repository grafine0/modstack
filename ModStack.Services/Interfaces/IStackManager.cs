using ModStack.Common;
using ModStack.Common.Models;

namespace ModStack.Services.Interfaces;

/// <summary>
/// Interface for managing stacks
/// </summary>
public interface IStackManager
{
    /// <summary>
    /// Gets all stacks
    /// </summary>
    Task<List<Stack>> GetStacksAsync();
    
    /// <summary>
    /// Gets a stack by ID
    /// </summary>
    Task<Stack?> GetStackAsync(long id);
    
    /// <summary>
    /// Gets a stack by name
    /// </summary>
    Task<Stack?> GetStackAsync(string name);
    
    /// <summary>
    /// Gets the currently active stack
    /// </summary>
    Task<Stack?> GetActiveStackAsync();
    
    /// <summary>
    /// Creates a new stack
    /// </summary>
    Task<Stack> CreateStackAsync(string name, string displayName, string? description = null);
    
    /// <summary>
    /// Updates an existing stack
    /// </summary>
    Task<bool> UpdateStackAsync(Stack stack);
    
    /// <summary>
    /// Deletes a stack
    /// </summary>
    Task<bool> DeleteStackAsync(long stackId);
    
    /// <summary>
    /// Adds a service to a stack
    /// </summary>
    Task<bool> AddServiceToStackAsync(long stackId, long serviceId, int? port = null, int priority = 0);
    
    /// <summary>
    /// Removes a service from a stack
    /// </summary>
    Task<bool> RemoveServiceFromStackAsync(long stackId, long serviceId);
    
    /// <summary>
    /// Updates a service configuration in a stack
    /// </summary>
    Task<bool> UpdateStackServiceAsync(StackService stackService);
    
    /// <summary>
    /// Starts all services in a stack
    /// </summary>
    Task<bool> StartStackAsync(long stackId);
    
    /// <summary>
    /// Stops all services in a stack
    /// </summary>
    Task<bool> StopStackAsync(long stackId);
    
    /// <summary>
    /// Switches to a different stack (stops current, starts new)
    /// </summary>
    Task<bool> SwitchStackAsync(long stackId);
    
    /// <summary>
    /// Gets the status of a stack
    /// </summary>
    Task<StackStatus> GetStackStatusAsync(long stackId);
    
    /// <summary>
    /// Validates a stack configuration
    /// </summary>
    Task<List<string>> ValidateStackAsync(long stackId);
}

