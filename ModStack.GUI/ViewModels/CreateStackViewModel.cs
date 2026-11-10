using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ModStack.Common;
using ModStack.Core.Interfaces;
using ModStack.Services.Interfaces;
using ModStack.Common.Models;

using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Views.Dialogs;
using ModStack.Data.Interfaces;
using Microsoft.Extensions.Logging;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for Create/Edit Stack view following MVVM pattern
/// </summary>
public class CreateStackViewModel : INotifyPropertyChanged
{
    private readonly IServiceManager _serviceManager;
    private readonly IStackManager _stackManager;
    private readonly IServiceRepository _serviceRepository;
    private readonly IServiceInstallationChecker _installationChecker;
    private readonly ILocalizationService? _localizationService;
    private readonly ILogger<CreateStackViewModel> _logger;
    
    private readonly Stack? _editingStack;
    private bool _isEditMode;
    private string _stackName = string.Empty;
    private string _description = string.Empty;
    private bool _isDefault;
    private bool _autoStart;
    private bool _createProject;
    private bool _autoPortAssignment;
    private bool _enableLogging;
    private bool _createBackup;
    private bool _isLoading = false;

    public CreateStackViewModel(
        IServiceManager serviceManager,
        IStackManager stackManager,
        IServiceRepository serviceRepository,
        IServiceInstallationChecker installationChecker,
        IDialogService? dialogService = null,
        ILocalizationService? localizationService = null,
        ILogger<CreateStackViewModel>? logger = null,
        Stack? stackToEdit = null)
    {
        _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        _stackManager = stackManager ?? throw new ArgumentNullException(nameof(stackManager));
        _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
        _installationChecker = installationChecker ?? throw new ArgumentNullException(nameof(installationChecker));
        // dialogService is optional since we use ModernDialogService static methods
        
        _localizationService = localizationService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateStackViewModel>.Instance;
        
        Services = new ObservableCollection<ServiceSelectionItem>();
        _editingStack = stackToEdit;
        _isEditMode = stackToEdit != null;

        // Initialize commands
        CreateOrUpdateCommand = new AsyncRelayCommand(CreateOrUpdateStackAsync);
        BackCommand = new RelayCommand(Back);
        ApplyLAMPTemplateCommand = new RelayCommand(() => ApplyTemplate("LAMP Stack", "Apache + PHP + MySQL development stack", ["apache", "php", "mysql"]));
        ApplyLEMPTemplateCommand = new RelayCommand(() => ApplyTemplate("LEMP Stack", "Nginx + PHP + MySQL development stack", ["nginx", "php", "mysql"]));
        ApplyDatabaseTemplateCommand = new RelayCommand(() => ApplyTemplate("Database Stack", "Multiple database servers", ["mysql", "postgresql", "redis"]));
        ApplyCustomTemplateCommand = new RelayCommand(ApplyCustomTemplate);

        // Subscribe to culture changes if localization service is available
        if (_localizationService != null)
        {
            _localizationService.CultureChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageSubtitle));
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(ButtonIcon));
            };
        }

        // Defer loading to InitializeAsync to guarantee ordering
    }

    #region Properties

    public ObservableCollection<ServiceSelectionItem> Services { get; }
    
    public string PageTitle
    {
        get
        {
            var localizationService = _localizationService;
            return _isEditMode
                ? (localizationService?.GetString("EditStackTitle") ?? "Edit Stack")
                : (localizationService?.GetString("CreateStackTitle") ?? "Create New Stack");
        }
    }

    public string PageSubtitle
    {
        get
        {
            var localizationService = _localizationService;
            return _isEditMode
                ? (localizationService?.GetString("EditStackSubtitle") ?? "Modify your development stack configuration")
                : (localizationService?.GetString("CreateStackSubtitle") ?? "Configure a new development stack with your preferred services");
        }
    }

    public string ButtonText
    {
        get
        {
            var localizationService = _localizationService;
            return _isEditMode
                ? (localizationService?.GetString("ButtonUpdateStack") ?? "Update Stack")
                : (localizationService?.GetString("ButtonCreateStack") ?? "Create Stack");
        }
    }

    public string ButtonIcon
    {
        get
        {
            return _isEditMode ? "💾" : "➕";
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    public string StackName
    {
        get => _stackName;
        set
        {
            if (_stackName != value)
            {
                _stackName = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault != value)
            {
                _isDefault = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (_autoStart != value)
            {
                _autoStart = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CreateProject
    {
        get => _createProject;
        set
        {
            if (_createProject != value)
            {
                _createProject = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoPortAssignment
    {
        get => _autoPortAssignment;
        set
        {
            if (_autoPortAssignment != value)
            {
                _autoPortAssignment = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableLogging
    {
        get => _enableLogging;
        set
        {
            if (_enableLogging != value)
            {
                _enableLogging = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CreateBackup
    {
        get => _createBackup;
        set
        {
            if (_createBackup != value)
            {
                _createBackup = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand CreateOrUpdateCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ApplyLAMPTemplateCommand { get; }
    public ICommand ApplyLEMPTemplateCommand { get; }
    public ICommand ApplyDatabaseTemplateCommand { get; }
    public ICommand ApplyCustomTemplateCommand { get; }

    #endregion

    #region Command Implementations

    private async Task CreateOrUpdateStackAsync()
    {
        try
        {
            // Validate input first (before setting loading)
            if (string.IsNullOrWhiteSpace(StackName))
            {
                ModernDialogService.ShowWarning("Please enter a stack name.", "Validation Error");
                return;
            }

            var selectedServices = Services.Where(s => s.IsSelected).ToList();

            if (selectedServices.Count == 0)
            {
                ModernDialogService.ShowWarning("Please select at least one service.", "Validation Error");
                return;
            }

            if (_isEditMode && _editingStack != null)
            {
                // Update existing stack - set loading after validation
                IsLoading = true;
                
                // Update status bar
                MainWindow.UpdateStatusBar($"Updating stack '{StackName}'...");
                
                // Give UI time to update before async operation
                await Task.Delay(50);
                
                await UpdateStackInDatabase(_editingStack.Id, StackName, Description, selectedServices);
                
                // Reset status bar
                MainWindow.ResetStatusBar();
            }
            else
            {
                // Show confirmation dialog first for new stack (before setting loading)
                var serviceList = selectedServices.Select(s => $"• {s.Name} v{s.Version}").ToList();
                var result = ModernDialogService.ShowStackCreationConfirmation(StackName, serviceList);

                if (result == MessageBoxResult.OK)
                {
                    // Set loading after confirmation
                    IsLoading = true;
                    
                    // Update status bar
                    MainWindow.UpdateStatusBar($"Creating stack '{StackName}'...");
                    
                    // Give UI time to update before async operation
                    await Task.Delay(50);
                    
                    // Create the stack using IStackManager
                    var createdStack = await CreateStackInDatabase(StackName, Description, selectedServices);

                    // Reset status bar
                    MainWindow.ResetStatusBar();

                    // Return created stack so View can handle navigation
                    if (createdStack != null)
                    {
                        CreatedStack = createdStack;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MainWindow.ResetStatusBar();
            ModernDialogService.ShowError($"Error {(_isEditMode ? "updating" : "creating")} stack: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Back()
    {
        // Notify that back was requested
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTemplate(string name, string desc, string[] serviceNames)
    {
        StackName = name;
        Description = desc;

        // Clear all selections first
        foreach (var service in Services)
        {
            service.IsSelected = false;
        }

        // Select template services
        foreach (var serviceName in serviceNames)
        {
            var service = Services.FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
            if (service != null)
            {
                service.IsSelected = true;
            }
        }
    }

    private void ApplyCustomTemplate()
    {
        // Clear all selections for custom configuration
        foreach (var service in Services)
        {
            service.IsSelected = false;
        }

        StackName = "";
        Description = "";
    }

    #endregion

    #region Events

    public event EventHandler? BackRequested;
    public event EventHandler<Stack>? StackCreated;

    #endregion

    #region Public Properties for Navigation

    public Stack? CreatedStack { get; private set; }

    #endregion

    #region Private Methods

    public async Task InitializeAsync()
    {
        await LoadInstalledServicesAsync();
        if (_isEditMode && _editingStack != null)
        {
            await PopulateFormForEditAsync();
        }
    }

    private async Task LoadInstalledServicesAsync()
    {
        try
        {
            // Load all installed services from the database
            var services = await _serviceRepository.GetInstalledAsync();

            Services.Clear();

            foreach (var service in services)
            {
                // Use IServiceInstallationChecker to verify actual installation
                var isInstalled = _installationChecker.IsServiceInstalled(service.BinaryPath, service.ServiceType);

                Services.Add(new ServiceSelectionItem
                {
                    Name = service.Name,
                    Version = service.Version, // Use version from database
                    ServiceType = service.ServiceType.ToString(),
                    Port = service.DefaultPort ?? 0,
                    Priority = GetDefaultPriority(service.ServiceType),
                    IsSelected = false,
                    IsInstalled = isInstalled,
                    DatabaseId = service.Id
                });
            }

            if (!Services.Any())
            {
                ModernDialogService.ShowWarning("No installed services are available in the database. Please install services first using the Package Manager.", "No Installed Services");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error loading services from database: {ex.Message}", "Database Error");
            LoadSampleServices();
        }
    }

    private void LoadSampleServices()
    {
        Services.Add(new ServiceSelectionItem { Name = "apache", Version = "2.4.58", ServiceType = ServiceType.Apache.ToString(), Port = 80, Priority = 1, IsSelected = false });
        Services.Add(new ServiceSelectionItem { Name = "nginx", Version = "1.24.0", ServiceType = ServiceType.Nginx.ToString(), Port = 80, Priority = 1, IsSelected = false });
        Services.Add(new ServiceSelectionItem { Name = "php", Version = "8.3.0", ServiceType = ServiceType.PHP.ToString(), Port = 9000, Priority = 2, IsSelected = false });
        Services.Add(new ServiceSelectionItem { Name = "mysql", Version = "8.0.35", ServiceType = ServiceType.MySQL.ToString(), Port = 3306, Priority = 3, IsSelected = false });
        Services.Add(new ServiceSelectionItem { Name = "postgresql", Version = "16.0", ServiceType = ServiceType.PostgreSQL.ToString(), Port = 5432, Priority = 3, IsSelected = false });
        Services.Add(new ServiceSelectionItem { Name = "redis", Version = "7.2.3", ServiceType = ServiceType.Redis.ToString(), Port = 6379, Priority = 4, IsSelected = false });
    }

    private async Task PopulateFormForEditAsync()
    {
        if (_editingStack == null) return;

        // Ensure we have a fresh copy with services populated from DB
        var dbStack = await _stackManager.GetStackAsync(_editingStack.Id) ?? _editingStack;

        StackName = dbStack.Name;
        Description = dbStack.Description ?? "";
        IsDefault = dbStack.IsDefault;

        var autoStartValue = dbStack.Configuration.TryGetValue("AutoStart", out var autoStartObj) && Convert.ToBoolean(autoStartObj);
        AutoStart = autoStartValue;

        CreateProject = dbStack.Configuration.ContainsKey("CreateProject") && Convert.ToBoolean(dbStack.Configuration["CreateProject"]);
        AutoPortAssignment = dbStack.Configuration.ContainsKey("AutoPortAssignment") && Convert.ToBoolean(dbStack.Configuration["AutoPortAssignment"]);
        EnableLogging = dbStack.Configuration.ContainsKey("EnableLogging") && Convert.ToBoolean(dbStack.Configuration["EnableLogging"]);
        CreateBackup = dbStack.Configuration.ContainsKey("CreateBackup") && Convert.ToBoolean(dbStack.Configuration["CreateBackup"]);

        // Mark selected services by ServiceId for reliability
        foreach (var stackService in dbStack.Services)
        {
            var serviceItem = Services.FirstOrDefault(s => s.DatabaseId == stackService.ServiceId)
                              ?? Services.FirstOrDefault(s =>
                                    s.Name.Equals(stackService.Service.Name, StringComparison.OrdinalIgnoreCase));

            if (serviceItem != null)
            {
                serviceItem.IsSelected = true;
                if (stackService.Port.HasValue) serviceItem.Port = stackService.Port.Value;
                serviceItem.Priority = stackService.Priority;
            }
        }
    }

    private async Task<Stack?> CreateStackInDatabase(string stackName, string description, List<ServiceSelectionItem> selectedServices)
    {
        MainWindow.UpdateStatusBar($"Creating stack '{stackName}'...");
        var stack = await _stackManager.CreateStackAsync(stackName, stackName, description);

        stack.IsDefault = IsDefault;
        stack.Configuration["AutoStart"] = AutoStart;
        stack.Configuration["CreateProject"] = CreateProject;
        stack.Configuration["AutoPortAssignment"] = AutoPortAssignment;
        stack.Configuration["EnableLogging"] = EnableLogging;
        stack.Configuration["CreateBackup"] = CreateBackup;

        MainWindow.UpdateStatusBar($"Configuring stack '{stackName}'...");
        await _stackManager.UpdateStackAsync(stack);

        int addedCount = 0;
        int totalServices = selectedServices.Count;
        int currentServiceIndex = 0;
        
        foreach (var serviceItem in selectedServices)
        {
            currentServiceIndex++;
            MainWindow.UpdateStatusBar($"Adding services to stack '{stackName}' ({currentServiceIndex}/{totalServices})...");
            
            long serviceId = serviceItem.DatabaseId;

            // Fallback: resolve service ID by name/version if DatabaseId is missing
            if (serviceId <= 0)
            {
                var resolved = await _serviceRepository.GetByNameAndVersionAsync(serviceItem.Name, serviceItem.Version);
                if (resolved != null)
                {
                    serviceId = resolved.Id;
                }
            }

            if (serviceId > 0)
            {
                var ok = await _stackManager.AddServiceToStackAsync(stack.Id, serviceId, serviceItem.Port, serviceItem.Priority);
                if (ok) addedCount++;
            }
        }

        // Safety check: warn if nothing was saved
        if (addedCount == 0)
        {
            NotificationService.Instance.ShowError(
                _localizationService?.GetString("StackServicesSaveFailed") ?? "No services were saved to the stack.",
                _localizationService?.GetString("Error") ?? "Error");
        }

        NotificationService.Instance.ShowSuccess($"Stack '{stackName}' has been created successfully", "Stack Created");
        MainWindow.NotifyStackUpdated();

        StackCreated?.Invoke(this, stack);
        return stack;
    }

    private async Task UpdateStackInDatabase(long stackId, string stackName, string description, List<ServiceSelectionItem> selectedServices)
    {
        MainWindow.UpdateStatusBar($"Updating stack '{stackName}'...");
        var stackToUpdate = await _stackManager.GetStackAsync(stackId);
        if (stackToUpdate != null)
        {
            stackToUpdate.Name = stackName;
            stackToUpdate.DisplayName = stackName;
            stackToUpdate.Description = description;
            stackToUpdate.IsDefault = IsDefault;
            stackToUpdate.Configuration["AutoStart"] = AutoStart;
            stackToUpdate.Configuration["CreateProject"] = CreateProject;
            stackToUpdate.Configuration["AutoPortAssignment"] = AutoPortAssignment;
            stackToUpdate.Configuration["EnableLogging"] = EnableLogging;
            stackToUpdate.Configuration["CreateBackup"] = CreateBackup;

            MainWindow.UpdateStatusBar($"Saving stack configuration for '{stackName}'...");
        await _stackManager.UpdateStackAsync(stackToUpdate);
        }

        // Get fresh copy of the stack with services
        MainWindow.UpdateStatusBar($"Loading stack services for '{stackName}'...");
        var currentStack = await _stackManager.GetStackAsync(stackId);
        if (currentStack == null) return;

        // First, resolve DatabaseId for all selected services that don't have it set
        MainWindow.UpdateStatusBar($"Resolving services for stack '{stackName}'...");
        foreach (var serviceItem in selectedServices)
        {
            if (serviceItem.DatabaseId <= 0)
            {
                var resolved = await _serviceRepository.GetByNameAndVersionAsync(serviceItem.Name, serviceItem.Version);
                if (resolved != null)
                {
                    serviceItem.DatabaseId = resolved.Id;
                }
            }
        }

        // Get list of selected service IDs (after resolution)
        var selectedServiceIds = selectedServices
            .Where(s => s.DatabaseId > 0)
            .Select(s => s.DatabaseId)
            .ToHashSet();

        // Get list of current service IDs in the stack
        var currentServiceIds = currentStack.Services.Select(s => s.ServiceId).ToHashSet();

        // Remove services that are no longer selected
        int totalServicesToRemove = currentServiceIds.Count(s => !selectedServiceIds.Contains(s));
        int removedCount = 0;
        
        foreach (var currentServiceId in currentServiceIds)
        {
            if (!selectedServiceIds.Contains(currentServiceId))
            {
                removedCount++;
                if (totalServicesToRemove > 0)
                {
                    MainWindow.UpdateStatusBar($"Removing services from stack '{stackName}' ({removedCount}/{totalServicesToRemove})...");
                }
                
                var removeSuccess = await _stackManager.RemoveServiceFromStackAsync(stackId, currentServiceId);
                if (!removeSuccess)
                {
                    NotificationService.Instance.ShowWarning(
                        _localizationService?.GetString("RemoveServiceFromStackFailed", "Unknown", stackName) ?? $"Failed to remove service from stack '{stackName}'.",
                        _localizationService?.GetString("ServiceRemoveWarningTitle") ?? "Service Remove Warning");
                }
            }
        }

        // Refresh the stack after removals to get updated services list
        currentStack = await _stackManager.GetStackAsync(stackId);
        if (currentStack == null) return;

        // Add or update selected services
        MainWindow.UpdateStatusBar($"Adding/updating services for stack '{stackName}'...");
        int updatedOrAdded = 0;
        int totalServicesToUpdate = selectedServices.Count(s => s.DatabaseId > 0);
        int currentServiceIndex = 0;
        
        foreach (var serviceItem in selectedServices)
        {
            if (serviceItem.DatabaseId <= 0) continue;

            currentServiceIndex++;
            if (totalServicesToUpdate > 0)
            {
                MainWindow.UpdateStatusBar($"Updating services for stack '{stackName}' ({currentServiceIndex}/{totalServicesToUpdate})...");
            }

            var existingStackService = currentStack.Services.FirstOrDefault(s => s.ServiceId == serviceItem.DatabaseId);
            if (existingStackService != null)
            {
                // Update existing service in stack
                existingStackService.Port = serviceItem.Port;
                existingStackService.Priority = serviceItem.Priority;
                var updateServiceSuccess = await _stackManager.UpdateStackServiceAsync(existingStackService);
                if (updateServiceSuccess)
                {
                    updatedOrAdded++;
                }
                else
                {
                    NotificationService.Instance.ShowWarning(
                        _localizationService?.GetString("UpdateStackServiceFailed", serviceItem.Name, stackName) ?? $"Failed to update service '{serviceItem.Name}' in stack '{stackName}'.",
                        _localizationService?.GetString("ServiceUpdateWarningTitle") ?? "Service Update Warning");
                }
            }
            else
            {
                // Add new service to stack
                var addServiceSuccess = await _stackManager.AddServiceToStackAsync(stackId, serviceItem.DatabaseId, serviceItem.Port, serviceItem.Priority);
                if (addServiceSuccess)
                {
                    updatedOrAdded++;
                }
                else
                {
                    NotificationService.Instance.ShowWarning(
                        _localizationService?.GetString("AddServiceToStackFailed", serviceItem.Name, stackName) ?? $"Failed to add service '{serviceItem.Name}' to stack '{stackName}'.",
                        _localizationService?.GetString("ServiceAddWarningTitle") ?? "Service Add Warning");
                }
            }
        }

        if (updatedOrAdded == 0 && selectedServices.Count > 0)
        {
            NotificationService.Instance.ShowWarning(
                _localizationService?.GetString("StackServicesSaveFailed") ?? "Services were not saved. Please try again.",
                _localizationService?.GetString("Warning") ?? "Warning");
        }

        NotificationService.Instance.ShowSuccess($"Stack '{stackName}' has been updated successfully", "Stack Updated");
        MainWindow.NotifyStackUpdated();
    }

    private static int GetDefaultPriority(ServiceType serviceType)
    {
        return serviceType switch
        {
            ServiceType.Apache => 1,
            ServiceType.Nginx => 1,
            ServiceType.PHP => 2,
            ServiceType.MySQL => 3,
            ServiceType.MariaDB => 3,
            ServiceType.PostgreSQL => 3,
            ServiceType.Redis => 4,
            _ => 5
        };
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Model for service selection in the stack creation view
/// </summary>
public class ServiceSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private int _priority;

    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool IsInstalled { get; set; }
    public long DatabaseId { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public int Priority
    {
        get => _priority;
        set
        {
            if (_priority != value)
            {
                _priority = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
