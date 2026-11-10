using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModStack.Common;
using ModStack.Common.Models;
using ModStack.Core.Interfaces;
using ModStack.Services.Interfaces;

using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// Enum for stack status filtering
/// </summary>
public enum StackStatusFilter
{
    All,
    Active,
    Inactive,
    PartiallyActive
}

/// <summary>
/// Enum for service count filtering
/// </summary>
public enum ServiceCountFilter
{
    All,
    Small,      // 1-3 services
    Medium,     // 4-6 services
    Large       // 7+ services
}

/// <summary>
/// ViewModel for Stack Management with proper MVVM separation
/// </summary>
public class StackManagementViewModel : INotifyPropertyChanged
{
    private readonly IStackManager _stackManager;
    private readonly ILogger<StackManagementViewModel> _logger;
    private readonly IDispatcherService _dispatcherService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    private string _searchText = string.Empty;
    private StackStatusFilter _statusFilter = StackStatusFilter.All;
    private ServiceCountFilter _serviceCountFilter = ServiceCountFilter.All;
    private bool _isLoading = false;
    private string _statusBarText = "Ready";
    private int _totalStacks = 0;
    private int _activeStacks = 0;
    private Stack? _selectedStack;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Stack> Stacks { get; }
    public ICollectionView FilteredStacks { get; }
    
    // ComboBox ItemSources for proper XAML binding
    public ObservableCollection<StackStatusFilter> StatusOptions { get; }
    public ObservableCollection<ServiceCountFilter> ServiceCountOptions { get; }
    
    // Commands for UI actions
    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand CreateStackCommand { get; }
    public ICommand EditStackCommand { get; }
    public ICommand DeleteStackCommand { get; }
    public ICommand StartStackCommand { get; }
    public ICommand StopStackCommand { get; }
    public ICommand ImportStackCommand { get; }
    public ICommand ExportStackCommand { get; }

    public StackManagementViewModel(
        IStackManager stackManager,
        ILogger<StackManagementViewModel> logger,
        IDispatcherService dispatcherService,
        IDialogService dialogService,
        INavigationService navigationService)
    {
        _stackManager = stackManager ?? throw new ArgumentNullException(nameof(stackManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        // Initialize collections
        Stacks = new();
        FilteredStacks = CollectionViewSource.GetDefaultView(Stacks);
        FilteredStacks.Filter = ApplyStackFilter;
        
        // Initialize ComboBox ItemSources
        StatusOptions = new()
        {
            StackStatusFilter.All,
            StackStatusFilter.Active,
            StackStatusFilter.Inactive,
            StackStatusFilter.PartiallyActive
        };
        
        ServiceCountOptions = new()
        {
            ServiceCountFilter.All,
            ServiceCountFilter.Small,
            ServiceCountFilter.Medium,
            ServiceCountFilter.Large
        };

        // Initialize commands
        RefreshCommand = new AsyncRelayCommand(LoadStacksAsync, () => !IsLoading);
        ClearFiltersCommand = new RelayCommand(ClearFilters, CanClearFilters);
        CreateStackCommand = new RelayCommand(CreateStack);
        EditStackCommand = new AsyncRelayCommand<Stack>(EditStackAsync, stack => stack != null);
        DeleteStackCommand = new AsyncRelayCommand<Stack>(DeleteStackAsync, stack => stack != null);
        StartStackCommand = new AsyncRelayCommand<Stack>(StartStackAsync, stack => stack != null && !stack.IsActive);
        StopStackCommand = new AsyncRelayCommand<Stack>(StopStackAsync, stack => stack != null && stack.IsActive);
        ImportStackCommand = new RelayCommand(ImportStack);
        ExportStackCommand = new AsyncRelayCommand<Stack>(ExportStackAsync, stack => stack != null);

        // Load initial data
        _ = LoadStacksAsync();
    }

    #region Properties

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredStacks.Refresh();
                UpdateStatusBar();
                OnPropertyChanged(nameof(HasActiveFilters));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public StackStatusFilter StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                FilteredStacks.Refresh();
                UpdateStatusBar();
                OnPropertyChanged(nameof(HasActiveFilters));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ServiceCountFilter ServiceCountFilter
    {
        get => _serviceCountFilter;
        set
        {
            if (SetProperty(ref _serviceCountFilter, value))
            {
                FilteredStacks.Refresh();
                UpdateStatusBar();
                OnPropertyChanged(nameof(HasActiveFilters));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusBarText
    {
        get => _statusBarText;
        set => SetProperty(ref _statusBarText, value);
    }

    public int TotalStacks
    {
        get => _totalStacks;
        set => SetProperty(ref _totalStacks, value);
    }

    public int ActiveStacks
    {
        get => _activeStacks;
        set => SetProperty(ref _activeStacks, value);
    }

    public Stack? SelectedStack
    {
        get => _selectedStack;
        set
        {
            if (SetProperty(ref _selectedStack, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasStacks => Stacks.Count > 0;

    public bool HasActiveFilters =>
        !string.IsNullOrEmpty(SearchText) ||
        StatusFilter != StackStatusFilter.All ||
        ServiceCountFilter != ServiceCountFilter.All;

    public string TotalStacksText => $"{TotalStacks} stack{(TotalStacks == 1 ? "" : "s")}";
    public string ActiveStacksText => $"{ActiveStacks} active";

    #endregion

    #region Data Loading

    public async Task LoadStacksAsync()
    {
        try
        {
            IsLoading = true;
            StatusBarText = "Loading stacks...";

            var stacks = await _stackManager.GetStacksAsync();

            // Check actual running status of each stack
            foreach (var stack in stacks)
            {
                await UpdateStackRealTimeStatus(stack);
            }

            // Update UI on dispatcher thread
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                Stacks.Clear();
                foreach (var stack in stacks)
                {
                    Stacks.Add(stack);
                }
            });

            UpdateStatusBar();
            OnPropertyChanged(nameof(HasStacks));

            _logger.LogInformation("Loaded {StackCount} stacks", Stacks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading stacks");
            StatusBarText = "Error loading stacks";
            await _dialogService.ShowErrorAsync($"Error loading stacks: {ex.Message}", "Load Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool ApplyStackFilter(object obj)
    {
        if (obj is not Stack stack)
            return false;

        // Apply search filter
        if (!string.IsNullOrEmpty(SearchText))
        {
            if (!stack.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !stack.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !(stack.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }

        // Apply status filter
        if (StatusFilter != StackStatusFilter.All)
        {
            var matchesStatus = StatusFilter switch
            {
                StackStatusFilter.Active => stack.Status == StackStatus.Active,
                StackStatusFilter.Inactive => stack.Status == StackStatus.Inactive,
                StackStatusFilter.PartiallyActive => stack.Status == StackStatus.PartiallyActive,
                _ => true
            };

            if (!matchesStatus)
                return false;
        }

        // Apply service count filter
        if (ServiceCountFilter != ServiceCountFilter.All)
        {
            var serviceCount = stack.Services.Count;
            var matchesServiceCount = ServiceCountFilter switch
            {
                ServiceCountFilter.Small => serviceCount >= 1 && serviceCount <= 3,
                ServiceCountFilter.Medium => serviceCount >= 4 && serviceCount <= 6,
                ServiceCountFilter.Large => serviceCount >= 7,
                _ => true
            };

            if (!matchesServiceCount)
                return false;
        }

        return true;
    }

    private void UpdateStatusBar()
    {
        var filteredList = FilteredStacks.Cast<Stack>().ToList();
        TotalStacks = filteredList.Count;
        ActiveStacks = filteredList.Count(s => s.Status == StackStatus.Active);

        StatusBarText = TotalStacks switch
        {
            0 => "No stacks found",
            _ when ActiveStacks == TotalStacks => "All stacks are active",
            _ when ActiveStacks > 0 => $"{ActiveStacks} of {TotalStacks} stacks are active",
            _ => "No active stacks"
        };

        OnPropertyChanged(nameof(TotalStacksText));
        OnPropertyChanged(nameof(ActiveStacksText));
    }

    private async Task UpdateStackRealTimeStatus(Stack stack)
    {
        try
        {
            Debug.WriteLine($"🔍 Checking real-time status for stack: {stack.DisplayName}");

            int runningServices = 0;
            int totalServices = stack.Services.Count;

            foreach (var stackService in stack.Services)
            {
                bool isRunning = await IsServiceActuallyRunning(stackService.Service);
                if (isRunning)
                {
                    runningServices++;
                }
                Debug.WriteLine($"   Service {stackService.Service.Name}: {(isRunning ? "Running" : "Stopped")}");
            }

            // Update stack status based on running services
            StackStatus newStatus;
            bool newIsActive;

            if (runningServices == 0)
            {
                newStatus = StackStatus.Inactive;
                newIsActive = false;
            }
            else if (runningServices == totalServices)
            {
                newStatus = StackStatus.Active;
                newIsActive = true;
            }
            else
            {
                newStatus = StackStatus.PartiallyActive;
                newIsActive = true;
            }

            // Update stack properties if they changed
            if (stack.Status != newStatus || stack.IsActive != newIsActive)
            {
                Debug.WriteLine($"🔄 Updating stack {stack.DisplayName}: Status {stack.Status} → {newStatus}, IsActive {stack.IsActive} → {newIsActive}");

                stack.Status = newStatus;
                stack.IsActive = newIsActive;

                // Update in database
                await _stackManager.UpdateStackAsync(stack);
                Debug.WriteLine("💾 Stack status updated in database");
            }
            else
            {
                Debug.WriteLine($"✅ Stack {stack.DisplayName} status is already correct: {stack.Status}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error checking stack status: {ex.Message}");
            _logger.LogError(ex, "Error updating stack real-time status");
        }
    }

    private static async Task<bool> IsServiceActuallyRunning(ServiceDefinition service)
    {
        try
        {
            await Task.Delay(50); // Small delay to avoid blocking UI

            switch (service.ServiceType)
            {
                case ServiceType.PHP:
                    var phpProcesses = Process.GetProcessesByName("php-cgi");
                    foreach (var process in phpProcesses)
                    {
                        try
                        {
                            var processPath = process.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(processPath) &&
                                processPath.StartsWith(service.BinaryPath ?? "", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch { /* Process may have exited */ }
                    }
                    break;

                case ServiceType.Apache:
                    var apacheProcesses = Process.GetProcessesByName("httpd");
                    foreach (var process in apacheProcesses)
                    {
                        try
                        {
                            var processPath = process.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(processPath) &&
                                processPath.StartsWith(service.BinaryPath ?? "", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch { /* Process may have exited */ }
                    }
                    break;

                case ServiceType.MySQL:
                    var mysqlProcesses = Process.GetProcessesByName("mysqld");
                    return mysqlProcesses.Length > 0;

                case ServiceType.Nginx:
                    var nginxProcesses = Process.GetProcessesByName("nginx");
                    return nginxProcesses.Length > 0;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error checking if service {service.Name} is running: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Commands Implementation

    private void ClearFilters()
    {
        SearchText = string.Empty;
        StatusFilter = StackStatusFilter.All;
        ServiceCountFilter = ServiceCountFilter.All;
    }

    private bool CanClearFilters()
    {
        return HasActiveFilters;
    }

    private void CreateStack()
    {
        try
        {
            _navigationService.NavigateToCreateStack();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to create stack view");
        }
    }

    private async Task EditStackAsync(Stack? stack)
    {
        if (stack == null)
            return;

        try
        {
            _navigationService.NavigateToEditStack(stack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to edit stack view for {StackName}", stack.DisplayName);
            await _dialogService.ShowErrorAsync($"Error opening edit view: {ex.Message}", "Error");
        }
    }

    private async Task DeleteStackAsync(Stack? stack)
    {
        if (stack == null)
            return;

        try
        {
            var result = await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to delete stack '{stack.DisplayName}'?\n\nThis will also remove {stack.Services.Count} service configuration(s).",
                "Delete Stack");

            if (result)
            {
                // Update status bar
                MainWindow.UpdateStatusBar($"Deleting stack '{stack.DisplayName}'...");
                
                await _stackManager.DeleteStackAsync(stack.Id);
                Stacks.Remove(stack);
                UpdateStatusBar();
                OnPropertyChanged(nameof(HasStacks));

                MainWindow.ResetStatusBar();
                await _dialogService.ShowInformationAsync($"Stack '{stack.DisplayName}' has been deleted.", "Stack Deleted");
                _logger.LogInformation("Deleted stack {StackName}", stack.DisplayName);
            }
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Error deleting stack {StackName}", stack.DisplayName);
            await _dialogService.ShowErrorAsync($"Error deleting stack: {ex.Message}", "Delete Error");
            await LoadStacksAsync(); // Refresh to restore UI state
        }
    }

    private async Task StartStackAsync(Stack? stack)
    {
        if (stack == null)
            return;

        try
        {
            Debug.WriteLine($"🚀 Starting stack: {stack.DisplayName}");
            
            // Update status bar
            MainWindow.UpdateStatusBar($"Starting stack '{stack.DisplayName}'...");
            
            var originalStatus = stack.Status;
            
            // Update status immediately on UI thread to show "Starting..."
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                stack.Status = StackStatus.Starting;
                // Force collection view to refresh
                FilteredStacks.Refresh();
            });

            var success = await _stackManager.StartStackAsync(stack.Id);
            if (success)
            {
                await Task.Delay(1000); // Wait for services to start

                MainWindow.ResetStatusBar();
                NotificationService.Instance.ShowSuccess($"Stack '{stack.DisplayName}' started successfully!", "Stack Started");
                Debug.WriteLine($"✅ Stack {stack.DisplayName} started successfully");

                await LoadStacksAsync(); // Refresh the view with updated status
            }
            else
            {
                MainWindow.ResetStatusBar();
                // Revert status on failure
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    stack.Status = originalStatus;
                    FilteredStacks.Refresh();
                });
                
                NotificationService.Instance.ShowError(
                    $"Failed to start stack '{stack.DisplayName}'. Some services may not be installed or configured.",
                    "Start Failed");
                Debug.WriteLine($"❌ Failed to start stack {stack.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Error starting stack {StackName}", stack.DisplayName);
            
            // Revert status on error
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                if (stack != null)
                {
                    stack.Status = StackStatus.Inactive;
                    FilteredStacks.Refresh();
                }
            });
            
            NotificationService.Instance.ShowError($"Error starting stack: {ex.Message}", "Start Error");
        }
    }

    private async Task StopStackAsync(Stack? stack)
    {
        if (stack == null)
            return;

        try
        {
            Debug.WriteLine($"🛑 Stopping stack: {stack.DisplayName}");
            
            // Update status bar
            MainWindow.UpdateStatusBar($"Stopping stack '{stack.DisplayName}'...");
            
            var originalStatus = stack.Status;
            
            // Update status immediately on UI thread to show "Stopping..."
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                stack.Status = StackStatus.Stopping;
                // Force collection view to refresh
                FilteredStacks.Refresh();
            });

            var success = await _stackManager.StopStackAsync(stack.Id);
            if (success)
            {
                await Task.Delay(1000); // Wait for services to stop

                MainWindow.ResetStatusBar();
                NotificationService.Instance.ShowSuccess($"Stack '{stack.DisplayName}' stopped successfully!", "Stack Stopped");
                Debug.WriteLine($"✅ Stack {stack.DisplayName} stopped successfully");

                await LoadStacksAsync(); // Refresh the view with updated status
            }
            else
            {
                MainWindow.ResetStatusBar();
                // Revert status on failure
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    stack.Status = originalStatus;
                    FilteredStacks.Refresh();
                });
                
                NotificationService.Instance.ShowError(
                    $"Failed to stop stack '{stack.DisplayName}'. Some services may still be running.",
                    "Stop Failed");
                Debug.WriteLine($"❌ Failed to stop stack {stack.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            _logger.LogError(ex, "Error stopping stack {StackName}", stack.DisplayName);
            
            // Revert status on error
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                if (stack != null)
                {
                    stack.Status = StackStatus.Active;
                    FilteredStacks.Refresh();
                }
            });
            
            NotificationService.Instance.ShowError($"Error stopping stack: {ex.Message}", "Stop Error");
        }
    }

    private void ImportStack()
    {
        // TODO: Implement import stack functionality
        _logger.LogWarning("Import stack functionality not yet implemented");
    }

    private async Task ExportStackAsync(Stack? stack)
    {
        if (stack == null)
            return;

        // TODO: Implement export stack functionality
        _logger.LogWarning("Export stack functionality not yet implemented for {StackName}", stack.DisplayName);
        await Task.CompletedTask;
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

