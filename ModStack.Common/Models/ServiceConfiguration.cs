using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ModStack.Common.Models;

/// <summary>
/// Strongly-typed model for service configuration settings
/// </summary>
public class ServiceConfiguration : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _port;
    private bool _autoStart;
    private string _path = string.Empty;
    private string _version = string.Empty;

    /// <summary>
    /// Service port number
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port 
    { 
        get => _port; 
        set 
        { 
            if (_port != value)
            {
                _port = value; 
                OnPropertyChanged(nameof(Port));
            }
        } 
    }
    
    /// <summary>
    /// Whether the service should auto-start
    /// </summary>
    public bool AutoStart 
    { 
        get => _autoStart; 
        set 
        { 
            if (_autoStart != value)
            {
                _autoStart = value; 
                OnPropertyChanged(nameof(AutoStart));
            }
        } 
    }
    
    /// <summary>
    /// Service installation path
    /// </summary>
    [Required(ErrorMessage = "Path is required")]
    [MaxLength(500, ErrorMessage = "Path cannot exceed 500 characters")]
    public string Path 
    { 
        get => _path; 
        set 
        { 
            if (_path != value)
            {
                _path = value ?? string.Empty; 
                OnPropertyChanged(nameof(Path));
            }
        } 
    }
    
    /// <summary>
    /// Service version
    /// </summary>
    [Required(ErrorMessage = "Version is required")]
    [MaxLength(20, ErrorMessage = "Version cannot exceed 20 characters")]
    public string Version 
    { 
        get => _version; 
        set 
        { 
            if (_version != value)
            {
                _version = value ?? string.Empty; 
                OnPropertyChanged(nameof(Version));
            }
        } 
    }
    
    /// <summary>
    /// Additional configuration properties (extensible)
    /// </summary>
    public Dictionary<string, object> ExtendedProperties { get; set; } = new();
    
    /// <summary>
    /// Creates a ServiceConfiguration from a ServiceDisplayItem
    /// </summary>
    public static ServiceConfiguration FromServiceDisplayItem(ServiceDisplayItem service)
    {
        return new ServiceConfiguration
        {
            Port = service.Port,
            AutoStart = service.AutoStart,
            Path = service.Path,
            Version = service.Version
        };
    }
    
    /// <summary>
    /// Applies this configuration to a ServiceDisplayItem
    /// </summary>
    public void ApplyToServiceDisplayItem(ServiceDisplayItem service)
    {
        service.Port = Port;
        service.AutoStart = AutoStart;
        service.Path = Path;
        service.Version = Version;
    }
    
    /// <summary>
    /// Validates the configuration
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (Port < 1 || Port > 65535)
            errors.Add("Port must be between 1 and 65535");
            
        if (string.IsNullOrWhiteSpace(Path))
            errors.Add("Path is required");
        else if (Path.Length > 500)
            errors.Add("Path cannot exceed 500 characters");
            
        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("Version is required");
        else if (Version.Length > 20)
            errors.Add("Version cannot exceed 20 characters");
            
        return errors;
    }
    
    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
