using ModStack.Common;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Linq;

namespace ModStack.Common.Models;

/// <summary>
/// Represents a stack of services working together
/// </summary>
public class Stack : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Services included in this stack
    /// </summary>
    public List<StackService> Services { get; set; } = new();
    
    /// <summary>
    /// Projects using this stack
    /// </summary>
    public List<Project> Projects { get; set; } = new();
    
    /// <summary>
    /// Current runtime status of the stack
    /// </summary>
    private StackStatus _status = StackStatus.Inactive;
    [JsonIgnore]
    public StackStatus Status 
    { 
        get => _status; 
        set 
        { 
            if (_status != value) 
            { 
                _status = value; 
                OnPropertyChanged(nameof(Status)); 
                OnPropertyChanged(nameof(CanStart)); 
                OnPropertyChanged(nameof(CanStop)); 
            } 
        } 
    }
    
    /// <summary>
    /// Stack-level configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// Indicates if the stack can be started (not active and has services)
    /// </summary>
    [JsonIgnore]
    public bool CanStart => !IsActive && Services.Any();
    
    /// <summary>
    /// Indicates if the stack can be stopped (currently active)
    /// </summary>
    [JsonIgnore]
    public bool CanStop => IsActive;
    
    /// <summary>
    /// Gets all ports used by services in this stack
    /// </summary>
    public List<int> GetUsedPorts()
    {
        return Services
            .Where(s => s.Port.HasValue)
            .Select(s => s.Port!.Value)
            .ToList();
    }
    
    /// <summary>
    /// Gets services sorted by priority (start order)
    /// </summary>
    public List<StackService> GetServicesByPriority()
    {
        return Services
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Service.ServiceType)
            .ToList();
    }
    
    /// <summary>
    /// Gets the web server service in this stack (Apache or Nginx)
    /// </summary>
    public StackService? GetWebServer()
    {
        return Services.FirstOrDefault(s => 
            s.Service?.ServiceType == ServiceType.Apache || 
            s.Service?.ServiceType == ServiceType.Nginx);
    }
    
    /// <summary>
    /// Checks if the stack has any port conflicts
    /// </summary>
    public bool HasPortConflicts()
    {
        var ports = GetUsedPorts();
        return ports.Count != ports.Distinct().Count();
    }
    
    /// <summary>
    /// Gets the database service from this stack
    /// </summary>
    public StackService? GetDatabase()
    {
        return Services.FirstOrDefault(s => 
            s.Service.ServiceType == ServiceType.MySQL ||
            s.Service.ServiceType == ServiceType.MariaDB ||
            s.Service.ServiceType == ServiceType.PostgreSQL ||
            s.Service.ServiceType == ServiceType.MongoDB);
    }
    
    /// <summary>
    /// Gets the PHP service from this stack
    /// </summary>
    public StackService? GetPHP()
    {
        return Services.FirstOrDefault(s => s.Service.ServiceType == ServiceType.PHP);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
