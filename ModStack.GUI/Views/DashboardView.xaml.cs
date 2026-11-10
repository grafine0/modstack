using System.Windows.Controls;
using ModStack.Core.Interfaces;
using ModStack.Core.Services;
using ModStack.Services.Interfaces;
using ModStack.GUI.Services;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using System.Collections.ObjectModel;
using ModStack.Common.Models;
using System.Windows.Media;
using ModStack.GUI.Views.Dialogs;
using System.IO;
using Microsoft.Extensions.Logging;
using ModStack.GUI.ViewModels;
using ModStack.Services;

namespace ModStack.GUI.Views;

public partial class DashboardView : UserControl
{
    private readonly IStackManager _stackManager;
    private readonly IServiceService _serviceService;
    private readonly IProjectManager _projectManager;
    private readonly PackageManagerService _packageManagerService;
    private readonly ServiceControlManager _serviceControlManager;
    private readonly ILogger<DashboardView> _logger;
    private DashboardViewModel? _viewModel;

    private static bool _hasLoadedOnce = false;
    private bool _isHandlersWired = false;
    private CancellationTokenSource? _cancellationTokenSource;

    public DashboardView(
        ILogger<DashboardView> logger,
        IStackManager stackManager,
        IServiceService serviceService,
        IProjectManager projectManager,
        PackageManagerService packageManagerService,
        ServiceControlManager serviceControlManager)
    {
        InitializeComponent();
        
        _logger = logger;
        _stackManager = stackManager;
        _serviceService = serviceService;
        _projectManager = projectManager;
        _packageManagerService = packageManagerService;
        _serviceControlManager = serviceControlManager;
        
        // Initialize ViewModel
        _viewModel = new DashboardViewModel();
        DataContext = _viewModel;
        
        // Set initial loading state only if this is the first load
        if (!_hasLoadedOnce)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            MainContent.Visibility = Visibility.Collapsed;
            _logger.LogInformation("DashboardView: First time loading - overlay visible");
        }
        else
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MainContent.Visibility = Visibility.Visible;
            _logger.LogInformation("DashboardView: Subsequent load - overlay hidden");
        }
        
        // Wire up event handlers only once
        if (!_isHandlersWired)
        {
            StartStackButton.Click += async (s, e) => await StartStackButton_Click(s, e);
            StopStackButton.Click += async (s, e) => await StopStackButton_Click(s, e);
            RestartStackButton.Click += async (s, e) => await RestartStackButton_Click(s, e);
            CreateNewStackButton.Click += CreateNewStackButton_Click;
            CreateNewProjectButton.Click += CreateNewProjectButton_Click;
            
            // Load data when view becomes visible
            this.IsVisibleChanged += async (s, e) => await DashboardView_IsVisibleChanged(s, e);
            
            // Cleanup resources when view is unloaded
            this.Unloaded += DashboardView_Unloaded;
            
            _isHandlersWired = true;
        }
    }
    
    // InitializeServices removed as it was for ServiceLocator fallback

    private async Task DashboardView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            await LoadDashboardData();
        }
    }

    private async Task LoadDashboardData()
    {
        try
        {
            // Cancel any existing data loading
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            // Only show loading overlay on first load
            if (!_hasLoadedOnce)
            {
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    MainContent.Visibility = Visibility.Collapsed;
                }));
                _logger.LogInformation("DashboardView: First time loading - starting data load with overlay");
                
                // Add a small delay to ensure the overlay is visible
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("DashboardView: Subsequent load - loading data without overlay");
            }
            
            // Fetch data on background thread with cancellation support
            var statsData = await Task.Run(async () => await FetchStatsData(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            var stacksData = await Task.Run(async () => await FetchStacksData(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            var servicesData = await Task.Run(async () => await FetchServicesData(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            
            // Update UI on dispatcher thread
            await Dispatcher.InvokeAsync(() =>
            {
                UpdateStatsUI(statsData);
                UpdateStacksUI(stacksData);
                UpdateServicesUI(servicesData);
            });
            
            // Only add delay and show overlay transitions on first load
            if (!_hasLoadedOnce)
            {
                // Add a minimum delay to show the loading state
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                
                // Hide loading overlay and show content - use dispatcher for UI thread safety
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MainContent.Visibility = Visibility.Visible;
                }));
                _logger.LogInformation("DashboardView: First load completed - overlay hidden, content visible");
                
                // Mark as loaded
                _hasLoadedOnce = true;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DashboardView: Data loading was cancelled");
            // Don't show error dialog for cancellation
        }
        catch (Exception ex)
        {
            // Hide loading overlay even on error - use dispatcher for UI thread safety
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MainContent.Visibility = Visibility.Visible;
            }));
            
            _logger.LogError(ex, "DashboardView: Error loading dashboard data");
            
            // Show error dialog on UI thread with proper dispatcher handling
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    ModernDialogService.ShowError($"Error loading dashboard data: {ex.Message}", "Load Error");
                }
                catch (Exception dialogEx)
                {
                    _logger.LogError(dialogEx, "Failed to show error dialog");
                }
            }), DispatcherPriority.Normal);
        }
    }

    // Data structures for background fetching
    private class StatsData
    {
        public int TotalServicesCount { get; set; }
        public int TotalStacksCount { get; set; }
        public int ProjectsCount { get; set; }
        public int TotalPackagesCount { get; set; }
    }

    private async Task<StatsData> FetchStatsData(CancellationToken cancellationToken = default)
    {
        try
        {
            // Load total services count (only installed services, matching Service Management view)
            var services = await _serviceService.GetInstalledServicesAsync().ConfigureAwait(false);
            var totalServicesCount = services.Count();

            // Load total stacks count
            var stacks = await _stackManager.GetStacksAsync().ConfigureAwait(false);

            // Load projects count
            var projects = await _projectManager.GetAllProjectsAsync().ConfigureAwait(false);

            // Load total packages count
            var packages = await _packageManagerService.GetAvailablePackagesAsync().ConfigureAwait(false);
            var totalPackagesCount = packages.Count;

            return new StatsData
            {
                TotalServicesCount = totalServicesCount,
                TotalStacksCount = stacks.Count,
                ProjectsCount = projects.Count,
                TotalPackagesCount = totalPackagesCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stats data");
            return new StatsData(); // Return empty data on error
        }
    }

    private void UpdateStatsUI(StatsData statsData)
    {
        try
        {
            if (_viewModel == null) return;
            
            _viewModel.TotalServicesCount = statsData.TotalServicesCount;
            _viewModel.TotalStacksCount = statsData.TotalStacksCount;
            _viewModel.ProjectsCount = statsData.ProjectsCount;
            _viewModel.TotalPackagesCount = statsData.TotalPackagesCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating stats UI");
        }
    }

    private async Task<List<Stack>> FetchStacksData(CancellationToken cancellationToken = default)
    {
        try
        {
            var stacks = await _stackManager.GetStacksAsync().ConfigureAwait(false);
            return stacks.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stacks data");
            return new List<Stack>();
        }
    }

    private void UpdateStacksUI(List<Stack> stacks)
    {
        try
        {
            if (_viewModel == null) return;
            
            _viewModel.AvailableStacks.Clear();
            foreach (var stack in stacks)
            {
                _viewModel.AvailableStacks.Add(stack);
            }
            
            // Select active stack if any
            var activeStack = stacks.FirstOrDefault(s => s.IsActive);
            if (activeStack != null)
            {
                _viewModel.SelectedStack = activeStack;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating stacks UI");
        }
    }

    private async Task<List<ServiceDisplayItem>> FetchServicesData(CancellationToken cancellationToken = default)
    {
        try
        {
            // Load services using centralized service (only installed services for consistency)
            var serviceDisplayItems = await _serviceService.GetInstalledServicesAsync().ConfigureAwait(false);
            return serviceDisplayItems.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching services data");
            return new List<ServiceDisplayItem>();
        }
    }

    private void UpdateServicesUI(List<ServiceDisplayItem> serviceDisplayItems)
    {
        try
        {
            if (_viewModel == null) return;
            
            _viewModel.Services.Clear();
            
            foreach (var serviceDisplayItem in serviceDisplayItems)
            {
                _viewModel.Services.Add(serviceDisplayItem);
            }
            
            // Detect service statuses in parallel with throttling
            _ = Task.Run(async () => await DetectServiceStatusesInParallel(serviceDisplayItems));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating services UI");
        }
    }




    private async Task StartStackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedStack = _viewModel?.SelectedStack;
            if (selectedStack == null)
            {
                ModernDialogService.ShowError("Please select a stack to start.", "No Stack Selected");
                return;
            }

            if (selectedStack.IsActive)
            {
                ModernDialogService.ShowInformation($"Stack '{selectedStack.DisplayName}' is already active!", "Stack Active");
                return;
            }

            var success = await _stackManager.StartStackAsync(selectedStack.Id);
            if (success)
            {
                NotificationService.Instance.ShowSuccess($"Stack '{selectedStack.DisplayName}' started successfully!", "Stack Started");
                await LoadDashboardData(); // Refresh all data
            }
            else
            {
                ModernDialogService.ShowError($"Failed to start stack '{selectedStack.DisplayName}'.", "Start Failed");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error starting stack: {ex.Message}", "Start Error");
        }
    }

    private async Task StopStackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedStack = _viewModel?.SelectedStack;
            if (selectedStack == null)
            {
                ModernDialogService.ShowError("Please select a stack to stop.", "No Stack Selected");
                return;
            }

            if (!selectedStack.IsActive)
            {
                ModernDialogService.ShowInformation($"Stack '{selectedStack.DisplayName}' is not active.", "Stack Not Active");
                return;
            }

            var success = await _stackManager.StopStackAsync(selectedStack.Id);
            if (success)
            {
                NotificationService.Instance.ShowSuccess($"Stack '{selectedStack.DisplayName}' stopped successfully!", "Stack Stopped");
                await LoadDashboardData(); // Refresh all data
            }
            else
            {
                ModernDialogService.ShowError($"Failed to stop stack '{selectedStack.DisplayName}'.", "Stop Failed");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error stopping stack: {ex.Message}", "Stop Error");
        }
    }

    private async Task RestartStackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedStack = _viewModel?.SelectedStack;
            if (selectedStack == null)
            {
                ModernDialogService.ShowError("Please select a stack to restart.", "No Stack Selected");
                return;
            }

            // Stop first, then start
            if (selectedStack.IsActive)
            {
                await _stackManager.StopStackAsync(selectedStack.Id);
                await Task.Delay(2000); // Wait 2 seconds
            }

            var success = await _stackManager.StartStackAsync(selectedStack.Id);
            if (success)
            {
                NotificationService.Instance.ShowSuccess($"Stack '{selectedStack.DisplayName}' restarted successfully!", "Stack Restarted");
                await LoadDashboardData(); // Refresh all data
            }
            else
            {
                ModernDialogService.ShowError($"Failed to restart stack '{selectedStack.DisplayName}'.", "Restart Failed");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error restarting stack: {ex.Message}", "Restart Error");
        }
    }

    private void CreateNewStackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Navigate to Create Stack view
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.CreateStackButton_Click(sender, e);
            }
            else
            {
                ModernDialogService.ShowError("Unable to navigate to Create Stack view.", "Navigation Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error navigating to Create Stack: {ex.Message}", "Navigation Error");
        }
    }

    private void CreateNewProjectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Navigate to Create Project view
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.CreateProjectButton_Click(sender, e);
            }
            else
            {
                ModernDialogService.ShowError("Unable to navigate to Create Project view.", "Navigation Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error navigating to Create Project: {ex.Message}", "Navigation Error");
        }
    }


    private async void StartService_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button?.DataContext is ServiceDisplayItem service)
            {
                await StartServiceAsync(service);
            }
            else
            {
                ModernDialogService.ShowError("Unable to get service information to start.", "Service Start Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error starting service: {ex.Message}", "Service Start Error");
        }
    }

    private async void StopService_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button?.DataContext is ServiceDisplayItem service)
            {
                await StopServiceAsync(service);
            }
            else
            {
                ModernDialogService.ShowError("Unable to get service information to stop.", "Service Stop Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error stopping service: {ex.Message}", "Service Stop Error");
        }
    }

    private async Task StartServiceAsync(ServiceDisplayItem service)
    {
        if (service == null) return;

        try
        {
            service.IsBusy = true;

            // Check for port conflicts before starting
            _logger.LogInformation("Checking port conflict for {ServiceName} on port {Port}", service.Name, service.Port);
            var allServices = _viewModel?.Services?.ToList() ?? new List<ServiceDisplayItem>();
            var conflictingService = await ModStack.Core.Services.ServiceControlManager.CheckPortConflictAsync(service, allServices);
            if (conflictingService != null)
            {
                _logger.LogWarning("Port conflict detected: {ServiceName} on port {Port} (used by {ConflictingServiceName})", service.Name, service.Port, conflictingService.Name);
                ModernDialogService.ShowError($"Port {service.Port} is already in use by '{conflictingService.Name}'. Please stop the conflicting service first or change the port.", "Port Conflict");
                return;
            }
            _logger.LogInformation("No port conflict detected for {ServiceName} on port {Port}", service.Name, service.Port);

            // Update status to show starting
            service.Status = "Starting";

            _logger.LogInformation("Starting service: {ServiceName}", service.Name);
            await _serviceControlManager.StartServiceAsync(service);

            // Check status immediately after starting
            var newStatus = await _serviceControlManager.DetectServiceStatusAsync(service);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                service.Status = newStatus;
            });

            _logger.LogInformation("Service {ServiceName} started successfully", service.Name);

            // Show success toast notification
            NotificationService.Instance.ShowSuccess(
                $"Service '{service.Name}' started successfully",
                "Service Started");

            // Refresh the services UI to update status
            await LoadDashboardData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service {ServiceName}", service.Name);

            // Check for port conflicts after failed start attempt
            var allServices = _viewModel?.Services?.ToList() ?? new List<ServiceDisplayItem>();
            var conflictingService = await ModStack.Core.Services.ServiceControlManager.CheckPortConflictAsync(service, allServices);
            if (conflictingService != null)
            {
                ModernDialogService.ShowError($"Port {service.Port} is already in use by '{conflictingService.Name}'. Please stop the conflicting service first or change the port.", "Port Conflict");
            }
            else
            {
                ModernDialogService.ShowError($"Failed to start service '{service.Name}':\n\n{ex.Message}", "Service Start Error");
            }

            // Reset status on failure
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                service.Status = "Stopped";
            });
        }
        finally
        {
            service.IsBusy = false;
        }
    }

    private async Task StopServiceAsync(ServiceDisplayItem service)
    {
        if (service == null) return;

        try
        {
            service.IsBusy = true;

            // Update status to show stopping
            service.Status = "Stopping";

            _logger.LogInformation("Stopping service: {ServiceName}", service.Name);
            await _serviceControlManager.StopServiceAsync(service);

            // Check status immediately after stopping
            var newStatus = await _serviceControlManager.DetectServiceStatusAsync(service);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                service.Status = newStatus;
            });

            _logger.LogInformation("Service {ServiceName} stopped successfully", service.Name);

            // Show success toast notification
            NotificationService.Instance.ShowSuccess(
                $"Service '{service.Name}' stopped successfully",
                "Service Stopped");

            // Refresh the services UI to update status
            await LoadDashboardData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service {ServiceName}", service.Name);
            ModernDialogService.ShowError($"Failed to stop service '{service.Name}':\n\n{ex.Message}", "Service Stop Error");

            // Reset status on failure
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                service.Status = "Running";
            });
        }
        finally
        {
            service.IsBusy = false;
        }
    }

    private void ConfigureService_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the service from the button's data context
            var button = sender as Button;
            if (button?.DataContext is ServiceDisplayItem service)
            {
                // Open the service configuration dialog using our new modern dialog
                var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                var dialog = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<StackServiceConfigurationDialog>(
                    serviceProvider, service);
                dialog.Owner = Application.Current.MainWindow;
                Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
            }
            else
            {
                ModernDialogService.ShowError("Unable to get service information for configuration.", "Configuration Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error opening service configuration: {ex.Message}", "Configuration Error");
        }
    }


    /// <summary>
    /// Detect service statuses in parallel with throttling for better UI responsiveness
    /// </summary>
    private async Task DetectServiceStatusesInParallel(List<ServiceDisplayItem> services)
    {
        try
        {
            // Use Parallel.ForEachAsync with throttling to limit concurrent status checks
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount, // Limit concurrent operations
                CancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None
            };

            await Parallel.ForEachAsync(services, parallelOptions, async (service, cancellationToken) =>
            {
                try
                {
                    // Ask the central ServiceControlManager for accurate status
                    var detectedStatus = await _serviceControlManager.DetectServiceStatusAsync(service);
                    string actualStatus = detectedStatus;

                    // Update the service status on the UI thread
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        service.Status = actualStatus;
                    }, DispatcherPriority.Background);
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => { service.Status = "Error"; }, DispatcherPriority.Background);
                    
                    _logger.LogError(ex, "Error detecting status for service {ServiceName}", service.Name);
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service status detection was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in parallel service status detection");
        }
    }

    /// <summary>
    /// Cleanup resources when the view is unloaded
    /// </summary>
    private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cancel any ongoing operations and dispose resources
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
