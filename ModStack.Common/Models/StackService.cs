using System.Text.Json.Serialization;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a service within a stack with stack-specific configuration
/// </summary>
public class StackService
{
    public long Id { get; set; }
    public long StackId { get; set; }
    public long ServiceId { get; set; }
    public int? Port { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Stack-specific configuration overrides for this service
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = [];
    
    /// <summary>
    /// The service definition
    /// </summary>
    public ServiceDefinition Service { get; set; } = new();
    
    /// <summary>
    /// The stack this service belongs to
    /// </summary>
    [JsonIgnore]
    public Stack? Stack { get; set; }
    
    /// <summary>
    /// Gets the effective port for this service (stack-specific or default)
    /// </summary>
    public int EffectivePort => Port ?? Service.DefaultPort ?? 0;
    
    /// <summary>
    /// Gets merged configuration (service default + stack overrides)
    /// </summary>
    public Dictionary<string, object> GetMergedConfiguration()
    {
        var merged = new Dictionary<string, object>();
        
        // Parse service configuration if it exists
        if (!string.IsNullOrEmpty(Service.Configuration))
        {
            try
            {
                var serviceConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(Service.Configuration);
                if (serviceConfig != null)
                {
                    foreach (var kvp in serviceConfig)
                    {
                        merged[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch
            {
                // If parsing fails, ignore service configuration
            }
        }
        
        // Add stack-specific configuration overrides
        foreach (var kvp in Configuration)
        {
            merged[kvp.Key] = kvp.Value;
        }
        
        return merged;
    }
    
    /// <summary>
    /// Checks if this service has a dependency on another service in the stack
    /// </summary>
    public bool DependsOn(StackService other)
    {
        // Check dependencies from the Dependencies field
        if (!string.IsNullOrEmpty(Service.Dependencies))
        {
            try
            {
                var dependencies = System.Text.Json.JsonSerializer.Deserialize<List<string>>(Service.Dependencies);
                if (dependencies != null)
                {
                    return dependencies.Contains(other.Service.Name) ||
                           dependencies.Contains(other.Service.UniqueId ?? "") ||
                           (!string.IsNullOrEmpty(Service.UniqueId) && dependencies.Contains(Service.UniqueId));
                }
            }
            catch
            {
                // If parsing fails, fall back to simple name check
            }
        }
        
        // Fallback to simple name-based dependency check
        return Service.Name.Equals(other.Service.Name, StringComparison.OrdinalIgnoreCase);
    }
}
