using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using ModStack.Core.Common;
using ModStack.Core.Extensions;
using ModStack.Core.Interfaces;
using ModStack.Data.Interfaces;
using ModStack.Common.Models;
using ModStack.Core.Services;

using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Views.Dialogs;
using ModStack.GUI.Helpers;
using ModStack.Services;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for Service Management functionality following MVVM pattern
/// </summary>
public class ServiceManagementViewModel : INotifyPropertyChanged
{
    #region Private Fields
    
    private readonly IServiceService _serviceService;
    private readonly PackageManagerService _packageManagerService;
    private readonly IServiceInstallationChecker _serviceInstallationChecker;
    private readonly ServiceControlManager _serviceControlManager;
    private readonly ILogger<ServiceManagementViewModel> _logger;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ModStack.Data.Interfaces.IServiceRepository _serviceRepository;
    
    private string _searchText = string.Empty;
    private string _selectedServiceType = "All Types";
    private string _selectedStatus = "All Status";
    private string _statusText = "Loading services...";
    private string _serviceCountText = "0 services";
    private bool _isLoading = false;
    
    private readonly List<ServiceDisplayItem> _allServices = [];

    #endregion

    #region Public Properties

    public ObservableCollection<ServiceDisplayItem> Services { get; }
    
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = Task.Run(FilterServicesAsync);
            }
        }
    }
    
    public string SelectedServiceType
    {
        get => _selectedServiceType;
        set
        {
            if (SetProperty(ref _selectedServiceType, value))
            {
                _ = Task.Run(FilterServicesAsync);
            }
        }
    }
    
    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _ = Task.Run(FilterServicesAsync);
            }
        }
    }
    
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
    
    public string ServiceCountText
    {
        get => _serviceCountText;
        set => SetProperty(ref _serviceCountText, value);
    }
    
    public bool IsLoading
    {
        get => _isLoading;
        set 
        {
            if (SetProperty(ref _isLoading, value))
            {
                // Notify commands that their CanExecute state might have changed
                NotifyCommandsCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<string> ServiceTypes { get; }
    public ObservableCollection<string> StatusOptions { get; }

    #endregion

    #region Commands

    public ICommand StartServiceCommand { get; }
    public ICommand StopServiceCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand DeleteServiceCommand { get; }
    public ICommand ConfigureServiceCommand { get; }
    public ICommand ManageExtensionsCommand { get; }
    public ICommand ManageApacheModulesCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand AutoStartToggleCommand { get; }
    public ICommand InstallServiceCommand { get; }

    #endregion

    #region Constructor

    public ServiceManagementViewModel(
        IServiceService serviceService,
        PackageManagerService packageManagerService,
        IServiceInstallationChecker serviceInstallationChecker,
        ServiceControlManager serviceControlManager,
        ILogger<ServiceManagementViewModel> logger,
        INavigationService navigationService,
        IDialogService dialogService,
        ModStack.Data.Interfaces.IServiceRepository serviceRepository)
    {
        _serviceService = serviceService ?? throw new ArgumentNullException(nameof(serviceService));
        _packageManagerService = packageManagerService ?? throw new ArgumentNullException(nameof(packageManagerService));
        _serviceInstallationChecker = serviceInstallationChecker ?? throw new ArgumentNullException(nameof(serviceInstallationChecker));
        _serviceControlManager = serviceControlManager ?? throw new ArgumentNullException(nameof(serviceControlManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));

        Services = [];
        ServiceTypes = ["All Types"];
        StatusOptions = ["All Status", "Running", "Stopped", "Not Installed"];

        // Initialize commands
        StartServiceCommand = new AsyncRelayCommand<ServiceDisplayItem>(StartServiceAsync, CanStartService);
        StopServiceCommand = new AsyncRelayCommand<ServiceDisplayItem>(StopServiceAsync, CanStopService);
        RefreshCommand = new AsyncRelayCommand(RefreshServicesAsync);
        DeleteServiceCommand = new AsyncRelayCommand<ServiceDisplayItem>(DeleteServiceAsync, CanDeleteService);
        ConfigureServiceCommand = new AsyncRelayCommand<ServiceDisplayItem>(ConfigureServiceAsync, CanConfigureService);
        ManageExtensionsCommand = new RelayCommand<ServiceDisplayItem>(ManageExtensions, CanManageExtensions);
        ManageApacheModulesCommand = new RelayCommand<ServiceDisplayItem>(ManageApacheModules, CanManageApacheModules);
        ClearFiltersCommand = new AsyncRelayCommand(ClearFiltersAsync);
        AutoStartToggleCommand = new AsyncRelayCommand<ServiceDisplayItem>(ToggleAutoStartAsync);
        InstallServiceCommand = new AsyncRelayCommand(InstallServiceAsync);

        // Load services when ViewModel is created
        _ = Task.Run(LoadServicesAsync);
        
        // Start status refresh timer
        StartStatusRefreshTimer();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refresh services after installation
    /// </summary>
    public async Task RefreshAfterInstallationAsync()
    {
        try
        {
            _logger.LogDebug("Triggering service detection after installation");
            await _serviceService.DetectAndUpdateInstallationStatusAsync();
            await _serviceService.DetectAndUpdateServiceStatusAsync();
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh services after installation");
            StatusText = "Failed to refresh services after installation";
        }
    }

    /// <summary>
    /// Update progress display
    /// </summary>
    public void UpdateProgress(string status, int percentage)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            StatusText = $"{status} ({percentage}%)";
        });
    }

    #endregion

    #region Private Methods - Service Loading

    private async Task LoadServicesAsync()
    {
        try
        {
            IsLoading = true;
            StatusText = "Loading services...";
            
            _logger.LogDebug("Starting to load services");
            
            // Get only installed services
            var serviceDisplayItems = await _serviceService.GetInstalledServicesAsync();
            
            _logger.LogDebug("Retrieved {ServiceCount} installed services from database", serviceDisplayItems.Count());
            
            // Debug: Log each service that was retrieved
            foreach (var service in serviceDisplayItems)
            {
                _logger.LogDebug("Service: {ServiceName} ({ServiceType}) - Status: {Status}", 
                    service.Name, service.Type, service.Status);
            }
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _allServices.Clear();
                Services.Clear();
                
                foreach (var service in serviceDisplayItems)
                {
                    _logger.LogTrace("Adding service: {ServiceName} ({ServiceType})", service.Name, service.Type);
                    
                    // Set action delegates for service operations
                    service.StartServiceAction = StartServiceAsync;
                    service.StopServiceAction = StopServiceAsync;
                    service.DeleteServiceAction = DeleteServiceAsync;
                    service.ConfigureServiceAction = ConfigureServiceAsync;
                    service.AutoStartToggleAction = ToggleAutoStartAsync;
                    
                    _allServices.Add(service);
                    Services.Add(service);
                }
                
                _logger.LogDebug("Added {ServiceCount} services to UI", Services.Count);
                
                UpdateStatusBar();
                UpdateServiceTypeFilter();
            });
            
            // Immediately check status of all services
            _logger.LogDebug("Performing immediate status check for all services");
            await RefreshServiceStatusesAsync();
            _logger.LogDebug("Initial status check completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load services");
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = $"Failed to load services: {ex.Message}";
                ModernDialogService.ShowError($"Failed to load services: {ex.Message}", "Service Load Error");
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshServiceStatusesAsync()
    {
        if (!Services.Any())
            return;

        var servicesToCheck = Services.ToList(); // Create snapshot to avoid collection modification
        
        try
        {
            // Check all services in parallel
            var tasks = servicesToCheck.Where(service => service != null).Select(async service =>
            {
                try
                {
                    var status = await _serviceControlManager.DetectServiceStatusAsync(service);
                    return (service, status);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check status for {ServiceName}", service?.Name ?? "Unknown");
                    return (service, "Error");
                }
            });

            var results = await Task.WhenAll(tasks);

            // Batch update UI on main thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var (service, status) in results)
                {
                    // Skip null services (may have been removed from collection)
                    if (service == null)
                    {
                        _logger.LogWarning("Skipping status update for null service");
                        continue;
                    }
                    
                    // Only update if service is still in the collection
                    if (!Services.Contains(service))
                    {
                        _logger.LogTrace("Skipping status update for service {ServiceName} - no longer in collection", service.Name);
                        continue;
                    }
                    
                    // Skip status updates for services that are currently busy (stopping/starting)
                    // This prevents race conditions where status refresh timer overwrites status during operations
                    if (service.IsBusy)
                    {
                        _logger.LogTrace("Skipping status update for {ServiceName} - service is busy", service.Name);
                        continue;
                    }
                    
                    // Skip status updates if service is currently showing "Stopping" status
                    // This prevents the refresh timer from overwriting the stopping state
                    if (service.Status == ServiceStatus.Stopping.ToString())
                    {
                        _logger.LogTrace("Skipping status update for {ServiceName} - service is stopping", service.Name);
                        continue;
                    }
                    
                    var oldStatus = service.Status;
                    service.Status = status;
                    _logger.LogTrace("Status updated for {ServiceName}: {OldStatus} → {NewStatus}", 
                        service.Name, oldStatus, status);
                }
                
                UpdateStatusBar();
                
                // Notify commands that their CanExecute state might have changed due to status updates
                NotifyCommandsCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh service statuses");
        }
    }

    #endregion

    #region Private Methods - Filtering

    private async Task FilterServicesAsync()
    {
        try
        {
            if (_allServices == null)
            {
                _logger.LogDebug("_allServices is null, skipping filter");
                return;
            }

            var filteredServices = _allServices.AsEnumerable();

            // Apply search filter
            if (!string.IsNullOrEmpty(SearchText))
            {
                filteredServices = filteredServices.Where(s => 
                    (s.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Version?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Path?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            // Apply type filter
            if (!string.IsNullOrEmpty(SelectedServiceType) && SelectedServiceType != "All Types")
            {
                filteredServices = filteredServices.Where(s => s.Type.ToString() == SelectedServiceType);
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "All Status")
            {
                filteredServices = filteredServices.Where(s => s.Status == SelectedStatus);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Services.Clear();
                foreach (var service in filteredServices)
                {
                    Services.Add(service);
                }
                
                UpdateStatusBar();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering services");
        }
    }

    private async Task ClearFiltersAsync()
    {
        try
        {
            SearchText = string.Empty;
            SelectedServiceType = "All Types";
            SelectedStatus = "All Status";
            
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing filters");
            ModernDialogService.ShowError($"Error clearing filters: {ex.Message}", "Filter Error");
        }
    }

    #endregion

    #region Private Methods - Service Operations

    private async Task StartServiceAsync(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            service.IsBusy = true;
            NotifyCommandsCanExecuteChanged();

            // Update main window status bar
            MainWindow.UpdateStatusBar($"Starting service '{service.Name}'...");

            // Check for port conflicts before starting
            ModStack.Common.Services.LogService.Info($"Checking port conflict for {service.Name} on port {service.Port}");
            var conflictingService = await ServiceControlManager.CheckPortConflictAsync(service, Services);
            if (conflictingService != null)
            {
                MainWindow.ResetStatusBar();
                ModStack.Common.Services.LogService.Warning($"Port conflict detected before start: {service.Name} on port {service.Port} (used by {conflictingService.Name})");
                var message = CreatePortConflictMessage(service, conflictingService);
                ModernDialogService.ShowError(message, "Port Conflict");
                return;
            }
            ModStack.Common.Services.LogService.Info($"No port conflict detected for {service.Name} on port {service.Port}");

            // Update status to show starting
            service.Status = ServiceStatus.Starting.ToString();
            
            ModStack.Common.Services.LogService.Info($"Starting service: {service.Name}");
            await _serviceControlManager.StartServiceAsync(service);
            
            // Check status immediately after starting
            var newStatus = await _serviceControlManager.DetectServiceStatusAsync(service);
            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                service.Status = newStatus;
                UpdateStatusBar();
            });
            
            MainWindow.ResetStatusBar();
            ModStack.Common.Services.LogService.Info($"Service {service.Name} started successfully");
            
            // Show success toast notification
            ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(
                $"Service '{service.Name}' started successfully",
                "Service Started");
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Failed to start service {ServiceName}", service.Name);
            
            // Check for port conflicts after failed start attempt
            ModStack.Common.Services.LogService.Info($"Checking for port conflicts after failed start attempt for {service.Name}");
            var conflictingService = await ServiceControlManager.CheckPortConflictAsync(service, Services);
            if (conflictingService != null)
            {
                ModStack.Common.Services.LogService.Warning($"Port conflict detected after failed start: {service.Name} on port {service.Port} (used by {conflictingService.Name})");
                var message = CreatePortConflictMessage(service, conflictingService);
                ModernDialogService.ShowError(message, "Port Conflict");
            }
            else
            {
                // Create a more user-friendly error message for non-port-conflict errors
                var userMessage = CreateUserFriendlyErrorMessage(service, ex);
                ModernDialogService.ShowError(userMessage, "Service Start Error");
            }
            
            // Reset status on failure
            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                service.Status = ServiceStatus.Stopped.ToString();
                UpdateStatusBar();
            });
        }
        finally
        {
            service.IsBusy = false;
            NotifyCommandsCanExecuteChanged();
        }
    }

    private async Task StopServiceAsync(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            service.IsBusy = true;
            NotifyCommandsCanExecuteChanged();

            // Update main window status bar
            MainWindow.UpdateStatusBar($"Stopping service '{service.Name}'...");

            // Update status to show stopping
            service.Status = ServiceStatus.Stopping.ToString();
            
            _logger.LogInformation("Stopping service: {ServiceName}", service.Name);
            await _serviceControlManager.StopServiceAsync(service);
            
            // Clear ProcessIds since we stopped the service
            service.ProcessIds = string.Empty;
            
            // Wait a moment for processes to fully terminate before checking status
            await Task.Delay(500, CancellationToken.None);
            
            // Set status directly to Stopped after successful stop
            // Don't re-detect immediately to avoid race conditions with status refresh timer
            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                service.Status = ServiceStatus.Stopped.ToString();
                UpdateStatusBar();
            });
            
            MainWindow.ResetStatusBar();
            _logger.LogInformation("Service {ServiceName} stopped successfully", service.Name);
            
            // Show success toast notification
            ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(
                $"Service '{service.Name}' stopped successfully",
                "Service Stopped");
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Failed to stop service {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Failed to stop service '{service.Name}':\n\n{ex.Message}", "Service Stop Error");
            
            // Re-detect status on failure to get accurate state
            try
            {
                var actualStatus = await _serviceControlManager.DetectServiceStatusAsync(service);
                await Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    service.Status = actualStatus;
                    UpdateStatusBar();
                });
            }
            catch (Exception detectEx)
            {
                _logger.LogError(detectEx, "Failed to detect status after stop failure for {ServiceName}", service.Name);
                // Fallback to Running if detection fails
                await Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    service.Status = ServiceStatus.Running.ToString();
                    UpdateStatusBar();
                });
            }
        }
        finally
        {
            service.IsBusy = false;
            NotifyCommandsCanExecuteChanged();
        }
    }

    private async Task DeleteServiceAsync(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            // Show confirmation dialog
            var result = ModernDialogService.ShowServiceDeletionConfirmation(service);
            
            if (!result.Confirmed) return;
            
            _logger.LogInformation("Deleting service: {ServiceName} (Delete files: {DeleteFiles})", 
                service.Name, result.DeleteFiles);
            
            // Step 1: Stop the service if it's running
            if (service.Status == "Running")
            {
                _logger.LogDebug("Stopping running service: {ServiceName}", service.Name);
                await _serviceControlManager.StopServiceAsync(service);
                await Task.Delay(1000); // Give it time to stop
            }
            
            // Step 2: Delete service files if requested
            if (result.DeleteFiles && !string.IsNullOrEmpty(service.Path) && Directory.Exists(service.Path))
            {
                _logger.LogDebug("Deleting service files: {ServicePath}", service.Path);
                Directory.Delete(service.Path, recursive: true);
                _logger.LogDebug("Service files deleted: {ServicePath}", service.Path);
            }
            
            // Step 3: Remove from database
            
            _logger.LogDebug("Removing service from database: {ServiceName} (ID: {ServiceId})", 
                service.Name, service.Id);
            bool deleted = await _serviceRepository.DeleteAsync(service.Id);
            
            if (!deleted)
            {
                throw new InvalidOperationException($"Failed to delete service '{service.Name}' from database");
            }
            
            // Remove from UI collection
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Services.Remove(service);
                _allServices.Remove(service);
                UpdateStatusBar();
            });
            
            // Show success toast notification
            var message = result.DeleteFiles 
                ? $"Service '{service.Name}' and its files deleted successfully"
                : $"Service '{service.Name}' removed (files preserved)";
            ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(message, "Service Deleted");
            
            _logger.LogInformation("Service {ServiceName} deleted successfully (Files deleted: {FilesDeleted})", 
                service.Name, result.DeleteFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete service {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Failed to delete service '{service.Name}':\n\n{ex.Message}", "Delete Error");
        }
    }

    private async Task ConfigureServiceAsync(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            // Navigate to UpdateServiceView
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                    var updateView = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<Views.UpdateServiceView>(serviceProvider, service);
                    mainWindow.SetCurrentView(updateView);
                    
                    // Don't set active navigation button - keep Service Management enabled
                    mainWindow.EnableAllNavigationButtons();
                }
            });
            
            MainWindow.ResetStatusBar();
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Failed to open service configuration for {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Failed to open service configuration: {ex.Message}", "Configuration Error");
        }
    }

    private async Task ToggleAutoStartAsync(ServiceDisplayItem? service)
    {
        if (service == null) 
        {
            System.Diagnostics.Debug.WriteLine("🔔 ToggleAutoStartAsync: service is null");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"🔔 ToggleAutoStartAsync called for {service.Name}: {service.AutoStart}");
            _logger.LogDebug("AutoStart toggled for {ServiceName}: {NewValue}", service.Name, service.AutoStart);
            
            // Save to database
            await SaveServiceConfigurationAsync(service);
            
            System.Diagnostics.Debug.WriteLine($"🔔 AutoStart setting saved for {service.Name}: {service.AutoStart}");
            _logger.LogDebug("AutoStart setting saved for {ServiceName}: {Value}", service.Name, service.AutoStart);
            
            // Show success toast notification
            var statusText = service.AutoStart ? "enabled" : "disabled";
            ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(
                $"Auto-start {statusText} for '{service.Name}'",
                "Auto-Start Updated");
            
            // Refresh services to update UI with latest database values
            System.Diagnostics.Debug.WriteLine($"🔔 Refreshing services after AutoStart update for {service.Name}");
            await LoadServicesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"🔔 Error saving AutoStart for {service.Name}: {ex.Message}");
            _logger.LogError(ex, "Failed to save AutoStart setting for {ServiceName}", service.Name);
            
            // Revert the UI change on error
            service.AutoStart = !service.AutoStart;
            
            ModernDialogService.ShowError($"Failed to save AutoStart setting: {ex.Message}", "Configuration Error");
        }
    }

    private async Task RefreshServicesAsync()
    {
        try
        {
            await RefreshAfterInstallationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing services");
            ModernDialogService.ShowError($"Failed to refresh services: {ex.Message}", "Refresh Error");
        }
    }

    #endregion

    #region Private Methods - UI Management

    private void ManageExtensions(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            if (service.Type == ModStack.Common.ServiceType.PHP)
            {
                _logger.LogDebug("Opening UpdateServiceView for PHP extensions: {ServiceName} v{ServiceVersion}", 
                    service.Name, service.Version);
                
                // Navigate to UpdateServiceView
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                        var updateView = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<Views.UpdateServiceView>(serviceProvider, service);
                        
                        // Switch to PHP Extensions tab
                        if (updateView.ViewModel != null)
                        {
                            updateView.ViewModel.SelectedTabIndex = 1; // PHP Extensions tab
                        }
                        
                        mainWindow.SetCurrentView(updateView);
                        
                        // Don't set active navigation button - keep Service Management enabled
                        mainWindow.EnableAllNavigationButtons();
                    }
                });
            }
            else
            {
                ModernDialogService.ShowInformation("Extension management is only available for PHP services.", "Feature Info");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening extension manager for {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Error opening extension manager: {ex.Message}", "Extension Manager Error");
        }
    }

    private void ManageApacheModules(ServiceDisplayItem? service)
    {
        if (service == null) return;
        
        try
        {
            if (service.Type == ModStack.Common.ServiceType.Apache)
            {
                _logger.LogDebug("Opening UpdateServiceView for Apache modules: {ServiceName}", service.Name);
                
                // Navigate to UpdateServiceView
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                        var updateView = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<Views.UpdateServiceView>(serviceProvider, service);
                        
                        // Switch to Apache Modules tab
                        if (updateView.ViewModel != null)
                        {
                            // Apache Modules tab index depends on service type
                            // For Apache services: Configuration (0), Apache Modules (1)
                            updateView.ViewModel.SelectedTabIndex = 1; // Apache Modules tab
                        }
                        
                        mainWindow.SetCurrentView(updateView);
                        
                        // Don't set active navigation button - keep Service Management enabled
                        mainWindow.EnableAllNavigationButtons();
                    }
                });
            }
            else
            {
                ModernDialogService.ShowInformation("Module management is only available for Apache services.", "Feature Info");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening Apache module manager for {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Error opening Apache module manager: {ex.Message}", "Module Manager Error");
        }
    }

    private void UpdateStatusBar()
    {
        try
        {
            var totalServices = Services?.Count ?? 0;
            var runningServices = Services?.Count(s => s.Status == "Running") ?? 0;
            var stoppedServices = Services?.Count(s => s.Status == "Stopped") ?? 0;
            var notInstalledServices = Services?.Count(s => s.Status == "Not Installed") ?? 0;
            var unknownServices = Services?.Count(s => s.Status == "Unknown") ?? 0;

            ServiceCountText = $"{totalServices} services";
            
            // Update status text with more detailed information
            if (totalServices > 0)
            {
                if (notInstalledServices > 0)
                {
                    StatusText = $"Running: {runningServices} | Stopped: {stoppedServices} | Not Installed: {notInstalledServices}";
                }
                else if (unknownServices > 0)
                {
                    StatusText = $"Running: {runningServices} | Stopped: {stoppedServices} | Unknown: {unknownServices}";
                }
                else
                {
                    StatusText = $"Running: {runningServices} | Stopped: {stoppedServices}";
                }
            }
            else
            {
                StatusText = "No services available";
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error updating status";
            _logger.LogError(ex, "Error updating status bar");
        }
    }

    private void UpdateServiceTypeFilter()
    {
        try
        {
            // Get unique service types from loaded services
            var serviceTypes = _allServices?
                .Select(s => s.Type.ToString())
                .Distinct()
                .OrderBy(t => t)
                .ToList() ?? [];

            // Store current selection
            var currentSelection = SelectedServiceType;

            // Clear existing items and add "All Types"
            ServiceTypes.Clear();
            ServiceTypes.Add("All Types");

            // Add actual service types
            foreach (var serviceType in serviceTypes)
            {
                ServiceTypes.Add(serviceType);
            }

            // Restore selection if it still exists, otherwise select "All Types"
            if (!string.IsNullOrEmpty(currentSelection) && ServiceTypes.Contains(currentSelection))
            {
                SelectedServiceType = currentSelection;
            }
            else
            {
                SelectedServiceType = "All Types";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating type filter");
        }
    }

    #endregion

    #region Private Methods - Helper Methods

    private async Task SaveServiceConfigurationAsync(ServiceDisplayItem? service)
    {
        if (service is null)
        {
            _logger.LogWarning("SaveServiceConfigurationAsync called with null service");
            return;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"🔔 SaveServiceConfigurationAsync called for {service.Name}, AutoStart: {service.AutoStart}");
            _logger.LogDebug("Updating service configuration in database for {ServiceName}", service.Name);
            
            // Removed ServiceLocator get
            
            System.Diagnostics.Debug.WriteLine($"🔔 Service repository type: {_serviceRepository.GetType().Name}");

            System.Diagnostics.Debug.WriteLine($"🔔 Getting service by ID: {service.Id}");
            // Get the existing service from database
            var existingService = await _serviceRepository.GetByIdAsync(service.Id);
            if (existingService == null)
            {
                System.Diagnostics.Debug.WriteLine($"🔔 Service with ID {service.Id} not found in database");
                throw new InvalidOperationException($"Service with ID {service.Id} not found in database");
            }

            System.Diagnostics.Debug.WriteLine($"🔔 Found existing service: {existingService.Name}, current AutoStart: {existingService.AutoStart}");
            
            // Update the service properties (map from ServiceDisplayItem to ServiceDefinition)
            existingService.DefaultPort = service.Port;
            existingService.AutoStart = service.AutoStart;
            existingService.BinaryPath = service.Path;
            existingService.Version = service.Version;

            System.Diagnostics.Debug.WriteLine($"🔔 Updated service AutoStart to: {existingService.AutoStart}, calling UpdateAsync");
            
            // Save to database
            var updatedService = await _serviceRepository.UpdateAsync(existingService);
            bool success = updatedService != null;
            
            System.Diagnostics.Debug.WriteLine($"🔔 Service configuration updated in database for {service.Name}");
            _logger.LogDebug("Service configuration updated in database for {ServiceName}", service.Name);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"🔔 Error in SaveServiceConfigurationAsync for {service.Name}: {ex.Message}");
            _logger.LogError(ex, "Failed to save service configuration for {ServiceName}", service.Name);
            throw; // Re-throw to be handled by the calling method
        }
    }

    private static string CreatePortConflictMessage(ServiceDisplayItem serviceToStart, ServiceDisplayItem conflictingService)
    {
        var message = $"Cannot start {serviceToStart.Name} on port {serviceToStart.Port}.\n\n";
        
        // Determine if it's an external process or another ModStack service
        var isExternal = conflictingService.Status == "Running (External)";
        
        if (isExternal)
        {
            message += $"Port {serviceToStart.Port} is already in use by an external process:\n\n";
            message += $"🔸 Process: {conflictingService.Name}\n";
            if (!string.IsNullOrEmpty(conflictingService.Version) && conflictingService.Version != "Unknown")
            {
                message += $"🔸 Version: {conflictingService.Version}\n";
            }
            message += $"🔸 Process ID: {conflictingService.ProcessIds}\n\n";
            
            message += "To resolve this conflict, you can:\n\n";
            message += "1. **Stop the conflicting process** - Use Task Manager to end the process\n";
            message += "2. **Change the port** - Configure this service to use a different port\n";
            message += "3. **Wait and retry** - If the process is temporary, wait for it to finish\n\n";
            
            message += "💡 **Tip**: You can use 'netstat -ano | findstr :3306' in Command Prompt to see what's using the port.";
        }
        else
        {
            message += $"Port {serviceToStart.Port} is already in use by another ModStack service:\n\n";
            message += $"🔸 Service: {conflictingService.Name} {conflictingService.Version}\n";
            message += $"🔸 Status: {conflictingService.Status}\n";
            if (!string.IsNullOrEmpty(conflictingService.ProcessIds))
            {
                message += $"🔸 Process ID: {conflictingService.ProcessIds}\n";
            }
            message += "\n";
            
            message += "To resolve this conflict, you can:\n\n";
            message += "1. **Stop the conflicting service** - Click the stop button for the service above\n";
            message += "2. **Change the port** - Configure this service to use a different port\n";
            message += "3. **Use different ports** - Configure both services to use different ports\n\n";
            
            message += "💡 **Tip**: Each service needs its own unique port to run simultaneously.";
        }
        
        return message;
    }

    private static string CreateUserFriendlyErrorMessage(ServiceDisplayItem service, Exception ex)
    {
        var baseMessage = $"Failed to start service '{service.Name}'";
        
        return ex switch
        {
            FileNotFoundException => $"{baseMessage}\n\nThe service executable was not found.\n\n" +
                                   $"Path: {service.Path}\n\n" +
                                   "Please check if the service is properly installed.",
                                   
            InvalidOperationException when ex.Message.Contains("Apache failed to start") => 
                $"{baseMessage}\n\n{ex.Message}",
                
            InvalidOperationException when ex.Message.Contains("Port") => 
                $"{baseMessage}\n\n{ex.Message}",
                
            InvalidOperationException => $"{baseMessage}\n\n{ex.Message}\n\n" +
                                       "Please check the service configuration and try again.",
                                       
            _ => $"{baseMessage}\n\nError: {ex.Message}\n\n" +
                 "Please check the application logs for more details."
        };
    }

    private void StartStatusRefreshTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += async (s, e) => await RefreshServiceStatusesAsync();
        timer.Start();
    }

    #endregion

    #region Command Can Execute Methods

    private bool CanStartService(ServiceDisplayItem? service) => 
        service != null && !service.IsBusy &&
        (service.Status == "Stopped");

    private bool CanStopService(ServiceDisplayItem? service) => 
        service != null && !service.IsBusy && service.Status == "Running";

    private bool CanDeleteService(ServiceDisplayItem? service) => 
        service != null && !service.IsBusy && !IsLoading;

    private bool CanConfigureService(ServiceDisplayItem? service) => 
        service != null && !service.IsBusy && !IsLoading;

    private bool CanManageExtensions(ServiceDisplayItem? service) => 
        service != null && service.Type == ModStack.Common.ServiceType.PHP;

    private bool CanManageApacheModules(ServiceDisplayItem? service) => 
        service != null && service.Type == ModStack.Common.ServiceType.Apache;

    /// <summary>
    /// Notifies all commands that their CanExecute state might have changed
    /// </summary>
    private void NotifyCommandsCanExecuteChanged()
    {
        if (StartServiceCommand is AsyncRelayCommand<ServiceDisplayItem> startCmd)
            startCmd.NotifyCanExecuteChanged();
        if (StopServiceCommand is AsyncRelayCommand<ServiceDisplayItem> stopCmd)
            stopCmd.NotifyCanExecuteChanged();
        if (DeleteServiceCommand is AsyncRelayCommand<ServiceDisplayItem> deleteCmd)
            deleteCmd.NotifyCanExecuteChanged();
        if (ConfigureServiceCommand is AsyncRelayCommand<ServiceDisplayItem> configCmd)
            configCmd.NotifyCanExecuteChanged();
        if (ManageExtensionsCommand is RelayCommand<ServiceDisplayItem> extCmd)
            extCmd.NotifyCanExecuteChanged();
        if (ManageApacheModulesCommand is RelayCommand<ServiceDisplayItem> modCmd)
            modCmd.NotifyCanExecuteChanged();
        if (AutoStartToggleCommand is AsyncRelayCommand<ServiceDisplayItem> autoStartCmd)
            autoStartCmd.NotifyCanExecuteChanged();
    }

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

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

    #endregion

    #region Install Service Command

    private async Task InstallServiceAsync()
    {
        try
        {
            _logger.LogInformation("Install Service command triggered - redirecting to Package Manager");
            
            // Navigate to Package Manager view
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow is not null)
                {
                    // Trigger the Package Manager button click to navigate
                    mainWindow.PackageManagerButton_Click(mainWindow, new RoutedEventArgs());
                }
                else
                {
                    _ = _dialogService.ShowErrorAsync(
                        "Unable to navigate to Package Manager. Please try again.", 
                        "Navigation Error");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in InstallServiceAsync");
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await _dialogService.ShowErrorAsync(
                    $"Error navigating to Package Manager: {ex.Message}", 
                    "Navigation Error");
            });
        }
    }

    #endregion
}
