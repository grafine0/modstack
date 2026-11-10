using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ModStack.Common.Enums;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.Services;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for creating and editing packages with full MVVM support
/// </summary>
public partial class CreatePackageViewModel : INotifyPropertyChanged
{
    [GeneratedRegex(@"^[a-z0-9\-]+$")]
    private static partial Regex PackageNamePattern();

    private readonly PackageManagerService _packageManagerService;
    private readonly INavigationService _navigationService;
    private readonly IPackageRepository _packageRepository;
    private readonly ILogger<CreatePackageViewModel> _logger;
    private AvailablePackage? _editingPackage;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(ActionButtonIcon));
            }
        }
    }

    private bool _isSystemPackage;
    public bool IsSystemPackage
    {
        get => _isSystemPackage;
        set
        {
            if (SetProperty(ref _isSystemPackage, value))
            {
                OnPropertyChanged(nameof(IsFieldsReadOnly));
                OnPropertyChanged(nameof(CanDeletePackage));
            }
        }
    }

    /// <summary>
    /// Indicates if basic package fields should be read-only (system packages)
    /// </summary>
    public bool IsFieldsReadOnly => IsSystemPackage;

    /// <summary>
    /// Indicates if the package can be deleted (user packages only)
    /// </summary>
    public bool CanDeletePackage => !IsSystemPackage;

    private string _pageTitle = "Create New Package";
    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    private string _actionButtonText = "Create Package";
    public string ActionButtonText
    {
        get => _actionButtonText;
        set => SetProperty(ref _actionButtonText, value);
    }

    public string ActionButtonIcon
    {
        get
        {
            return IsEditMode ? "💾" : "➕";
        }
    }

    private string _packageName = string.Empty;
    public string PackageName
    {
        get => _packageName;
        set
        {
            if (SetProperty(ref _packageName, value))
            {
                ValidatePackageName();
            }
        }
    }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private string _category = "Other";
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    private PackageType _packageType = PackageType.Service;
    public PackageType PackageType
    {
        get => _packageType;
        set => SetProperty(ref _packageType, value);
    }

    private string _icon = "📦";
    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    private bool _isPopular;
    public bool IsPopular
    {
        get => _isPopular;
        set => SetProperty(ref _isPopular, value);
    }

    private string _tags = string.Empty;
    public string Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    private string _homepage = string.Empty;
    public string Homepage
    {
        get => _homepage;
        set => SetProperty(ref _homepage, value);
    }

    private string _documentation = string.Empty;
    public string Documentation
    {
        get => _documentation;
        set => SetProperty(ref _documentation, value);
    }

    private string _license = string.Empty;
    public string License
    {
        get => _license;
        set => SetProperty(ref _license, value);
    }

    private long _downloadSize;
    public long DownloadSize
    {
        get => _downloadSize;
        set => SetProperty(ref _downloadSize, value);
    }

    private int _defaultPort;
    public int DefaultPort
    {
        get => _defaultPort;
        set => SetProperty(ref _defaultPort, value);
    }

    private string _defaultInstallPath = string.Empty;
    public string DefaultInstallPath
    {
        get => _defaultInstallPath;
        set => SetProperty(ref _defaultInstallPath, value);
    }

    private bool _autoStart = true;
    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    private bool _registerAsService = true;
    public bool RegisterAsService
    {
        get => _registerAsService;
        set => SetProperty(ref _registerAsService, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    private bool _hasValidationError;
    public bool HasValidationError
    {
        get => _hasValidationError;
        set => SetProperty(ref _hasValidationError, value);
    }

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<PackageTypeItem> PackageTypes { get; }
    public ObservableCollection<PackageVersionViewModel> Versions { get; }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddVersionCommand { get; }
    public ICommand RemoveVersionCommand { get; }

    #endregion

    public CreatePackageViewModel(
        PackageManagerService packageManagerService,
        INavigationService navigationService,
        IPackageRepository packageRepository,
        ILogger<CreatePackageViewModel> logger)
    {
        _packageManagerService = packageManagerService ?? throw new ArgumentNullException(nameof(packageManagerService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Categories = new ObservableCollection<string>
        {
            "Web Server", "Database", "Runtime", "Framework",
            "Tool", "Other"
        };

        PackageTypes = new ObservableCollection<PackageTypeItem>
        {
            new() { Type = PackageType.Service, DisplayName = "Service" },
            new() { Type = PackageType.Framework, DisplayName = "Framework" },
            new() { Type = PackageType.StandaloneWebApp, DisplayName = "Standalone Web App" },
            new() { Type = PackageType.Tool, DisplayName = "Tool" }
        };

        Versions = new ObservableCollection<PackageVersionViewModel>();

        SaveCommand = new AsyncRelayCommand(SavePackageAsync, CanSavePackage);
        CancelCommand = new RelayCommand(Cancel);
        AddVersionCommand = new RelayCommand(AddVersion);
        RemoveVersionCommand = new RelayCommand<PackageVersionViewModel>(RemoveVersion);
    }

    public async Task InitializeForEditAsync(AvailablePackage package)
    {
        _editingPackage = package;
        IsEditMode = true;
        IsSystemPackage = package.IsSystemPackage;
        PageTitle = IsSystemPackage ? "View Package" : "Edit Package";
        ActionButtonText = "Save Package";
        OnPropertyChanged(nameof(ActionButtonIcon));

        PackageName = package.Name;
        DisplayName = package.DisplayName;
        Description = package.Description;
        Category = package.Category;
        PackageType = package.PackageType;
        Icon = package.Icon;
        IsPopular = package.IsPopular;
        Tags = string.Join(", ", package.Tags ?? new List<string>());
        Homepage = package.Homepage;
        Documentation = package.Documentation;
        License = package.License;
        DownloadSize = package.DownloadSize;

        // Load default configuration
        if (package.DefaultConfiguration != null)
        {
            DefaultPort = package.DefaultConfiguration.Port;
            DefaultInstallPath = package.DefaultConfiguration.InstallPath;
            AutoStart = package.DefaultConfiguration.AutoStart;
            RegisterAsService = package.DefaultConfiguration.RegisterAsService;
        }

        // Load versions
        Versions.Clear();
        if (package.AvailableVersions != null)
        {
            foreach (var version in package.AvailableVersions)
            {
                Versions.Add(new PackageVersionViewModel
                {
                    Version = version.Version,
                    DisplayName = version.DisplayName,
                    IsStable = version.IsStable,
                    IsRecommended = version.IsRecommended,
                    IsLTS = version.IsLTS,
                    DownloadUrl = version.DownloadUrls?.FirstOrDefault() ?? string.Empty,
                    ReleaseDate = version.ReleaseDate,
                    Size = version.Size,
                    Checksum = version.Checksum,
                    ReleaseNotes = version.ReleaseNotes,
                    IsSystemVersion = version.IsSystemVersion
                });
            }
        }

        await Task.CompletedTask;
    }

    private void ValidatePackageName()
    {
        HasValidationError = false;
        ValidationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(PackageName))
        {
            return;
        }

        if (!PackageNamePattern().IsMatch(PackageName))
        {
            HasValidationError = true;
            ValidationMessage = "Package name can only contain lowercase letters, numbers, and hyphens";
        }
    }

    private bool CanSavePackage()
    {
        return !string.IsNullOrWhiteSpace(PackageName) &&
               !string.IsNullOrWhiteSpace(DisplayName) &&
               !HasValidationError &&
               Versions.Count > 0;
    }

    private async Task SavePackageAsync()
    {
        try
        {
            // Validate input first (before setting loading)
            if (!CanSavePackage())
            {
                return;
            }

            // Set loading after validation
            IsLoading = true;
            
            // Update status bar
            MainWindow.UpdateStatusBar($"{(IsEditMode ? "Updating" : "Creating")} package '{DisplayName}'...");
            
            // Give UI time to update before async operation
            await Task.Delay(50);

            var package = new AvailablePackage
            {
                Name = PackageName.ToLowerInvariant(),
                DisplayName = DisplayName,
                Description = Description,
                Category = Category,
                PackageType = PackageType,
                Icon = Icon,
                IsPopular = IsPopular,
                IsSystemPackage = IsSystemPackage, // Preserve system package flag
                Tags = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList(),
                Homepage = Homepage,
                Documentation = Documentation,
                License = License,
                DownloadSize = DownloadSize,
                LastUpdated = DateTime.UtcNow,
                DefaultConfiguration = new PackageConfiguration
                {
                    Port = DefaultPort,
                    InstallPath = DefaultInstallPath,
                    AutoStart = AutoStart,
                    RegisterAsService = RegisterAsService
                },
                AvailableVersions = Versions.Select(v => new PackageVersion
                {
                    Version = v.Version,
                    DisplayName = v.DisplayName,
                    ServiceName = $"{PackageName.ToLowerInvariant()}-{v.Version}",
                    IsStable = v.IsStable,
                    IsRecommended = v.IsRecommended,
                    IsLTS = v.IsLTS,
                    DownloadUrls = new List<string> { v.DownloadUrl },
                    ReleaseDate = v.ReleaseDate,
                    Size = v.Size,
                    Checksum = v.Checksum,
                    ReleaseNotes = v.ReleaseNotes,
                    IsSystemVersion = v.IsSystemVersion // Preserve system version flag
                }).ToList()
            };

            if (IsEditMode && _editingPackage != null)
            {
                package.Id = _editingPackage.Id;
                await _packageRepository.UpdateAsync(package);
            }
            else
            {
                package = await _packageRepository.AddAsync(package);
            }
            
            // Reset status bar on success
            MainWindow.ResetStatusBar();
            
            // Show success toast notification
            if (IsEditMode)
            {
                NotificationService.Instance.ShowSuccess(
                    $"Package '{DisplayName}' updated successfully!",
                    "Package Updated");
            }
            else
            {
                NotificationService.Instance.ShowSuccess(
                    $"Package '{DisplayName}' created successfully!",
                    "Package Created");
            }
            
            // If creating a new package, navigate to edit mode
            if (!IsEditMode && package != null)
            {
                // Refresh the package from database to get all data including versions
                var savedPackage = await _packageRepository.GetByIdAsync(package.Id);
                if (savedPackage != null)
                {
                    // Navigate to edit view via MainWindow
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            mainWindow.EditPackageButton_Click(savedPackage);
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            // Show error toast notification
            NotificationService.Instance.ShowError(
                $"Failed to save package: {ex.Message}",
                IsEditMode ? "Update Failed" : "Create Failed");
        }
        finally
        {
            MainWindow.ResetStatusBar();
            IsLoading = false;
        }
    }

    private void Cancel()
    {
        // Navigate back to Package Manager
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.PackageManagerButton_Click(this, new RoutedEventArgs());
        }
    }

    private void AddVersion()
    {
        var newVersion = new PackageVersionViewModel
        {
            Version = "1.0.0",
            DisplayName = $"{DisplayName} 1.0.0",
            IsStable = true,
            IsRecommended = true,
            IsNewlyAdded = true // Used to trigger the highlight animation
        };
        
        Versions.Add(newVersion);
    }

    private void RemoveVersion(PackageVersionViewModel? version)
    {
        if (version != null)
        {
            Versions.Remove(version);
        }
    }

    protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for package version entry
/// </summary>
public class PackageVersionViewModel : INotifyPropertyChanged
{
    private string _version = string.Empty;
    private string _displayName = string.Empty;
    private bool _isStable = true;
    private bool _isRecommended;
    private bool _isLTS;
    private string _downloadUrl = string.Empty;
    private DateTime _releaseDate = DateTime.UtcNow;
    private long _size;
    private string _checksum = string.Empty;
    private string _releaseNotes = string.Empty;
    private bool _isNewlyAdded;
    private bool _isSystemVersion;

    public bool IsNewlyAdded
    {
        get => _isNewlyAdded;
        set
        {
            _isNewlyAdded = value;
            OnPropertyChanged(nameof(IsNewlyAdded));
        }
    }

    public bool IsSystemVersion
    {
        get => _isSystemVersion;
        set
        {
            _isSystemVersion = value;
            OnPropertyChanged(nameof(IsSystemVersion));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanDelete));
        }
    }

    /// <summary>
    /// Indicates if this version can be edited (not a system version)
    /// </summary>
    public bool CanEdit => !IsSystemVersion;

    /// <summary>
    /// Indicates if this version can be deleted (not a system version)
    /// </summary>
    public bool CanDelete => !IsSystemVersion;

    public string Version
    {
        get => _version;
        set
        {
            _version = value;
            OnPropertyChanged(nameof(Version));
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public bool IsStable
    {
        get => _isStable;
        set
        {
            _isStable = value;
            OnPropertyChanged(nameof(IsStable));
        }
    }

    public bool IsRecommended
    {
        get => _isRecommended;
        set
        {
            _isRecommended = value;
            OnPropertyChanged(nameof(IsRecommended));
        }
    }

    public bool IsLTS
    {
        get => _isLTS;
        set
        {
            _isLTS = value;
            OnPropertyChanged(nameof(IsLTS));
        }
    }

    public string DownloadUrl
    {
        get => _downloadUrl;
        set
        {
            _downloadUrl = value;
            OnPropertyChanged(nameof(DownloadUrl));
        }
    }

    public DateTime ReleaseDate
    {
        get => _releaseDate;
        set
        {
            _releaseDate = value;
            OnPropertyChanged(nameof(ReleaseDate));
        }
    }

    public long Size
    {
        get => _size;
        set
        {
            _size = value;
            OnPropertyChanged(nameof(Size));
        }
    }

    public string Checksum
    {
        get => _checksum;
        set
        {
            _checksum = value;
            OnPropertyChanged(nameof(Checksum));
        }
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        set
        {
            _releaseNotes = value;
            OnPropertyChanged(nameof(ReleaseNotes));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Package type display item
/// </summary>
public class PackageTypeItem
{
    public PackageType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

