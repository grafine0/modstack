using ModStack.Common;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a port allocation in the system
/// </summary>
public class PortAllocation
{
    public long Id { get; set; }
    public int Port { get; set; }
    public long? ServiceId { get; set; }
    public long? StackId { get; set; }
    public Protocol Protocol { get; set; } = Protocol.TCP;
    public bool IsReserved { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// The service using this port
    /// </summary>
    public ServiceDefinition? Service { get; set; }
    
    /// <summary>
    /// The stack this port allocation belongs to
    /// </summary>
    public Stack? Stack { get; set; }
    
    /// <summary>
    /// Checks if the port is currently in use
    /// </summary>
    public bool IsInUse => ServiceId.HasValue || IsReserved;
    
    /// <summary>
    /// Gets a description of what is using this port
    /// </summary>
    public string GetUsageDescription()
    {
        if (IsReserved)
            return Description ?? "Reserved";
        
        if (Service != null)
            return $"{Service.DisplayName} v{Service.Version}";
        
        return "Available";
    }
}
