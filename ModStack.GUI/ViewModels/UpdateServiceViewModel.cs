using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Common.Models;
using ModStack.Core.Interfaces;
using ModStack.Core.Services;
using ModStack.Data.Interfaces;

using ModStack.GUI.Services;
using ModStack.Services;
using ModStack.GUI.Views;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for Update Service view that combines service configuration,
/// PHP extensions management, and Apache modules management
/// </summary>
public class UpdateServiceViewModel : INotifyPropertyChanged
{
    #region Private Fields

    private readonly ServiceDisplayItem _service;
    private readonly IServiceInstallationChecker _serviceInstallationChecker;
    private readonly IServiceRepository _serviceRepository;
    private readonly IApacheModuleRepository? _apacheModuleRepository;
    private readonly ApacheModuleManager? _apacheModuleManager;
    private readonly ApacheConfigurationService? _apacheConfigService;
    private readonly IPHPExtensionRepository? _phpExtensionRepository;
    private readonly PHPExtensionManager? _phpExtensionManager;
    private readonly ModStack.Core.Services.IServiceService? _serviceService;
    private readonly ServiceControlManager? _serviceControlManager;
    private readonly IVirtualHostService? _virtualHostService;
    private readonly ILogger<UpdateServiceViewModel>? _logger;
    
    private ServiceConfiguration _configuration;
    private string _configurationJson = string.Empty;
    private bool _isLoading = false;
    private string _statusMessage = "Ready";
    private int _selectedTabIndex = 0;

    // PHP Configuration fields
    private bool _displayErrors = false;
    private bool _fileUploads = true;
    private bool _allowUrlFopen = true;
    private bool _allowUrlInclude = false;
    private string _memoryLimit = "256M";
    private string _maxExecutionTime = "300";
    private string _uploadMaxFilesize = "512M";
    private string _postMaxSize = "512M";
    private string _maxInputTime = "600";

    // PHP Extensions fields
    private string _phpExtensionSearchText = string.Empty;
    private string _phpExtensionCategoryFilter = "All Categories";
    private string _phpExtensionStatusFilter = "All Extensions";
    private bool _phpExtensionsLoading = false;
    private bool _phpExtensionsLoaded = false;
    private ObservableCollection<PHPExtensionViewModel> _phpExtensions = [];
    private ICollectionView? _filteredPhpExtensionsView;

    // Apache Modules fields
    private string _apacheModuleSearchText = string.Empty;
    private string _apacheModuleCategoryFilter = "All Categories";
    private string _apacheModuleStatusFilter = "All Modules";
    private bool _apacheModulesLoading = false;
    private bool _apacheModulesLoaded = false;
    private ObservableCollection<ApacheModuleViewModel> _apacheModules = [];
    private ICollectionView? _filteredApacheModulesView;
    private string _apacheVersion = string.Empty;
    
    // Pagination fields
    private int _apacheModuleCurrentPage = 1;
    private int _apacheModulePageSize = 20;
    private int _apacheModuleTotalPages = 1;
    private List<ApacheModuleViewModel>? _cachedFilteredModules;

    #endregion

    #region Public Properties

    public ServiceDisplayItem Service => _service;

    public ServiceConfiguration Configuration
    {
        get => _configuration;
        set
        {
            if (_configuration != null)
            {
                _configuration.PropertyChanged -= Configuration_PropertyChanged;
            }
            
            _configuration = value;
            
            if (_configuration != null)
            {
                _configuration.PropertyChanged += Configuration_PropertyChanged;
            }
            
            OnPropertyChanged();
            UpdateConfigurationJson();
        }
    }

    public string ConfigurationJson
    {
        get => _configurationJson;
        set
        {
            _configurationJson = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsAnyLoading));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Returns true if any loading operation is in progress (main loading, PHP extensions, or Apache modules)
    /// </summary>
    public bool IsAnyLoading => IsLoading || PhpExtensionsLoading || ApacheModulesLoading;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            var oldIndex = _selectedTabIndex;
            if (SetProperty(ref _selectedTabIndex, value))
            {
                _logger?.LogInformation("SelectedTabIndex changed from {OldIndex} to {NewIndex}", oldIndex, value);
                // Fire and forget - load tab content asynchronously on UI thread
                _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await OnTabChanged();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in SelectedTabIndex change handler");
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
        }
    }

    // Service type checks
    public bool IsPHPService => _service.Type == ServiceType.PHP;
    public bool IsApacheService => _service.Type == ServiceType.Apache;
    public bool ShowExtensionsTab => IsPHPService;
    public bool ShowModulesTab => IsApacheService;
    public bool ShowTabControl => ShowExtensionsTab || ShowModulesTab;

    // PHP Extensions Properties
    public ObservableCollection<PHPExtensionViewModel> PhpExtensions
    {
        get => _phpExtensions;
        private set
        {
            _phpExtensions = value;
            OnPropertyChanged();
            _filteredPhpExtensionsView = null;
            OnPropertyChanged(nameof(FilteredPhpExtensions));
        }
    }

    public ICollectionView FilteredPhpExtensions
    {
        get
        {
            if (_filteredPhpExtensionsView == null)
            {
                _filteredPhpExtensionsView = CollectionViewSource.GetDefaultView(PhpExtensions);
                _filteredPhpExtensionsView.Filter = FilterPhpExtensions;
            }
            return _filteredPhpExtensionsView;
        }
    }

    public string PhpExtensionSearchText
    {
        get => _phpExtensionSearchText;
        set
        {
            if (SetProperty(ref _phpExtensionSearchText, value))
            {
                FilteredPhpExtensions.Refresh();
                UpdatePhpExtensionsStatusMessage();
            }
        }
    }

    public string PhpExtensionCategoryFilter
    {
        get => _phpExtensionCategoryFilter;
        set
        {
            if (SetProperty(ref _phpExtensionCategoryFilter, value))
            {
                FilteredPhpExtensions.Refresh();
                UpdatePhpExtensionsStatusMessage();
            }
        }
    }

    public string PhpExtensionStatusFilter
    {
        get => _phpExtensionStatusFilter;
        set
        {
            if (SetProperty(ref _phpExtensionStatusFilter, value))
            {
                FilteredPhpExtensions.Refresh();
                UpdatePhpExtensionsStatusMessage();
            }
        }
    }

    public bool PhpExtensionsLoading
    {
        get => _phpExtensionsLoading;
        set
        {
            if (SetProperty(ref _phpExtensionsLoading, value))
            {
                OnPropertyChanged(nameof(IsAnyLoading));
            }
        }
    }

    public ObservableCollection<string> PhpExtensionCategories { get; } = [];
    public ObservableCollection<string> PhpExtensionStatusOptions { get; } = [
        "All Extensions", "Enabled", "Disabled", "Not Installed"
    ];

    // Apache Modules Properties
    public ObservableCollection<ApacheModuleViewModel> ApacheModules
    {
        get => _apacheModules;
        private set
        {
            _apacheModules = value;
            OnPropertyChanged();
            _filteredApacheModulesView = null;
            OnPropertyChanged(nameof(FilteredApacheModules));
        }
    }

    public ICollectionView FilteredApacheModules
    {
        get
        {
            if (_filteredApacheModulesView == null)
            {
                _filteredApacheModulesView = CollectionViewSource.GetDefaultView(ApacheModules);
                _filteredApacheModulesView.Filter = FilterApacheModules;
            }
            return _filteredApacheModulesView;
        }
    }
    
    // Pagination properties
    public int ApacheModuleCurrentPage
    {
        get => _apacheModuleCurrentPage;
        set
        {
            if (SetProperty(ref _apacheModuleCurrentPage, value))
            {
                UpdatePagination();
                OnPropertyChanged(nameof(ApacheModulePaginationInfo));
                OnPropertyChanged(nameof(CanGoToPreviousPage));
                OnPropertyChanged(nameof(CanGoToNextPage));
            }
        }
    }
    
    public int ApacheModulePageSize
    {
        get => _apacheModulePageSize;
        set
        {
            if (SetProperty(ref _apacheModulePageSize, value))
            {
                UpdatePagination();
                OnPropertyChanged(nameof(ApacheModulePaginationInfo));
            }
        }
    }
    
    public int ApacheModuleTotalPages
    {
        get => _apacheModuleTotalPages;
        private set
        {
            if (SetProperty(ref _apacheModuleTotalPages, value))
            {
                OnPropertyChanged(nameof(ApacheModulePaginationInfo));
                OnPropertyChanged(nameof(CanGoToPreviousPage));
                OnPropertyChanged(nameof(CanGoToNextPage));
            }
        }
    }
    
    public string ApacheModulePaginationInfo
    {
        get
        {
            var filteredCount = GetFilteredModuleCount();
            var start = filteredCount == 0 ? 0 : ((ApacheModuleCurrentPage - 1) * ApacheModulePageSize) + 1;
            var end = Math.Min(start + ApacheModulePageSize - 1, filteredCount);
            return $"Showing {start}-{end} of {filteredCount} modules (Page {ApacheModuleCurrentPage} of {ApacheModuleTotalPages})";
        }
    }
    
    public bool CanGoToPreviousPage => ApacheModuleCurrentPage > 1;
    public bool CanGoToNextPage => ApacheModuleCurrentPage < ApacheModuleTotalPages;

    public string ApacheModuleSearchText
    {
        get => _apacheModuleSearchText;
        set
        {
            if (SetProperty(ref _apacheModuleSearchText, value))
            {
                _cachedFilteredModules = null; // Clear cache
                ApacheModuleCurrentPage = 1; // Reset to first page on search
                UpdatePagination();
            }
        }
    }

    public string ApacheModuleCategoryFilter
    {
        get => _apacheModuleCategoryFilter;
        set
        {
            if (SetProperty(ref _apacheModuleCategoryFilter, value))
            {
                _cachedFilteredModules = null; // Clear cache
                ApacheModuleCurrentPage = 1; // Reset to first page on filter change
                UpdatePagination();
            }
        }
    }

    public string ApacheModuleStatusFilter
    {
        get => _apacheModuleStatusFilter;
        set
        {
            if (SetProperty(ref _apacheModuleStatusFilter, value))
            {
                _cachedFilteredModules = null; // Clear cache
                ApacheModuleCurrentPage = 1; // Reset to first page on filter change
                UpdatePagination();
            }
        }
    }

    public bool ApacheModulesLoading
    {
        get => _apacheModulesLoading;
        set
        {
            if (SetProperty(ref _apacheModulesLoading, value))
            {
                OnPropertyChanged(nameof(IsAnyLoading));
            }
        }
    }

    public string ApacheVersion
    {
        get => _apacheVersion;
        set => SetProperty(ref _apacheVersion, value);
    }

    public ObservableCollection<string> ApacheModuleCategories { get; } = [];
    public ObservableCollection<string> ApacheModuleStatusOptions { get; } = [
        "All Modules", "Enabled", "Disabled", "Available", "Required"
    ];

    // PHP Configuration Properties (only for PHP services)
    public bool DisplayErrors
    {
        get => _displayErrors;
        set => SetProperty(ref _displayErrors, value);
    }

    public bool FileUploads
    {
        get => _fileUploads;
        set => SetProperty(ref _fileUploads, value);
    }

    public bool AllowUrlFopen
    {
        get => _allowUrlFopen;
        set => SetProperty(ref _allowUrlFopen, value);
    }

    public bool AllowUrlInclude
    {
        get => _allowUrlInclude;
        set => SetProperty(ref _allowUrlInclude, value);
    }

    public string MemoryLimit
    {
        get => _memoryLimit;
        set => SetProperty(ref _memoryLimit, value);
    }

    public string MaxExecutionTime
    {
        get => _maxExecutionTime;
        set => SetProperty(ref _maxExecutionTime, value);
    }

    public string UploadMaxFilesize
    {
        get => _uploadMaxFilesize;
        set => SetProperty(ref _uploadMaxFilesize, value);
    }

    public string PostMaxSize
    {
        get => _postMaxSize;
        set => SetProperty(ref _postMaxSize, value);
    }

    public string MaxInputTime
    {
        get => _maxInputTime;
        set => SetProperty(ref _maxInputTime, value);
    }

    #endregion

    #region Commands

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenConfigurationFilesCommand { get; }
    public ICommand RefreshPhpExtensionsCommand { get; }
    public ICommand RefreshApacheModulesCommand { get; }
    public ICommand TogglePhpExtensionCommand { get; }
    public ICommand ToggleApacheModuleCommand { get; }
    public ICommand SynchronizeApacheModulesCommand { get; }
    public ICommand ApacheModulePreviousPageCommand { get; }
    public ICommand ApacheModuleNextPageCommand { get; }

    #endregion

    #region Events

    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Constructor

    public UpdateServiceViewModel(
        ServiceDisplayItem service,
        IServiceInstallationChecker serviceInstallationChecker,
        IServiceRepository serviceRepository,
        IApacheModuleRepository? apacheModuleRepository = null,
        ApacheModuleManager? apacheModuleManager = null,
        ApacheConfigurationService? apacheConfigService = null,
        IPHPExtensionRepository? phpExtensionRepository = null,
        PHPExtensionManager? phpExtensionManager = null,
        ModStack.Core.Services.IServiceService? serviceService = null,
        ServiceControlManager? serviceControlManager = null,
        IVirtualHostService? virtualHostService = null,
        ILogger<UpdateServiceViewModel>? logger = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _serviceInstallationChecker = serviceInstallationChecker ?? throw new ArgumentNullException(nameof(serviceInstallationChecker));
        _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        _apacheModuleRepository = apacheModuleRepository;
        _apacheModuleManager = apacheModuleManager;
        _apacheConfigService = apacheConfigService;
        _phpExtensionRepository = phpExtensionRepository;
        _phpExtensionManager = phpExtensionManager;
        _serviceService = serviceService;
        _serviceControlManager = serviceControlManager;
        _virtualHostService = virtualHostService;
        _logger = logger;

        // Initialize configuration
        _configuration = ServiceConfiguration.FromServiceDisplayItem(service);
        _configuration.PropertyChanged += Configuration_PropertyChanged;
        UpdateConfigurationJson();

        // Initialize commands
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsLoading);
        CancelCommand = new RelayCommand(Cancel);
        OpenConfigurationFilesCommand = new RelayCommand(OpenConfigurationFiles);
        RefreshPhpExtensionsCommand = new AsyncRelayCommand(LoadPhpExtensionsAsync, () => IsPHPService && !PhpExtensionsLoading);
        RefreshApacheModulesCommand = new AsyncRelayCommand(LoadApacheModulesAsync, () => IsApacheService && !ApacheModulesLoading);
        TogglePhpExtensionCommand = new AsyncRelayCommand<PHPExtensionViewModel>(TogglePhpExtensionAsync);
        ToggleApacheModuleCommand = new AsyncRelayCommand<ApacheModuleViewModel>(ToggleApacheModuleAsync);
        SynchronizeApacheModulesCommand = new AsyncRelayCommand(SynchronizeApacheModulesAsync, () => IsApacheService && !ApacheModulesLoading);
        ApacheModulePreviousPageCommand = new RelayCommand(() => ApacheModuleCurrentPage--, () => CanGoToPreviousPage);
        ApacheModuleNextPageCommand = new RelayCommand(() => ApacheModuleCurrentPage++, () => CanGoToNextPage);

        // Initialize PHP extension categories
        PhpExtensionCategories.Add("All Categories");

        // Initialize Apache module categories
        ApacheModuleCategories.Add("All Categories");

        // Load service-specific data
        _ = InitializeAsync();
    }

    #endregion

    #region Initialization

    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Loading service configuration for '{_service.Name}'..."));
            StatusMessage = "Loading service configuration...";

            // Ensure service has proper status
            if (string.Equals(_service.Status, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                _service.Status = _serviceInstallationChecker.GetServiceInstallationStatus(_service.Path, _service.Type);
            }

            // Get Apache version if Apache service (needed for module management)
            if (IsApacheService && _apacheConfigService != null)
            {
                ApacheVersion = _apacheConfigService.GetApacheVersion();
            }

            // Don't load extensions/modules here - load them lazily when tab is opened
            
            // Load PHP configuration from ExtendedProperties if available
            if (IsPHPService)
            {
                LoadPHPConfigurationFromExtendedProperties();
            }

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            StatusMessage = "Ready";
            
            // Check if we need to load a tab that was pre-selected
            // For PHP services: tab 1 is PHP Extensions
            // For Apache services: tab 2 is Apache Modules (tab 1 is hidden PHP Extensions tab)
            if ((IsPHPService && SelectedTabIndex == 1) || (IsApacheService && SelectedTabIndex == 2))
            {
                _ = OnTabChanged();
            }
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error initializing UpdateServiceViewModel for {ServiceName}", _service.Name);
            StatusMessage = $"Error: {ex.Message}";
            ModernDialogService.ShowError($"Error loading service configuration: {ex.Message}", "Initialization Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Configuration Management

    private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateConfigurationJson();
        Configuration.ApplyToServiceDisplayItem(_service);
    }

    private void UpdateConfigurationJson()
    {
        try
        {
            ConfigurationJson = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            ConfigurationJson = "{}";
            Debug.WriteLine($"❌ Error serializing configuration: {ex.Message}");
        }
    }

    public bool TryApplyJsonConfiguration(out List<string> errors)
    {
        errors = [];
        
        try
        {
            var parsedConfig = JsonSerializer.Deserialize<ServiceConfiguration>(ConfigurationJson);
            if (parsedConfig == null)
            {
                errors.Add("Invalid JSON configuration");
                return false;
            }

            var validationErrors = parsedConfig.Validate();
            if (validationErrors.Count > 0)
            {
                errors.AddRange(validationErrors);
                return false;
            }

            Configuration = parsedConfig;
            Configuration.ApplyToServiceDisplayItem(_service);
            
            Debug.WriteLine($"✅ Configuration applied successfully for {_service.Name}");
            return true;
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON format: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            errors.Add($"Error parsing configuration: {ex.Message}");
            return false;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Saving configuration for '{_service.Name}'..."));
            StatusMessage = "Saving configuration...";

            // Validate JSON configuration if it was modified
            if (!string.IsNullOrEmpty(ConfigurationJson))
            {
                if (!TryApplyJsonConfiguration(out var errors))
                {
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                    var errorMessage = string.Join("\n", errors);
                    ModernDialogService.ShowError($"Configuration validation failed:\n\n{errorMessage}", "Configuration Error");
                    StatusMessage = "Validation failed";
                    return;
                }
            }

            // Apply configuration to service
            Configuration.ApplyToServiceDisplayItem(_service);

            // Save to database
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var existingService = await _serviceRepository.GetByIdAsync(_service.Id);
                if (existingService == null)
                {
                    throw new InvalidOperationException($"Service with ID {_service.Id} not found in database");
                }

                existingService.DefaultPort = _service.Port;
                existingService.AutoStart = _service.AutoStart;
                existingService.BinaryPath = _service.Path;
                existingService.Version = _service.Version;

                // Save PHP configuration to ExtendedProperties if this is a PHP service
                string? previousConfiguration = null;
                if (IsPHPService)
                {
                    previousConfiguration = existingService.Configuration;
                    existingService.Configuration = SavePHPConfigurationToJson();
                    
                    // Check if PHP configuration has changed
                    bool configurationChanged = previousConfiguration != existingService.Configuration;
                    
                    // Update php.ini file if configuration has changed
                    if (configurationChanged)
                    {
                        await UpdatePhpIniFileAsync(existingService);
                        
                        // Sync FcgidMaxRequestLen in all vhosts using this PHP service
                        if (_virtualHostService != null)
                        {
                            await _virtualHostService.UpdateVhostFcgidMaxRequestLenForPhpServiceAsync(existingService);
                        }
                    }
                }

                await _serviceRepository.UpdateAsync(existingService);
            });

            // Restart service to apply configuration changes
            await RestartServiceAfterToggleAsync();

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            StatusMessage = "Configuration saved successfully";
            
            // Show success notification
            NotificationService.Instance.ShowSuccess(
                $"Configuration saved for '{_service.Name}'",
                "Configuration Updated");

            _logger?.LogInformation("Configuration saved for {ServiceName}", _service.Name);

            // Keep user on the same view - don't navigate away
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error saving configuration for {ServiceName}", _service.Name);
            StatusMessage = $"Error: {ex.Message}";
            ModernDialogService.ShowError($"Error saving configuration: {ex.Message}", "Save Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenConfigurationFiles()
    {
        try
        {
            var configPath = GetServiceConfigPath();
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                Debug.WriteLine($"📁 Opening config file: {configPath}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                });
            }
            else
            {
                var serviceDir = GetServiceDirectory();
                if (!string.IsNullOrEmpty(serviceDir) && Directory.Exists(serviceDir))
                {
                    Debug.WriteLine($"📁 Opening service directory: {serviceDir}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serviceDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ModernDialogService.ShowWarning($"Configuration directory not found for {_service.Name}.", "Configuration Files");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error opening configuration files: {ex.Message}");
            ModernDialogService.ShowError($"Error opening configuration files: {ex.Message}", "Configuration Error");
        }
    }

    private string GetServiceConfigPath()
    {
        var serviceDir = GetServiceDirectory();
        if (string.IsNullOrEmpty(serviceDir))
            return string.Empty;

        return _service.Type switch
        {
            ServiceType.Apache => GetApacheConfigPath(serviceDir),
            ServiceType.Nginx => GetNginxConfigPath(serviceDir),
            ServiceType.PHP => GetPhpConfigPath(serviceDir),
            ServiceType.MySQL => GetMySqlConfigPath(serviceDir),
            _ => string.Empty
        };
    }

    private string GetServiceDirectory()
    {
        if (string.IsNullOrEmpty(_service.Path))
            return string.Empty;

        if (File.Exists(_service.Path))
            return Path.GetDirectoryName(_service.Path) ?? string.Empty;

        if (Directory.Exists(_service.Path))
            return _service.Path;

        var parentDir = Path.GetDirectoryName(_service.Path);
        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            return parentDir;

        return string.Empty;
    }

    private string GetApacheConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "conf", "httpd.conf"),
            Path.Combine(serviceDir, "apache", "conf", "httpd.conf"),
            Path.Combine(serviceDir, "httpd.conf"),
            Path.Combine(serviceDir, "conf", "apache2.conf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetNginxConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "conf", "nginx.conf"),
            Path.Combine(serviceDir, "nginx.conf"),
            Path.Combine(serviceDir, "conf", "nginx", "nginx.conf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetPhpConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "php.ini"),
            Path.Combine(serviceDir, "conf", "php.ini"),
            Path.Combine(serviceDir, "etc", "php.ini")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetMySqlConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "my.ini"),
            Path.Combine(serviceDir, "my.cnf"),
            Path.Combine(serviceDir, "conf", "my.ini"),
            Path.Combine(serviceDir, "etc", "my.cnf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private async Task OnTabChanged()
    {
        try
        {
            _logger?.LogInformation("OnTabChanged called: SelectedTabIndex={TabIndex}, IsPHPService={IsPHP}, IsApacheService={IsApache}, ApacheVersion={ApacheVersion}", 
                SelectedTabIndex, IsPHPService, IsApacheService, ApacheVersion);
            
            // Load PHP Extensions tab (tab index 1 for PHP services)
            // Note: For PHP services, tab order is: 0=Configuration, 1=PHP Extensions
            if (IsPHPService && SelectedTabIndex == 1 && !_phpExtensionsLoaded && !PhpExtensionsLoading)
            {
                _logger?.LogInformation("Loading PHP extensions for tab index 1");
                await LoadPhpExtensionsAsync();
                _phpExtensionsLoaded = true;
            }
            // Load Apache Modules tab (tab index 2 for Apache services)
            // Note: For Apache services, tab order is: 0=Configuration, 1=PHP Extensions (hidden), 2=Apache Modules
            // Hidden tabs still count in TabControl indices!
            else if (IsApacheService && SelectedTabIndex == 2 && !_apacheModulesLoaded && !ApacheModulesLoading)
            {
                _logger?.LogInformation("Loading Apache modules for tab index 2 - Starting LoadApacheModulesAsync");
                
                // Verify services are available before loading
                if (_apacheModuleRepository == null)
                {
                    _logger?.LogError("ApacheModuleRepository is null - cannot load modules");
                    StatusMessage = "Error: Apache module repository not available";
                    return;
                }
                
                if (_apacheModuleManager == null)
                {
                    _logger?.LogError("ApacheModuleManager is null - cannot load modules");
                    StatusMessage = "Error: Apache module manager not available";
                    return;
                }
                
                await LoadApacheModulesAsync();
                _apacheModulesLoaded = true;
                _logger?.LogInformation("Apache modules loaded successfully. Count: {Count}", ApacheModules.Count);
            }
            else
            {
                _logger?.LogDebug("Tab change conditions not met: ExtensionsLoaded={ExtLoaded}, ModulesLoaded={ModLoaded}, ExtLoading={ExtLoading}, ModLoading={ModLoading}, SelectedTabIndex={TabIndex}",
                    _phpExtensionsLoaded, _apacheModulesLoaded, PhpExtensionsLoading, ApacheModulesLoading, SelectedTabIndex);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading tab content for {ServiceName}: {Exception}", _service.Name, ex);
            StatusMessage = $"Error loading tab content: {ex.Message}";
        }
    }

    #endregion

    #region PHP Extensions Management

    private async Task LoadPhpExtensionsAsync()
    {
        if (!IsPHPService || _serviceRepository == null || _phpExtensionRepository == null || _phpExtensionManager == null) return;

        try
        {
            PhpExtensionsLoading = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Loading PHP extensions for '{_service.Name}'..."));
            StatusMessage = "Loading PHP extensions...";

            // Get PHP service from repository
            var phpService = await _serviceRepository.GetByIdAsync(_service.Id);
            if (phpService == null)
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                StatusMessage = "PHP service not found";
                return;
            }

            // Normalize PHP version (e.g., 8.3.0 -> 8.3)
            var normalizedVersion = NormalizePHPVersion(_service.Version);

            // Initialize default extensions in database
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Initializing PHP extension database for '{_service.Name}'..."));
            StatusMessage = "Initializing extension database...";
            await _phpExtensionRepository.InitializeDefaultExtensionsAsync();

            // Synchronize with filesystem
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Synchronizing PHP extensions for '{_service.Name}'..."));
            StatusMessage = "Synchronizing with filesystem...";
            var syncResult = await _phpExtensionManager.SynchronizeExtensionsAsync(phpService.Id, normalizedVersion);
            if (!syncResult.Success)
            {
                _logger?.LogWarning("Extension synchronization warning: {Message}", syncResult.Message);
            }

            // Load available extensions
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Loading available PHP extensions for '{_service.Name}'..."));
            StatusMessage = "Loading available extensions...";
            var available = await _phpExtensionManager.GetAvailableExtensionsAsync(normalizedVersion);

            // Load installed extensions
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Loading installed PHP extensions for '{_service.Name}'..."));
            StatusMessage = "Loading installed extensions...";
            var installed = await _phpExtensionManager.GetInstalledExtensionsAsync(phpService.Id, normalizedVersion);

            // Create view models
            var extensions = new List<PHPExtensionViewModel>();
            foreach (var ext in available)
            {
                var installedExt = installed.FirstOrDefault(i => i.ExtensionId == ext.Id);
                var isActuallyInstalled = await _phpExtensionManager.IsExtensionActuallyInstalledAsync(phpService.Id, ext.Id);
                
                // Determine if extension is enabled
                bool isEnabled;
                if (installedExt != null)
                {
                    // Use database value
                    isEnabled = installedExt.IsEnabled;
                }
                else if (isActuallyInstalled)
                {
                    // Extension is installed but not in database - check php.ini directly
                    isEnabled = await _phpExtensionManager.IsExtensionEnabledInPhpIniAsync(phpService.Id, ext.Name, normalizedVersion);
                }
                else
                {
                    // Extension is not installed
                    isEnabled = false;
                }
                
                extensions.Add(new PHPExtensionViewModel(ext)
                {
                    IsInstalled = installedExt != null || isActuallyInstalled,
                    IsEnabled = isEnabled
                });
            }

            // Update UI
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PhpExtensions.Clear();
                foreach (var ext in extensions)
                {
                    PhpExtensions.Add(ext);
                }
                
                // Load categories
                var categories = extensions.Select(e => e.Category).Distinct().OrderBy(c => c).ToList();
                PhpExtensionCategories.Clear();
                PhpExtensionCategories.Add("All Categories");
                foreach (var category in categories)
                {
                    if (!string.IsNullOrEmpty(category) && !PhpExtensionCategories.Contains(category))
                    {
                        PhpExtensionCategories.Add(category);
                    }
                }
                
                // Set default selection to "All Categories"
                PhpExtensionCategoryFilter = "All Categories";
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            
            // Update status message after UI is updated
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdatePhpExtensionsStatusMessage();
            });
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error loading PHP extensions for {ServiceName}", _service.Name);
            StatusMessage = $"Error loading extensions: {ex.Message}";
            ModernDialogService.ShowError($"Error loading PHP extensions: {ex.Message}", "Extension Load Error");
        }
        finally
        {
            PhpExtensionsLoading = false;
        }
    }

    private string NormalizePHPVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return "8.3";
        
        // Normalize version format (8.3.0 -> 8.3)
        var parts = version.Split('.');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}.{parts[1]}";
        }
        return version;
    }

    private async Task TogglePhpExtensionAsync(PHPExtensionViewModel? extension)
    {
        if (extension == null || _phpExtensionManager == null || _serviceRepository == null) return;

        try
        {
            var wasEnabled = extension.IsEnabled;
            var wasInstalled = extension.IsInstalled;
            
            // Determine the operation type
            string operationType;
            if (!wasInstalled)
            {
                operationType = "Installing";
            }
            else if (wasEnabled)
            {
                operationType = "Disabling";
            }
            else
            {
                operationType = "Enabling";
            }
            
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"{operationType} PHP extension '{extension.Name}' for '{_service.Name}'..."));
            StatusMessage = $"{operationType} extension '{extension.Name}'...";
            
            var phpService = await _serviceRepository.GetByIdAsync(_service.Id);
            if (phpService == null)
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                ModernDialogService.ShowError("PHP service not found", "Extension Toggle Error");
                return;
            }

            var normalizedVersion = NormalizePHPVersion(_service.Version);
            
            // Use ToggleExtensionAsync - this will auto-install if extension exists in filesystem
            var result = await _phpExtensionManager.ToggleExtensionAsync(phpService.Id, extension.Id, !wasEnabled);

            if (result.Success)
            {
                // Restart PHP service to apply changes
                await RestartServiceAfterToggleAsync();
                
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                
                // Determine success message based on what happened
                string successMessage;
                if (!wasInstalled)
                {
                    // Extension was auto-installed
                    successMessage = $"Extension '{extension.Name}' installed and {(result.Message.Contains("enabled", StringComparison.OrdinalIgnoreCase) ? "enabled" : "disabled")} successfully";
                    NotificationService.Instance.ShowSuccess(
                        $"Extension '{extension.Name}' installed successfully",
                        "Extension Installed");
                }
                else
                {
                    // Extension was just enabled/disabled
                    successMessage = $"Extension '{extension.Name}' {(extension.IsEnabled ? "disabled" : "enabled")} successfully";
                    NotificationService.Instance.ShowSuccess(
                        $"Extension '{extension.Name}' {(extension.IsEnabled ? "disabled" : "enabled")}",
                        "Extension Updated");
                }
                StatusMessage = successMessage;
                
                // Reload extensions to get accurate state
                // This is important because ToggleExtensionAsync may have auto-installed the extension
                // and we need to ensure the UI reflects the correct state
                await LoadPhpExtensionsAsync();
            }
            else
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                string errorMessage = result.ErrorCode == "NOT_INSTALLED" 
                    ? $"Extension '{extension.Name}' is not installed and cannot be enabled. The extension files are not available in the PHP installation."
                    : $"Failed to {(wasInstalled ? (wasEnabled ? "disable" : "enable") : "install")} extension '{extension.Name}': {result.Message}";
                    
                ModernDialogService.ShowError(errorMessage, "Extension Toggle Error");
                StatusMessage = $"Failed to toggle extension '{extension.Name}'";
            }
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error toggling PHP extension {ExtensionName}", extension?.Name ?? "unknown");
            ModernDialogService.ShowError($"Error toggling extension: {ex.Message}", "Extension Toggle Error");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private bool FilterPhpExtensions(object obj)
    {
        if (obj is not PHPExtensionViewModel extension) return false;

        // Search filter
        if (!string.IsNullOrEmpty(PhpExtensionSearchText))
        {
            var searchLower = PhpExtensionSearchText.ToLowerInvariant();
            if (!extension.Name.ToLowerInvariant().Contains(searchLower) &&
                !extension.Description.ToLowerInvariant().Contains(searchLower))
            {
                return false;
            }
        }

        // Category filter
        if (PhpExtensionCategoryFilter != "All Categories" && extension.Category != PhpExtensionCategoryFilter)
        {
            return false;
        }

        // Status filter
        return PhpExtensionStatusFilter switch
        {
            "Enabled" => extension.IsEnabled && extension.IsInstalled,
            "Disabled" => !extension.IsEnabled && extension.IsInstalled,
            "Not Installed" => !extension.IsInstalled,
            _ => true
        };
    }

    private void UpdatePhpExtensionsStatusMessage()
    {
        if (!IsPHPService || PhpExtensions == null) return;

        try
        {
            var totalCount = PhpExtensions.Count;
            var installedCount = PhpExtensions.Count(e => e.IsInstalled);
            
            // Count filtered extensions - ensure FilteredPhpExtensions is available
            int filteredCount = 0;
            if (FilteredPhpExtensions != null)
            {
                FilteredPhpExtensions.Refresh();
                var filteredView = FilteredPhpExtensions.Cast<PHPExtensionViewModel>();
                filteredCount = filteredView.Count();
            }
            else
            {
                // If filter view is not available, use all extensions count
                filteredCount = totalCount;
            }
            
            // Build status message based on active filters
            var filterParts = new List<string>();
            
            var statusFilter = PhpExtensionStatusFilter ?? "All Extensions";
            if (statusFilter != "All Extensions")
            {
                filterParts.Add(statusFilter.ToLowerInvariant());
            }
            
            var categoryFilter = PhpExtensionCategoryFilter ?? "All Categories";
            if (categoryFilter != "All Categories")
            {
                filterParts.Add(categoryFilter.ToLowerInvariant());
            }
            
            var searchText = PhpExtensionSearchText ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                filterParts.Add($"matching '{searchText}'");
            }
            
            // If filters are active, show filtered count, otherwise show installed count
            string filterText = filterParts.Count > 0 
                ? $" ({filteredCount} {string.Join(", ", filterParts)})" 
                : $" ({installedCount} installed)";
            
            StatusMessage = $"Loaded {totalCount} PHP extensions{filterText}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating PHP extensions status message: {Exception}", ex);
            // Fallback to simple message
            var totalCount = PhpExtensions?.Count ?? 0;
            StatusMessage = $"Loaded {totalCount} PHP extensions";
        }
    }

    #endregion

    #region Apache Modules Management

    private async Task LoadApacheModulesAsync()
    {
        if (!IsApacheService || _apacheModuleRepository == null || _apacheModuleManager == null) return;

        try
        {
            ApacheModulesLoading = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Loading Apache modules for '{_service.Name}'..."));
            StatusMessage = "Loading Apache modules...";

            // Get all modules from database
            var modules = await _apacheModuleRepository.GetAllAsync();

            // If no modules found, try to initialize them
            if (!modules.Any())
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Initializing Apache modules for '{_service.Name}'..."));
                StatusMessage = "Initializing Apache modules...";
                
                var apachePath = _apacheModuleManager.GetApacheInstallationPath(ApacheVersion);
                if (!string.IsNullOrEmpty(apachePath))
                {
                    await _apacheModuleManager.InitializeDefaultModulesAsync(apachePath, ApacheVersion);
                    modules = await _apacheModuleRepository.GetAllAsync();
                    _logger?.LogInformation("Initialized {Count} modules from Apache installation", modules.Count());
                }
                else
                {
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                    StatusMessage = "Apache installation not found. Click Synchronize to set up modules.";
                    ApacheModulesLoading = false;
                    return;
                }
            }

            // Create view models
            var moduleViewModels = modules.Select(m => new ApacheModuleViewModel
            {
                Id = m.Id,
                Name = m.Name,
                ModuleName = m.ModuleName,
                FileName = m.FileName,
                Description = m.Description ?? "No description available",
                Category = m.Category ?? "Other",
                ApacheVersion = m.ApacheVersion ?? "Unknown",
                IsEnabled = m.IsEnabled,
                IsRequired = m.IsRequired,
                IsAvailable = m.IsAvailable
            }).ToList();

            // Update UI
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApacheModules.Clear();
                foreach (var module in moduleViewModels)
                {
                    ApacheModules.Add(module);
                }
                
                // Load categories
                LoadApacheCategories();
                
                // Initialize pagination
                UpdatePagination();
            });

            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            StatusMessage = $"Loaded {ApacheModules.Count} Apache modules";
            NotificationService.Instance.ShowSuccess(
                $"Loaded {ApacheModules.Count} Apache modules for '{_service.Name}'",
                "Modules Loaded");
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error loading Apache modules for {ServiceName}", _service.Name);
            StatusMessage = $"Error loading modules: {ex.Message}";
            ModernDialogService.ShowError($"Error loading Apache modules: {ex.Message}", "Module Load Error");
        }
        finally
        {
            ApacheModulesLoading = false;
        }
    }

    private void LoadApacheCategories()
    {
        if (_apacheModuleRepository == null) return;

        try
        {
            // Get categories from loaded modules
            var categories = ApacheModules.Select(m => m.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            
            ApacheModuleCategories.Clear();
            ApacheModuleCategories.Add("All Categories");
            
            foreach (var category in categories)
            {
                ApacheModuleCategories.Add(category);
            }
            
            // Set default selection to "All Categories"
            ApacheModuleCategoryFilter = "All Categories";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading Apache module categories");
        }
    }

    private async Task ToggleApacheModuleAsync(ApacheModuleViewModel? module)
    {
        if (module == null || _apacheModuleManager == null) return;

        try
        {
            var wasEnabled = module.IsEnabled;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"{(wasEnabled ? "Disabling" : "Enabling")} Apache module '{module.Name}' for '{_service.Name}'..."));
            StatusMessage = $"{(wasEnabled ? "Disabling" : "Enabling")} module '{module.Name}'...";

            // Get Apache installation path
            var apachePath = _apacheModuleManager.GetApacheInstallationPath(module.ApacheVersion);
            if (string.IsNullOrEmpty(apachePath))
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                ModernDialogService.ShowError("Apache installation not found. Please check your installation.", "Apache Not Found");
                return;
            }

            bool success;
            if (wasEnabled)
            {
                success = await _apacheModuleManager.DisableModuleAsync(module.Id, apachePath);
                if (success)
                {
                    module.IsEnabled = false;
                    StatusMessage = $"Disabled module: {module.Name}";
                }
            }
            else
            {
                success = await _apacheModuleManager.EnableModuleAsync(module.Id, apachePath);
                if (success)
                {
                    module.IsEnabled = true;
                    StatusMessage = $"Enabled module: {module.Name}";
                }
            }

            if (!success)
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                ModernDialogService.ShowError($"Failed to {(wasEnabled ? "disable" : "enable")} module: {module.Name}", "Module Operation Failed");
                StatusMessage = $"Failed to toggle module: {module.Name}";
            }
            else
            {
                // Restart Apache service to apply changes
                await RestartServiceAfterToggleAsync();
                
                // Refresh the display
                FilteredApacheModules.Refresh();
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                NotificationService.Instance.ShowSuccess(
                    $"Module '{module.Name}' {(module.IsEnabled ? "enabled" : "disabled")}",
                    "Module Updated");
            }
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error toggling Apache module {ModuleName}", module.Name);
            ModernDialogService.ShowError($"Error toggling module: {ex.Message}", "Error");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task SynchronizeApacheModulesAsync()
    {
        if (!IsApacheService || _apacheModuleManager == null) return;

        try
        {
            ApacheModulesLoading = true;
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Synchronizing Apache modules for '{_service.Name}'..."));
            StatusMessage = "Synchronizing Apache modules...";

            // Find Apache installation
            var apachePath = _apacheModuleManager.GetApacheInstallationPath(ApacheVersion);
            if (string.IsNullOrEmpty(apachePath))
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                ModernDialogService.ShowError("Apache installation not found. Please check your installation.", "Apache Not Found");
                return;
            }

            // First, initialize default modules if database is empty
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Initializing Apache modules for '{_service.Name}'..."));
            StatusMessage = "Initializing Apache modules...";
            await _apacheModuleManager.InitializeDefaultModulesAsync(apachePath, ApacheVersion);

            // Then synchronize with current state
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Synchronizing module states for '{_service.Name}'..."));
            StatusMessage = "Synchronizing module states...";
            var success = await _apacheModuleManager.SynchronizeModulesAsync(apachePath, ApacheVersion);
            
            if (success)
            {
                await LoadApacheModulesAsync();
                
                // Show success toast notification
                NotificationService.Instance.ShowSuccess(
                    "Apache modules have been synchronized successfully",
                    "Modules Synchronized");
                StatusMessage = "Modules synchronized successfully";
            }
            else
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
                ModernDialogService.ShowError("Failed to synchronize Apache modules. Check the logs for details.", "Synchronization Failed");
                StatusMessage = "Synchronization failed";
            }
        }
        catch (Exception ex)
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
            _logger?.LogError(ex, "Error synchronizing Apache modules for {ServiceName}", _service.Name);
            StatusMessage = $"Error: {ex.Message}";
            ModernDialogService.ShowError($"Error synchronizing modules: {ex.Message}", "Error");
        }
        finally
        {
            ApacheModulesLoading = false;
        }
    }

    private bool FilterApacheModules(object obj)
    {
        if (obj is not ApacheModuleViewModel module) return false;

        // First apply search/filter logic
        if (!FilterApacheModulesWithoutPagination(module))
        {
            return false;
        }
        
        // Then apply pagination - cache filtered list for performance
        if (_cachedFilteredModules == null)
        {
            _cachedFilteredModules = ApacheModules.Cast<ApacheModuleViewModel>()
                .Where(FilterApacheModulesWithoutPagination)
                .ToList();
        }
        
        var index = _cachedFilteredModules.IndexOf(module);
        if (index == -1) return false;
        
        var startIndex = (ApacheModuleCurrentPage - 1) * ApacheModulePageSize;
        var endIndex = startIndex + ApacheModulePageSize;
        
        return index >= startIndex && index < endIndex;
    }
    
    private bool FilterApacheModulesWithoutPagination(object obj)
    {
        if (obj is not ApacheModuleViewModel module) return false;

        // Search filter
        if (!string.IsNullOrEmpty(ApacheModuleSearchText))
        {
            var searchLower = ApacheModuleSearchText.ToLowerInvariant();
            var fileNameMatches = !string.IsNullOrEmpty(module.FileName) && 
                                 module.FileName.ToLowerInvariant().Contains(searchLower);
            
            if (!module.Name.ToLowerInvariant().Contains(searchLower) &&
                !module.Description.ToLowerInvariant().Contains(searchLower) &&
                !module.ModuleName.ToLowerInvariant().Contains(searchLower) &&
                !fileNameMatches)
            {
                return false;
            }
        }

        // Category filter
        if (ApacheModuleCategoryFilter != "All Categories" && module.Category != ApacheModuleCategoryFilter)
        {
            return false;
        }

        // Status filter
        return ApacheModuleStatusFilter switch
        {
            "Enabled" => module.IsEnabled,
            "Disabled" => !module.IsEnabled,
            "Available" => !module.IsRequired && !module.IsEnabled,
            "Required" => module.IsRequired,
            _ => true
        };
    }
    
    private int GetFilteredModuleCount()
    {
        return ApacheModules.Cast<ApacheModuleViewModel>()
            .Count(FilterApacheModulesWithoutPagination);
    }
    
    private void UpdatePagination()
    {
        // Clear cache to force recalculation
        _cachedFilteredModules = null;
        
        var filteredCount = GetFilteredModuleCount();
        ApacheModuleTotalPages = filteredCount == 0 ? 1 : (int)Math.Ceiling(filteredCount / (double)ApacheModulePageSize);
        
        // Ensure current page is valid
        if (ApacheModuleCurrentPage > ApacheModuleTotalPages && ApacheModuleTotalPages > 0)
        {
            ApacheModuleCurrentPage = ApacheModuleTotalPages;
        }
        else if (ApacheModuleCurrentPage < 1)
        {
            ApacheModuleCurrentPage = 1;
        }
        
        // Refresh the filtered view to apply pagination
        FilteredApacheModules.Refresh();
        
        // Update command states
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        OnPropertyChanged(nameof(ApacheModulePaginationInfo));
    }

    #endregion

    #region Navigation

    private void Cancel()
    {
        // Navigate back to Service Management
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            // The navigation should ideally be handled by INavigationService.
            // For now, we will send a message or just ignore if it's too complex without DI.
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Navigation requested"));
        }
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            Application.Current?.Dispatcher?.Invoke(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion

    #region Service Restart

    /// <summary>
    /// Restarts the service after toggling extensions or modules to apply changes
    /// </summary>
    private async Task RestartServiceAfterToggleAsync()
    {
        if (_serviceControlManager == null || _serviceService == null || _serviceRepository == null)
        {
            _logger?.LogWarning("ServiceControlManager or ServiceService not available, skipping service restart");
            return;
        }

        try
        {
            // Get fresh service data from database
            var serviceDefinition = await _serviceService.GetServiceByIdAsync(_service.Id);
            if (serviceDefinition == null)
            {
                _logger?.LogWarning("Service {ServiceId} not found in database, skipping restart", _service.Id);
                return;
            }

            // Convert to ServiceDisplayItem for ServiceControlManager
            var serviceDisplayItem = ConvertToServiceDisplayItem(serviceDefinition);

            // Check current status
            var currentStatus = await _serviceControlManager.DetectServiceStatusAsync(serviceDisplayItem);
            
            // Only restart if service is running
            if (string.Equals(currentStatus, "Running", StringComparison.OrdinalIgnoreCase))
            {
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Restarting {_service.Name} to apply changes..."));
                _logger?.LogInformation("Restarting service {ServiceName} to apply extension/module changes", _service.Name);

                // Stop the service
                await _serviceControlManager.StopServiceAsync(serviceDisplayItem);
                await Task.Delay(1000); // Wait for service to stop

                // Start the service
                await _serviceControlManager.StartServiceAsync(serviceDisplayItem);
                
                _logger?.LogInformation("Successfully restarted service {ServiceName}", _service.Name);
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Service '{_service.Name}' restarted successfully"));
            }
            else
            {
                _logger?.LogDebug("Service {ServiceName} is not running (status: {Status}), skipping restart", _service.Name, currentStatus);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error restarting service {ServiceName} after toggle", _service.Name);
            // Don't show error to user - the extension/module toggle was successful
            // Just log the restart failure
        }
    }

    /// <summary>
    /// Converts a ServiceDefinition to a ServiceDisplayItem for use with ServiceControlManager
    /// </summary>
    private static ServiceDisplayItem ConvertToServiceDisplayItem(ServiceDefinition service)
    {
        var serviceStatus = (ModStack.Common.ServiceStatus)service.Status;
        var statusString = serviceStatus == ModStack.Common.ServiceStatus.Unknown 
            ? "Stopped" 
            : serviceStatus.ToString();
        
        return new ServiceDisplayItem
        {
            Id = service.Id,
            Name = service.DisplayName ?? service.Name,
            Version = service.Version ?? string.Empty,
            Description = service.Description ?? string.Empty,
            Type = service.ServiceType,
            Status = statusString,
            Port = service.DefaultPort ?? 0,
            Path = service.BinaryPath ?? string.Empty,
            AutoStart = service.AutoStart
        };
    }

    #endregion

    #region PHP Configuration Management

    /// <summary>
    /// Loads PHP configuration from ExtendedProperties
    /// </summary>
    private void LoadPHPConfigurationFromExtendedProperties()
    {
        if (!IsPHPService || _serviceService == null) return;

        try
        {
            // Try to get service from database to access ExtendedProperties
            var serviceDefinition = _serviceService.GetServiceByIdAsync(_service.Id).GetAwaiter().GetResult();
            if (serviceDefinition?.Configuration == null) return;

            // Parse JSON configuration
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serviceDefinition.Configuration);
            if (configDict == null) return;

            // Load PHP settings
            if (configDict.TryGetValue("display_errors", out var displayErrors))
                DisplayErrors = displayErrors.ValueKind == JsonValueKind.True || (displayErrors.ValueKind == JsonValueKind.String && displayErrors.GetString()?.Equals("On", StringComparison.OrdinalIgnoreCase) == true);

            if (configDict.TryGetValue("file_uploads", out var fileUploads))
                FileUploads = fileUploads.ValueKind == JsonValueKind.True || (fileUploads.ValueKind == JsonValueKind.String && fileUploads.GetString()?.Equals("On", StringComparison.OrdinalIgnoreCase) == true);

            if (configDict.TryGetValue("allow_url_fopen", out var allowUrlFopen))
                AllowUrlFopen = allowUrlFopen.ValueKind == JsonValueKind.True || (allowUrlFopen.ValueKind == JsonValueKind.String && allowUrlFopen.GetString()?.Equals("On", StringComparison.OrdinalIgnoreCase) == true);

            if (configDict.TryGetValue("allow_url_include", out var allowUrlInclude))
                AllowUrlInclude = allowUrlInclude.ValueKind == JsonValueKind.True || (allowUrlInclude.ValueKind == JsonValueKind.String && allowUrlInclude.GetString()?.Equals("On", StringComparison.OrdinalIgnoreCase) == true);

            if (configDict.TryGetValue("memory_limit", out var memoryLimit))
                MemoryLimit = memoryLimit.ValueKind == JsonValueKind.String ? memoryLimit.GetString() ?? "256M" : "256M";

            if (configDict.TryGetValue("max_execution_time", out var maxExecutionTime))
                MaxExecutionTime = maxExecutionTime.ValueKind == JsonValueKind.String ? maxExecutionTime.GetString() ?? "300" : "300";

            if (configDict.TryGetValue("upload_max_filesize", out var uploadMaxFilesize))
                UploadMaxFilesize = uploadMaxFilesize.ValueKind == JsonValueKind.String ? uploadMaxFilesize.GetString() ?? "512M" : "512M";

            if (configDict.TryGetValue("post_max_size", out var postMaxSize))
                PostMaxSize = postMaxSize.ValueKind == JsonValueKind.String ? postMaxSize.GetString() ?? "512M" : "512M";

            if (configDict.TryGetValue("max_input_time", out var maxInputTime))
                MaxInputTime = maxInputTime.ValueKind == JsonValueKind.String ? maxInputTime.GetString() ?? "600" : "600";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading PHP configuration from ExtendedProperties");
            // Use default values if loading fails
        }
    }

    /// <summary>
    /// Saves PHP configuration to JSON string for ExtendedProperties
    /// </summary>
    private string SavePHPConfigurationToJson()
    {
        var configDict = new Dictionary<string, object>();

        configDict["display_errors"] = DisplayErrors ? "On" : "Off";
        configDict["file_uploads"] = FileUploads ? "On" : "Off";
        configDict["allow_url_fopen"] = AllowUrlFopen ? "On" : "Off";
        configDict["allow_url_include"] = AllowUrlInclude ? "On" : "Off";

        if (!string.IsNullOrEmpty(MemoryLimit))
            configDict["memory_limit"] = MemoryLimit;

        if (!string.IsNullOrEmpty(MaxExecutionTime))
            configDict["max_execution_time"] = MaxExecutionTime;

        if (!string.IsNullOrEmpty(UploadMaxFilesize))
            configDict["upload_max_filesize"] = UploadMaxFilesize;

        if (!string.IsNullOrEmpty(PostMaxSize))
            configDict["post_max_size"] = PostMaxSize;

        if (!string.IsNullOrEmpty(MaxInputTime))
            configDict["max_input_time"] = MaxInputTime;

        return JsonSerializer.Serialize(configDict, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Updates the php.ini file with the current PHP configuration settings
    /// </summary>
    private async Task UpdatePhpIniFileAsync(ServiceDefinition service)
    {
        if (_phpExtensionManager == null) return;

        try
        {
            _logger?.LogInformation("Updating php.ini file for service {ServiceName}", service.Name);
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage($"Updating php.ini configuration for '{service.Name}'..."));

            // Get php.ini path
            var phpIniPath = await _phpExtensionManager.GetPHPIniPathAsync(service.Id, service.Version ?? string.Empty);
            if (string.IsNullOrEmpty(phpIniPath) || !File.Exists(phpIniPath))
            {
                _logger?.LogWarning("php.ini file not found at {PhpIniPath}, skipping file update", phpIniPath);
                return;
            }

            // Read current php.ini content
            var content = await File.ReadAllTextAsync(phpIniPath);

            // Create backup before modifying
            var backupPath = $"{phpIniPath}.backup";
            if (File.Exists(phpIniPath))
            {
                File.Copy(phpIniPath, backupPath, overwrite: true);
                _logger?.LogDebug("Created backup of php.ini at {BackupPath}", backupPath);
            }

            // Update each PHP setting
            content = UpdatePHPSetting(content, "display_errors", DisplayErrors ? "On" : "Off");
            content = UpdatePHPSetting(content, "file_uploads", FileUploads ? "On" : "Off");
            content = UpdatePHPSetting(content, "allow_url_fopen", AllowUrlFopen ? "On" : "Off");
            content = UpdatePHPSetting(content, "allow_url_include", AllowUrlInclude ? "On" : "Off");
            
            if (!string.IsNullOrEmpty(MemoryLimit))
                content = UpdatePHPSetting(content, "memory_limit", MemoryLimit);
            
            if (!string.IsNullOrEmpty(MaxExecutionTime))
                content = UpdatePHPSetting(content, "max_execution_time", MaxExecutionTime);
            
            if (!string.IsNullOrEmpty(UploadMaxFilesize))
                content = UpdatePHPSetting(content, "upload_max_filesize", UploadMaxFilesize);
            
            if (!string.IsNullOrEmpty(PostMaxSize))
                content = UpdatePHPSetting(content, "post_max_size", PostMaxSize);
            
            if (!string.IsNullOrEmpty(MaxInputTime))
                content = UpdatePHPSetting(content, "max_input_time", MaxInputTime);

            // Write updated content back to file
            await File.WriteAllTextAsync(phpIniPath, content);
            
            _logger?.LogInformation("Successfully updated php.ini file for service {ServiceName}", service.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating php.ini file for service {ServiceName}", service.Name);
            // Don't throw - configuration was saved to database, just file update failed
            // User can manually edit the file if needed
        }
    }

    /// <summary>
    /// Updates a PHP setting in php.ini content
    /// </summary>
    private static string UpdatePHPSetting(string content, string setting, string value)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.None);
        var updated = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            // Match both uncommented and commented settings (we'll uncomment if commented)
            if (line.StartsWith($"{setting} =", StringComparison.OrdinalIgnoreCase) || 
                line.StartsWith($";{setting} =", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{setting} = {value}";
                updated = true;
                break;
            }
        }

        if (!updated)
        {
            // Add the setting at the end if it doesn't exist
            content = content.TrimEnd();
            if (!content.EndsWith("\n", StringComparison.Ordinal) && !string.IsNullOrEmpty(content))
                content += "\n";
            content += $"{setting} = {value}\n";
            return content;
        }

        return string.Join("\n", lines);
    }

    #endregion
}

