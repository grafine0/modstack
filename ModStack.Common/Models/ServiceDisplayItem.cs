using System.ComponentModel;
using System.Windows.Input;
using ModStack.Common;

namespace ModStack.Common.Models;

/// <summary>
/// Display model for services in the UI
/// </summary>
public class ServiceDisplayItem : INotifyPropertyChanged
{
    private string _status = string.Empty;

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceType Type { get; set; } = ServiceType.Apache;

    public string Status
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
                OnPropertyChanged(nameof(CanConfigure));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    private int _port;
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

    private string _path = string.Empty;
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

    private bool _autoStart;
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

    private string _processIds = string.Empty;
    public string ProcessIds
    {
        get => _processIds;
        set
        {
            if (_processIds != value)
            {
                _processIds = value;
                OnPropertyChanged(nameof(ProcessIds));
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(CanConfigure));
                OnPropertyChanged(nameof(CanDelete));
            }
        }
    }

    // Command delegates - set by the ViewModel
    public Func<ServiceDisplayItem, Task>? StartServiceAction { get; set; }
    public Func<ServiceDisplayItem, Task>? StopServiceAction { get; set; }
    public Func<ServiceDisplayItem, Task>? DeleteServiceAction { get; set; }
    public Func<ServiceDisplayItem, Task>? ConfigureServiceAction { get; set; }
    public Func<ServiceDisplayItem, Task>? AutoStartToggleAction { get; set; }
    
    public bool IsNotBusy => !IsBusy;
    
    // UI state properties
    public bool CanStart 
    { 
        get 
        {
            var result = !_isBusy && !string.Equals(_status, "Running", StringComparison.OrdinalIgnoreCase) && 
                        !string.Equals(_status, "Starting...", StringComparison.OrdinalIgnoreCase);
            System.Diagnostics.Debug.WriteLine($"🎛️ CanStart getter called for {Name}: Status='{_status}', IsBusy={_isBusy} → CanStart={result}");
            return result;
        }
    }
    
    public bool CanStop => !_isBusy && (string.Equals(_status, "Running", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(_status, "Stopping...", StringComparison.OrdinalIgnoreCase));

    // Additional UI state properties
    public bool CanConfigure => !_isBusy; // Services can be configured when not busy
    public bool CanDelete => !_isBusy && !string.Equals(_status, "Running", StringComparison.OrdinalIgnoreCase);
    public bool IsPHPService => Type == ServiceType.PHP;
    public bool IsApacheService => Type == ServiceType.Apache;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        System.Diagnostics.Debug.WriteLine($"🔔 PropertyChanged fired for {Name}: {propertyName}");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
