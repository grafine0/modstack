using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for Apache module display
/// </summary>
public class ApacheModuleViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private long _id;
    private string _name = string.Empty;
    private string _moduleName = string.Empty;
    private string _fileName = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;
    private string _apacheVersion = string.Empty;
    private bool _isEnabled;
    private bool _isRequired;
    private bool _isAvailable;

    public long Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ModuleName
    {
        get => _moduleName;
        set => SetProperty(ref _moduleName, value);
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string ApacheVersion
    {
        get => _apacheVersion;
        set => SetProperty(ref _apacheVersion, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(ToggleButtonText));
            }
        }
    }

    public bool IsRequired
    {
        get => _isRequired;
        set => SetProperty(ref _isRequired, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public string ToggleButtonText => IsEnabled ? "Disable" : "Enable";
    public bool CanToggle => !IsRequired && IsAvailable;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

