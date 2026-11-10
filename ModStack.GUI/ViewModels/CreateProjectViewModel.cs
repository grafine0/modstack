using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Core.Services;
using ModStack.Data.Interfaces;

using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Views;
using ModStack.Common;
using ModStack.Common.Interfaces;
using ModStack.Services;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for creating and editing projects with full MVVM support
/// </summary>
public partial class CreateProjectViewModel : INotifyPropertyChanged
{
    [System.Text.RegularExpressions.GeneratedRegex(@"[^a-z0-9\-_]")]
    private static partial System.Text.RegularExpressions.Regex InvalidCharacterPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"-+")]
    private static partial System.Text.RegularExpressions.Regex MultipleHyphensPattern();
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IServiceService _serviceService;
    private readonly IProjectServiceRepository _projectServiceRepository;
    private readonly IStackServiceRepository _stackServiceRepository;
    private readonly ISSLCertificateService _sslCertificateService;
    private readonly IPathConfigurationService _pathConfigurationService;
    private readonly ProjectFormHandler _projectFormHandler;
    private readonly ILogger<CreateProjectViewModel> _logger;
    private readonly IProjectCreationService _projectCreationService;
    private readonly IVirtualHostService _virtualHostService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ServiceControlManager _serviceControlManager;
    private readonly object _serviceRestartSync = new();
    private CancellationTokenSource? _serviceRestartCts;
    private Task? _serviceRestartTask;
    
    // Project properties
    private string _projectName = string.Empty;
    private string _projectPath = string.Empty;
    private string _domain = string.Empty;
    private string _documentRoot = string.Empty;
    private string _serverAliases = string.Empty;
    private string _port = "80";
    private bool _enableSSL = false;
    private bool _createDirectory = true;
    private bool _updateHostsFile = true;
    private bool _createVirtualHost = true;
    
    // Stack selection
    private bool _noStackSelected = true;
    private bool _existingStackSelected = false;
    private StackItem? _selectedStack;
    
    // Service selection
    private ServiceBindingItem? _selectedWebServer;
    private ServiceBindingItem? _selectedDatabase;
    private ServiceBindingItem? _selectedPHPRuntime;
    private ServiceBindingItem? _selectedAdditionalService;
    
    // Collections
    private ObservableCollection<StackItem> _availableStacks = [];
    private ObservableCollection<ServiceBindingItem> _webServerServices = [];
    private ObservableCollection<ServiceBindingItem> _databaseServices = [];
    private ObservableCollection<ServiceBindingItem> _phpRuntimeServices = [];
    private ObservableCollection<ServiceBindingItem> _additionalServices = [];
    
    // UI State
    private bool _isLoading = false;
    private bool _isEditMode = false;
    private string _pageTitle = "Create New Project";
    private string _pageSubtitle = "Set up your development project with the right services and configuration";
    private string _actionButtonText = "Create Project";
    private string _actionButtonIcon = "➕";
    private bool _serviceBindingSectionVisible = true;
    
    // Validation
    private string _projectNameError = string.Empty;
    private string _projectPathError = string.Empty;
    private string _domainError = string.Empty;
    private bool _hasValidationErrors = false;
    
    // UI visibility properties
    private bool _isServiceBindingVisible = false;
    
    // Project being edited (if any)
    private Project? _editingProject;

    public CreateProjectViewModel(
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IServiceService serviceService,
        IProjectServiceRepository projectServiceRepository,
        IStackServiceRepository stackServiceRepository,
        ISSLCertificateService sslCertificateService,
        IPathConfigurationService pathConfigurationService,
        ProjectFormHandler projectFormHandler,
        ILogger<CreateProjectViewModel> logger,
        IProjectCreationService projectCreationService,
        IVirtualHostService virtualHostService,
        INavigationService navigationService,
        IDialogService dialogService,
        ServiceControlManager serviceControlManager)
    {
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _serviceService = serviceService;
        _projectServiceRepository = projectServiceRepository;
        _stackServiceRepository = stackServiceRepository;
        _sslCertificateService = sslCertificateService;
        _pathConfigurationService = pathConfigurationService;
        _projectFormHandler = projectFormHandler;
        _logger = logger;
        _projectCreationService = projectCreationService;
        _virtualHostService = virtualHostService;
        _navigationService = navigationService;
        _dialogService = dialogService;
        _serviceControlManager = serviceControlManager;
        
        // Initialize commands
        InitializeCommand = new AsyncRelayCommand(InitializeAsync);
        CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, CanCreateProject);
        CancelCommand = new RelayCommand(Cancel);
        BrowseProjectPathCommand = new AsyncRelayCommand(BrowseProjectPath);
        BrowseDocumentRootCommand = new AsyncRelayCommand(BrowseDocumentRoot);
        ApplyTemplateCommand = new RelayCommand<string>(ApplyTemplate);
        ApplyWordPressTemplateCommand = new RelayCommand(() => ApplyTemplate("wordpress"));
        ApplyLaravelTemplateCommand = new RelayCommand(() => ApplyTemplate("laravel"));
        ApplyReactTemplateCommand = new RelayCommand(() => ApplyTemplate("react"));
        ApplyStaticTemplateCommand = new RelayCommand(() => ApplyTemplate("static"));
        
        // Initialize collections
        InitializeServiceCollections();
    }

    #region Properties

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                ValidateProjectName();
                AutoGenerateFields();
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            if (SetProperty(ref _projectPath, value))
            {
                ValidateProjectPath();
                UpdateDocumentRootFromProjectPath();
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Domain
    {
        get => _domain;
        set
        {
            if (SetProperty(ref _domain, value))
            {
                ValidateDomain();
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string DocumentRoot
    {
        get => _documentRoot;
        set => SetProperty(ref _documentRoot, value);
    }

    public string ServerAliases
    {
        get => _serverAliases;
        set => SetProperty(ref _serverAliases, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public bool EnableSSL
    {
        get => _enableSSL;
        set => SetProperty(ref _enableSSL, value);
    }

    public bool CreateDirectory
    {
        get => _createDirectory;
        set => SetProperty(ref _createDirectory, value);
    }

    public bool UpdateHostsFile
    {
        get => _updateHostsFile;
        set => SetProperty(ref _updateHostsFile, value);
    }

    public bool CreateVirtualHost
    {
        get => _createVirtualHost;
        set => SetProperty(ref _createVirtualHost, value);
    }

    public bool NoStackSelected
    {
        get => _noStackSelected;
        set
        {
            if (SetProperty(ref _noStackSelected, value))
            {
                if (value)
                {
                    ExistingStackSelected = false;
                    IsServiceBindingVisible = true;
                    // Clear the selected stack when "No Stack" is chosen
                    // This ensures service bindings will be saved instead of stack reference
                    SelectedStack = null;
                }
            }
        }
    }

    public bool ExistingStackSelected
    {
        get => _existingStackSelected;
        set
        {
            if (SetProperty(ref _existingStackSelected, value))
            {
                if (value)
                {
                    NoStackSelected = false;
                    IsServiceBindingVisible = false;
                }
            }
        }
    }

    public StackItem? SelectedStack
    {
        get => _selectedStack;
        set => SetProperty(ref _selectedStack, value);
    }

    public ServiceBindingItem? SelectedWebServer
    {
        get => _selectedWebServer;
        set => SetProperty(ref _selectedWebServer, value);
    }

    public ServiceBindingItem? SelectedDatabase
    {
        get => _selectedDatabase;
        set => SetProperty(ref _selectedDatabase, value);
    }

    public ServiceBindingItem? SelectedPHPRuntime
    {
        get => _selectedPHPRuntime;
        set => SetProperty(ref _selectedPHPRuntime, value);
    }

    public ServiceBindingItem? SelectedAdditionalService
    {
        get => _selectedAdditionalService;
        set => SetProperty(ref _selectedAdditionalService, value);
    }

    public ObservableCollection<StackItem> AvailableStacks
    {
        get => _availableStacks;
        set => SetProperty(ref _availableStacks, value);
    }

    public ObservableCollection<ServiceBindingItem> WebServerServices
    {
        get => _webServerServices;
        set => SetProperty(ref _webServerServices, value);
    }

    public ObservableCollection<ServiceBindingItem> DatabaseServices
    {
        get => _databaseServices;
        set => SetProperty(ref _databaseServices, value);
    }

    public ObservableCollection<ServiceBindingItem> PHPRuntimeServices
    {
        get => _phpRuntimeServices;
        set => SetProperty(ref _phpRuntimeServices, value);
    }

    public ObservableCollection<ServiceBindingItem> AdditionalServices
    {
        get => _additionalServices;
        set => SetProperty(ref _additionalServices, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    public string PageSubtitle
    {
        get => _pageSubtitle;
        set => SetProperty(ref _pageSubtitle, value);
    }

    public string ActionButtonText
    {
        get => _actionButtonText;
        set => SetProperty(ref _actionButtonText, value);
    }

    public string ActionButtonIcon
    {
        get => _actionButtonIcon;
        set => SetProperty(ref _actionButtonIcon, value);
    }

    public bool ServiceBindingSectionVisible
    {
        get => _serviceBindingSectionVisible;
        set => SetProperty(ref _serviceBindingSectionVisible, value);
    }

    // Validation Properties
    public string ProjectNameError
    {
        get => _projectNameError;
        set => SetProperty(ref _projectNameError, value);
    }

    public string ProjectPathError
    {
        get => _projectPathError;
        set => SetProperty(ref _projectPathError, value);
    }

    public string DomainError
    {
        get => _domainError;
        set => SetProperty(ref _domainError, value);
    }

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        set => SetProperty(ref _hasValidationErrors, value);
    }

    /// <summary>
    /// Controls visibility of the service binding section when "No stack" is selected
    /// </summary>
    public bool IsServiceBindingVisible
    {
        get => _isServiceBindingVisible;
        set => SetProperty(ref _isServiceBindingVisible, value);
    }

    #endregion

    #region Commands

    public AsyncRelayCommand InitializeCommand { get; }
    public AsyncRelayCommand CreateProjectCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand BrowseProjectPathCommand { get; }
    public AsyncRelayCommand BrowseDocumentRootCommand { get; }
    public RelayCommand<string> ApplyTemplateCommand { get; }
    public RelayCommand ApplyWordPressTemplateCommand { get; }
    public RelayCommand ApplyLaravelTemplateCommand { get; }
    public RelayCommand ApplyReactTemplateCommand { get; }
    public RelayCommand ApplyStaticTemplateCommand { get; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialize the ViewModel for creating a new project
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            
            await LoadDataAsync();          // 1) load stacks/services

            EnsureDefaultSelections();      // 2) pick the "None" items after load

            if (IsEditMode && _editingProject != null)
            {
                await PopulateFormForEditAsync();  // 3) only now set edit selections
            }
            else
            {
                NoStackSelected = true;     // show service binding by default for create
                IsServiceBindingVisible = true; // explicitly set visibility for initial load
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing CreateProjectViewModel");
            ModernDialogService.ShowError("Failed to initialize project creation: " + ex.Message, "Initialization Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void EnsureDefaultSelections()
    {
        SelectedWebServer ??= WebServerServices.FirstOrDefault();
        SelectedDatabase ??= DatabaseServices.FirstOrDefault();
        SelectedPHPRuntime ??= PHPRuntimeServices.FirstOrDefault();
        SelectedAdditionalService ??= AdditionalServices.FirstOrDefault();

        // Optional nudge to UI
        OnPropertyChanged(nameof(SelectedWebServer));
        OnPropertyChanged(nameof(SelectedDatabase));
        OnPropertyChanged(nameof(SelectedPHPRuntime));
        OnPropertyChanged(nameof(SelectedAdditionalService));
    }

    /// <summary>
    /// Initialize the ViewModel for editing an existing project
    /// </summary>
    public async Task InitializeForEditAsync(Project project)
    {
        _editingProject = project;
        IsEditMode = true;
        PageTitle = "Edit Project";
        PageSubtitle = "Modify your project settings and configuration";
        ActionButtonText = "Update Project";
        ActionButtonIcon = "💾";
        
        // Let InitializeAsync (called by Loaded) handle loading + PopulateFormForEditAsync
        await Task.CompletedTask;
    }

    #endregion

    #region Private Methods

    private void InitializeServiceCollections()
    {
        // Add "None" options to service collections
        WebServerServices.Add(new ServiceBindingItem { Id = -1, DisplayName = "None", ServiceType = "None" });
        DatabaseServices.Add(new ServiceBindingItem { Id = -1, DisplayName = "None", ServiceType = "None" });
        PHPRuntimeServices.Add(new ServiceBindingItem { Id = -1, DisplayName = "None", ServiceType = "None" });
        AdditionalServices.Add(new ServiceBindingItem { Id = -1, DisplayName = "None", ServiceType = "None" });
        
        // Set default selections
        SelectedWebServer = WebServerServices.FirstOrDefault();
        SelectedDatabase = DatabaseServices.FirstOrDefault();
        SelectedPHPRuntime = PHPRuntimeServices.FirstOrDefault();
        SelectedAdditionalService = AdditionalServices.FirstOrDefault();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            
            // Load stacks and services in parallel
            await Task.WhenAll(
                LoadAvailableStacksAsync(),
                LoadAvailableServicesAsync()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data for CreateProjectViewModel");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAvailableStacksAsync()
    {
        try
        {
            var stacks = await _stackRepository.GetAllAsync();
            
            AvailableStacks.Clear();
            foreach (var stack in stacks)
            {
                AvailableStacks.Add(new StackItem
                {
                    Id = stack.Id,
                    Name = stack.DisplayName,
                    Description = stack.Description ?? ""
                });
            }
            
            // Create default stack if none exist
            if (!AvailableStacks.Any())
            {
                var defaultStack = new Stack
                {
                    DisplayName = "Default Stack",
                    Description = "Default development stack",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                
                await _stackRepository.CreateAsync(defaultStack);
                
                AvailableStacks.Add(new StackItem
                {
                    Id = defaultStack.Id,
                    Name = defaultStack.DisplayName,
                    Description = defaultStack.Description
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available stacks");
        }
    }

    private async Task LoadAvailableServicesAsync()
    {
        try
        {
            var serviceDisplayItems = await _serviceService.GetInstalledServicesAsync();
            
            // Clear existing services (except "None" options)
            ClearServiceCollections();
            
            foreach (var service in serviceDisplayItems)
            {
                var displayName = service.Name.Contains(service.Version) ? 
                    service.Name : 
                    $"{service.Name} {service.Version}".Trim();
                
                var bindingItem = new ServiceBindingItem
                {
                    Id = service.Id,
                    Name = service.Name,
                    DisplayName = displayName,
                    ServiceType = service.Type.ToString(),
                    DefaultPort = service.Port,
                    Status = service.Status,
                    IsSelected = false
                };
                
                // Group services by type
                switch (service.Type)
                {
                    case ServiceType.Apache:
                    case ServiceType.Nginx:
                        WebServerServices.Add(bindingItem);
                        break;
                        
                    case ServiceType.MySQL:
                    case ServiceType.PostgreSQL:
                    case ServiceType.MongoDB:
                    case ServiceType.MariaDB:
                        DatabaseServices.Add(bindingItem);
                        break;
                        
                    case ServiceType.PHP:
                        PHPRuntimeServices.Add(bindingItem);
                        break;
                        
                    default:
                        AdditionalServices.Add(bindingItem);
                        break;
                }
            }
            
            EnsureDefaultSelections(); // ✅ make sure something is selected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available services");
        }
    }

    private void ClearServiceCollections()
    {
        // Keep "None" options, remove others
        var webServerNone = WebServerServices.FirstOrDefault(s => s.Id == -1);
        var databaseNone = DatabaseServices.FirstOrDefault(s => s.Id == -1);
        var phpNone = PHPRuntimeServices.FirstOrDefault(s => s.Id == -1);
        var additionalNone = AdditionalServices.FirstOrDefault(s => s.Id == -1);
        
        WebServerServices.Clear();
        DatabaseServices.Clear();
        PHPRuntimeServices.Clear();
        AdditionalServices.Clear();
        
        if (webServerNone != null) WebServerServices.Add(webServerNone);
        if (databaseNone != null) DatabaseServices.Add(databaseNone);
        if (phpNone != null) PHPRuntimeServices.Add(phpNone);
        if (additionalNone != null) AdditionalServices.Add(additionalNone);
    }

    private async Task PopulateFormForEditAsync()
    {
        if (_editingProject == null) return;

        try
        {
            // Reload project to ensure Configuration is loaded
            var projectWithConfig = await _projectRepository.GetByIdAsync(_editingProject.Id);
            if (projectWithConfig == null)
            {
                _logger.LogError("Project with ID {ProjectId} not found when reloading", _editingProject.Id);
                return;
            }
            
            ProjectName = projectWithConfig.Name;
            ProjectPath = projectWithConfig.Path;
            Domain = projectWithConfig.Domain ?? "";
            DocumentRoot = projectWithConfig.DocumentRoot ?? projectWithConfig.Path;
            EnableSSL = projectWithConfig.EnableSSL;
            
            // Load server aliases from Configuration dictionary
            if (projectWithConfig.Configuration != null && projectWithConfig.Configuration.ContainsKey("ServerAliases"))
            {
                var serverAliasesValue = projectWithConfig.Configuration["ServerAliases"];
                if (serverAliasesValue != null)
                {
                    ServerAliases = serverAliasesValue.ToString() ?? "";
                }
                else
                {
                    ServerAliases = "";
                }
            }
            else
            {
                ServerAliases = "";
            }
            
            // Select the appropriate stack and set service binding visibility
            if (projectWithConfig.StackId.HasValue)
            {
                // Project is bound to a stack - hide service binding section
                SelectedStack = AvailableStacks.FirstOrDefault(s => s.Id == projectWithConfig.StackId.Value);
                if (SelectedStack != null)
                {
                    ExistingStackSelected = true; // This will set IsServiceBindingVisible = false
                }
            }
            else
            {
                // Project is not bound to a stack - show service binding section
                SelectedStack = null; // Explicitly clear any previously selected stack
                NoStackSelected = true; // This will set IsServiceBindingVisible = true
                
                // Force visibility update even if NoStackSelected was already true
                if (_noStackSelected)
                {
                    ExistingStackSelected = false;
                    IsServiceBindingVisible = true;
                }
                
                await LoadExistingServiceBindingsAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating form for edit");
        }
    }

    private async Task LoadExistingServiceBindingsAsync()
    {
        if (_editingProject == null) return;

        try
        {
            var existingBindings = await _projectServiceRepository.GetByProjectIdAsync(_editingProject.Id);
            
            // Reset selections to "None"
            SelectedWebServer = WebServerServices.FirstOrDefault(s => s.Id == -1);
            SelectedDatabase = DatabaseServices.FirstOrDefault(s => s.Id == -1);
            SelectedPHPRuntime = PHPRuntimeServices.FirstOrDefault(s => s.Id == -1);
            SelectedAdditionalService = AdditionalServices.FirstOrDefault(s => s.Id == -1);

            // Set selections based on existing bindings
            foreach (var binding in existingBindings)
            {
                if (binding.Service == null) 
                {
                    continue;
                }

                // Find existing service in collections or create new one if not found
                ServiceBindingItem? targetService = null;

                switch (binding.Service.ServiceType)
                {
                    case ServiceType.Apache:
                    case ServiceType.Nginx:
                        targetService = WebServerServices.FirstOrDefault(s => s.Id == binding.Service.Id);
                        if (targetService == null)
                        {
                            // Create new service item and add to collection
                            var displayName = binding.Service.Name.Contains(binding.Service.Version) ? 
                                binding.Service.Name : 
                                $"{binding.Service.Name} {binding.Service.Version}".Trim();
                            
                            targetService = new ServiceBindingItem
                            {
                                Id = binding.Service.Id,
                                Name = binding.Service.Name,
                                DisplayName = displayName,
                                ServiceType = binding.Service.ServiceType.ToString(),
                                DefaultPort = binding.Service.DefaultPort ?? 0,
                                Status = "Bound",
                                IsSelected = true
                            };
                            
                            WebServerServices.Add(targetService);
                        }
                        SelectedWebServer = targetService;
                        break;
                        
                    case ServiceType.MySQL:
                    case ServiceType.MariaDB:
                    case ServiceType.PostgreSQL:
                    case ServiceType.MongoDB:
                        targetService = DatabaseServices.FirstOrDefault(s => s.Id == binding.Service.Id);
                        if (targetService == null)
                        {
                            // Create new service item and add to collection
                            var displayName = binding.Service.Name.Contains(binding.Service.Version) ? 
                                binding.Service.Name : 
                                $"{binding.Service.Name} {binding.Service.Version}".Trim();
                            
                            targetService = new ServiceBindingItem
                            {
                                Id = binding.Service.Id,
                                Name = binding.Service.Name,
                                DisplayName = displayName,
                                ServiceType = binding.Service.ServiceType.ToString(),
                                DefaultPort = binding.Service.DefaultPort ?? 0,
                                Status = "Bound",
                                IsSelected = true
                            };
                            
                            DatabaseServices.Add(targetService);
                        }
                        SelectedDatabase = targetService;
                        break;
                        
                    case ServiceType.PHP:
                        targetService = PHPRuntimeServices.FirstOrDefault(s => s.Id == binding.Service.Id);
                        if (targetService == null)
                        {
                            // Create new service item and add to collection
                            var displayName = binding.Service.Name.Contains(binding.Service.Version) ? 
                                binding.Service.Name : 
                                $"{binding.Service.Name} {binding.Service.Version}".Trim();
                            
                            targetService = new ServiceBindingItem
                            {
                                Id = binding.Service.Id,
                                Name = binding.Service.Name,
                                DisplayName = displayName,
                                ServiceType = binding.Service.ServiceType.ToString(),
                                DefaultPort = binding.Service.DefaultPort ?? 0,
                                Status = "Bound",
                                IsSelected = true
                            };
                            
                            PHPRuntimeServices.Add(targetService);
                        }
                        SelectedPHPRuntime = targetService;
                        break;
                        
                    default:
                        targetService = AdditionalServices.FirstOrDefault(s => s.Id == binding.Service.Id);
                        if (targetService == null)
                        {
                            // Create new service item and add to collection
                            var displayName = binding.Service.Name.Contains(binding.Service.Version) ? 
                                binding.Service.Name : 
                                $"{binding.Service.Name} {binding.Service.Version}".Trim();
                            
                            targetService = new ServiceBindingItem
                            {
                                Id = binding.Service.Id,
                                Name = binding.Service.Name,
                                DisplayName = displayName,
                                ServiceType = binding.Service.ServiceType.ToString(),
                                DefaultPort = binding.Service.DefaultPort ?? 0,
                                Status = "Bound",
                                IsSelected = true
                            };
                            
                            AdditionalServices.Add(targetService);
                        }
                        SelectedAdditionalService = targetService;
                        break;
                }
            }
            
            // Force UI refresh to ensure selections are visible
            OnPropertyChanged(nameof(SelectedWebServer));
            OnPropertyChanged(nameof(SelectedDatabase));
            OnPropertyChanged(nameof(SelectedPHPRuntime));
            OnPropertyChanged(nameof(SelectedAdditionalService));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing service bindings");
        }
    }

    #endregion

    #region Validation

    private void ValidateProjectName()
    {
        ProjectNameError = string.IsNullOrWhiteSpace(ProjectName) ? "Project name is required" : string.Empty;
        UpdateValidationState();
    }

    private void ValidateProjectPath()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
        {
            ProjectPathError = "Project path is required";
        }
        else if (ProjectPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            ProjectPathError = "Project path contains invalid characters";
        }
        else
        {
            ProjectPathError = string.Empty;
        }
        UpdateValidationState();
    }

    private void ValidateDomain()
    {
        if (!string.IsNullOrWhiteSpace(Domain))
        {
            // Basic domain validation
            if (Domain.Contains(' ') || !Domain.Contains('.'))
            {
                DomainError = "Domain must contain a dot (e.g., example.local)";
            }
            else if (Domain.StartsWith('.') || Domain.EndsWith('.'))
            {
                DomainError = "Domain cannot start or end with a dot";
            }
            else if (Domain.Contains(".."))
            {
                DomainError = "Domain cannot contain consecutive dots";
            }
            else if (Domain.Length < 3)
            {
                DomainError = "Domain must be at least 3 characters long";
            }
            else
            {
                DomainError = string.Empty;
            }
        }
        else
        {
            DomainError = string.Empty;
        }
        UpdateValidationState();
    }

    private void UpdateValidationState()
    {
        HasValidationErrors = !string.IsNullOrEmpty(ProjectNameError) ||
                             !string.IsNullOrEmpty(ProjectPathError) ||
                             !string.IsNullOrEmpty(DomainError);
    }

    #endregion

    #region Auto-Generation

    private void AutoGenerateFields()
    {
        if (IsEditMode || string.IsNullOrWhiteSpace(ProjectName))
            return;

        var cleanName = GenerateCleanProjectName(ProjectName);
        
        // Auto-generate Domain
        if (string.IsNullOrWhiteSpace(Domain))
        {
            Domain = $"{cleanName}.local";
        }
        
        // Auto-generate Project Path
        if (string.IsNullOrWhiteSpace(ProjectPath))
        {
            ProjectPath = _pathConfigurationService.GenerateProjectPath(cleanName);
        }
        
        // Auto-generate Document Root
        if (string.IsNullOrWhiteSpace(DocumentRoot))
        {
            DocumentRoot = SuggestDocumentRoot(ProjectName, ProjectPath);
        }
    }

    private void UpdateDocumentRootFromProjectPath()
    {
        if (IsEditMode || string.IsNullOrWhiteSpace(ProjectPath))
            return;
            
        if (string.IsNullOrWhiteSpace(DocumentRoot) || DocumentRoot.Equals(ProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            DocumentRoot = ProjectPath;
        }
    }

    private static string GenerateCleanProjectName(string projectName)
    {
        var cleanName = projectName.ToLowerInvariant();
        cleanName = InvalidCharacterPattern().Replace(cleanName, "-");
        cleanName = MultipleHyphensPattern().Replace(cleanName, "-");
        cleanName = cleanName.Trim('-');
        return string.IsNullOrEmpty(cleanName) ? "project" : cleanName;
    }
    
    private static string SuggestDocumentRoot(string projectName, string projectPath)
    {
        var nameLower = projectName.ToLowerInvariant();
        
        if (nameLower.Contains("laravel") || nameLower.Contains("symfony") || 
            nameLower.Contains("api") || nameLower.Contains("app"))
        {
            return Path.Combine(projectPath, "public");
        }
        
        if (nameLower.Contains("react") || nameLower.Contains("vue") || 
            nameLower.Contains("angular") || nameLower.Contains("frontend"))
        {
            return Path.Combine(projectPath, "dist");
        }
        
        return projectPath;
    }

    #endregion

    #region Command Implementations

    private bool CanCreateProject()
    {
        return !HasValidationErrors && 
               !string.IsNullOrWhiteSpace(ProjectName) && 
               !string.IsNullOrWhiteSpace(ProjectPath) &&
               !IsLoading;
    }

    private async Task CreateProjectAsync()
    {
        try
        {
            IsLoading = true;
            
            // Create project creation request
            var request = new ProjectCreationRequest
            {
                Name = ProjectName,
                Path = ProjectPath,
                Domain = Domain,
                DocumentRoot = DocumentRoot,
                ServerAliases = ServerAliases,
                Port = SelectedWebServer?.DefaultPort > 0 ? SelectedWebServer.DefaultPort : 80,
                EnableSSL = EnableSSL,
                CreateDirectory = CreateDirectory,
                UpdateHostsFile = UpdateHostsFile,
                CreateVirtualHost = CreateVirtualHost,
                StackId = SelectedStack?.Id,
                WebServerId = SelectedWebServer?.Id > 0 ? SelectedWebServer.Id : null,
                DatabaseId = SelectedDatabase?.Id > 0 ? SelectedDatabase.Id : null,
                PHPRuntimeId = SelectedPHPRuntime?.Id > 0 ? SelectedPHPRuntime.Id : null,
                AdditionalServiceId = SelectedAdditionalService?.Id > 0 ? SelectedAdditionalService.Id : null
            };

            ProjectCreationResult result;
            
            if (IsEditMode && _editingProject != null)
            {
                var updateRequest = new ProjectUpdateRequest
                {
                    ProjectId = _editingProject.Id,
                    Name = request.Name,
                    Path = request.Path,
                    Domain = request.Domain,
                    DocumentRoot = request.DocumentRoot,
                    ServerAliases = request.ServerAliases,
                    Port = request.Port,
                    EnableSSL = request.EnableSSL,
                    CreateDirectory = request.CreateDirectory,
                    UpdateHostsFile = request.UpdateHostsFile,
                    CreateVirtualHost = request.CreateVirtualHost,
                    StackId = request.StackId,
                    WebServerId = request.WebServerId,
                    DatabaseId = request.DatabaseId,
                    PHPRuntimeId = request.PHPRuntimeId,
                    AdditionalServiceId = request.AdditionalServiceId
                };
                
                LogService.Info($"Calling UpdateProjectAsync for project: {ProjectName}");
                
                result = await _projectCreationService.UpdateProjectAsync(updateRequest);
                LogService.Info($"UpdateProjectAsync completed. Success: {result.Success}, Message: {result.Message}");
            }
            else
            {
                LogService.Info($"Calling CreateProjectAsync for project: {ProjectName}");
                
                result = await _projectCreationService.CreateProjectAsync(request);
                LogService.Info($"CreateProjectAsync completed. Success: {result.Success}, Message: {result.Message}");
            }

            if (result.Success)
            {
                var action = IsEditMode ? "updated" : "created";
                LogService.Info($"Project {action} successfully: {ProjectName}");
                LogService.Info($"About to show success dialog for project: {ProjectName}");
                
                // Get the project ID from result or editing project
                var projectId = result.Project?.Id ?? _editingProject?.Id;
                if (projectId.HasValue)
                {
                    // Restart bound services or stack services
                    ScheduleServiceRestart(projectId.Value);
                }
                
                // Stop loading indicator before showing dialog
                IsLoading = false;
                CreateProjectCommand.NotifyCanExecuteChanged();
                
                // Show success notification
                Services.NotificationService.Instance.ShowSuccess(
                    result.Message ?? $"Project '{ProjectName}' {action} successfully!",
                    $"Project {(IsEditMode ? "Updated" : "Created")}");
                
                // If creating a new project, navigate to edit mode
                if (!IsEditMode && result.Project != null)
                {
                    LogService.Info($"New project created, navigating to edit page for project: {result.Project.Name}");
                    
                    // Navigate to edit page via MainWindow
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            mainWindow.EditProjectButton_Click(result.Project);
                        });
                    }
                }
                else if (IsEditMode)
                {
                    LogService.Info($"Project updated, staying on edit page");
                    // Stay on the edit page after updating
                }
            }
            else
            {
                var action = IsEditMode ? "update" : "creation";
                LogService.Warning($"Project {action} failed: {result.Message}");
                LogService.Info($"About to show error dialog for project: {ProjectName}");
                
                // Stop loading indicator before showing dialog
                IsLoading = false;
                CreateProjectCommand.NotifyCanExecuteChanged();
                
                // Show error notification
                Services.NotificationService.Instance.ShowError(
                    result.Message ?? $"Project {action} failed",
                    $"Project {(IsEditMode ? "Update" : "Creation")} Failed");
            }
        }
        catch (Exception ex)
        {
            var action = IsEditMode ? "updating" : "creating";
            _logger.LogError(ex, "Error {Action} project: {ProjectName}", action, ProjectName);
            _logger.LogError(ex, "Exception details");
            
            // Stop loading indicator before showing dialog
            IsLoading = false;
            CreateProjectCommand.NotifyCanExecuteChanged();
            
            // Show error notification
            Services.NotificationService.Instance.ShowError(
                $"Error {(IsEditMode ? "updating" : "creating")} project: {ex.Message}",
                "Project Error");
        }
        finally
        {
            IsLoading = false;
            // Refresh command state to re-enable the button
            CreateProjectCommand.NotifyCanExecuteChanged();
        }
    }

    private void Cancel()
    {
        _navigationService.NavigateToProjectManagement();
    }

    #region Service Restart

    private void ScheduleServiceRestart(long projectId)
    {
        CancellationTokenSource newCts;

        lock (_serviceRestartSync)
        {
            if (_serviceRestartCts != null)
            {
                _serviceRestartCts.Cancel();
            }

            newCts = new CancellationTokenSource();
            _serviceRestartCts = newCts;
            var token = newCts.Token;

            _serviceRestartTask = Task.Run(async () =>
            {
                try
                {
                    await RestartProjectServicesAsync(projectId, token);
                }
                catch (OperationCanceledException)
                {
                    LogService.Info($"Service restart cancelled for project ID: {projectId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while restarting services for project ID {ProjectId}", projectId);
                }
                finally
                {
                    lock (_serviceRestartSync)
                    {
                        if (_serviceRestartCts == newCts)
                        {
                            _serviceRestartCts.Dispose();
                            _serviceRestartCts = null;
                            _serviceRestartTask = null;
                        }
                    }
                }
            }, token);
        }

    }

    /// <summary>
    /// Restarts services bound to the project or from the project's stack
    /// </summary>
    private async Task RestartProjectServicesAsync(long projectId, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            LogService.Info($"Restarting running services for project ID: {projectId}");

            if (!cancellationToken.IsCancellationRequested)
            {
                MainWindow.UpdateStatusBar($"Restarting running services for project '{ProjectName}'...");
            }

            var project = await _projectRepository.GetByIdAsync(projectId);
            cancellationToken.ThrowIfCancellationRequested();
            if (project == null)
            {
                LogService.Warning($"Project with ID {projectId} not found");
                if (!cancellationToken.IsCancellationRequested)
                {
                    MainWindow.ResetStatusBar();
                }
                return;
            }

            List<long> serviceIdsToRestart = [];

            if (project.StackId.HasValue)
            {
                LogService.Info($"Project bound to stack ID: {project.StackId.Value}");
                var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                cancellationToken.ThrowIfCancellationRequested();
                serviceIdsToRestart.AddRange(stackServices
                    .Where(ss => ss.IsEnabled)
                    .Select(ss => ss.ServiceId));
                LogService.Info($"Found {serviceIdsToRestart.Count} services from stack");
            }
            else
            {
                LogService.Info("Project not bound to stack, getting services from ProjectService bindings");
                var projectServices = await _projectServiceRepository.GetByProjectIdAsync(projectId);
                cancellationToken.ThrowIfCancellationRequested();
                serviceIdsToRestart.AddRange(projectServices
                    .Where(ps => ps.IsEnabled)
                    .Select(ps => ps.ServiceId));
                LogService.Info($"Found {serviceIdsToRestart.Count} services from ProjectService bindings");
            }

            if (serviceIdsToRestart.Count == 0)
            {
                LogService.Info("No services to restart for this project");
                if (!cancellationToken.IsCancellationRequested)
                {
                    MainWindow.ResetStatusBar();
                }
                return;
            }

            LogService.Info($"Found {serviceIdsToRestart.Count} enabled service(s). Will restart only those that are currently running.");

            var restartedCount = 0;
            var failedCount = 0;
            var totalServices = serviceIdsToRestart.Count;

            for (var i = 0; i < serviceIdsToRestart.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var serviceId = serviceIdsToRestart[i];
                var currentServiceIndex = i + 1;

                try
                {
                    var service = await _serviceService.GetServiceByIdAsync(serviceId);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (service == null)
                    {
                        LogService.Warning($"Service with ID {serviceId} not found in repository");
                        failedCount++;
                        continue;
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainWindow.UpdateStatusBar($"Restarting services ({currentServiceIndex}/{totalServices}): {service.Name}...");
                    }

                    if (!service.IsInstalled)
                    {
                        LogService.Info($"Service {service.Name} is not installed, skipping");
                        continue;
                    }

                    int? port = null;
                    if (project.StackId.HasValue)
                    {
                        var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                        cancellationToken.ThrowIfCancellationRequested();
                        var stackService = stackServices.FirstOrDefault(ss => ss.ServiceId == serviceId);
                        port = stackService?.Port;
                    }

                    var serviceDisplayItem = ConvertToServiceDisplayItem(service, port);

                    var currentStatus = await _serviceControlManager.DetectServiceStatusAsync(serviceDisplayItem);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!string.Equals(currentStatus, "Running", StringComparison.OrdinalIgnoreCase))
                    {
                        LogService.Info($"Service {service.Name} is not running (status: {currentStatus}), skipping restart");
                        continue;
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainWindow.UpdateStatusBar($"Stopping {service.Name}... ({currentServiceIndex}/{totalServices})");
                    }

                    LogService.Info($"Stopping service {service.Name} before restart");
                    await _serviceControlManager.StopServiceAsync(serviceDisplayItem);
                    await Task.Delay(1000, cancellationToken);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainWindow.UpdateStatusBar($"Starting {service.Name}... ({currentServiceIndex}/{totalServices})");
                    }

                    LogService.Info($"Starting service {service.Name}");
                    await _serviceControlManager.StartServiceAsync(serviceDisplayItem);
                    restartedCount++;

                    LogService.Info($"Successfully restarted service: {service.Name}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Failed to restart service ID {ServiceId}", serviceId);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (failedCount == 0)
                {
                    MainWindow.UpdateStatusBar($"Successfully restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")} for '{ProjectName}'");
                    Services.NotificationService.Instance.ShowSuccess(
                        $"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")} for project '{ProjectName}'",
                        "Services Restarted");
                }
                else
                {
                    MainWindow.UpdateStatusBar($"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")}, {failedCount} failed");
                    Services.NotificationService.Instance.ShowWarning(
                        $"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")}, {failedCount} failed",
                        "Service Restart");
                }

                _ = Task.Delay(3000).ContinueWith(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Application.Current.Dispatcher.Invoke(() => MainWindow.ResetStatusBar());
                    }
                }, TaskScheduler.Default);
            });
        }
        catch (OperationCanceledException)
        {
            LogService.Info($"Service restart operation cancelled for project ID: {projectId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting project services");
            if (!cancellationToken.IsCancellationRequested)
            {
                MainWindow.UpdateStatusBar($"Error restarting services: {ex.Message}");
                await Task.Delay(3000);
                MainWindow.ResetStatusBar();
            }
        }
    }
    
    /// <summary>
    /// Converts a ServiceDefinition to a ServiceDisplayItem for use with ServiceControlManager
    /// </summary>
    private static ServiceDisplayItem ConvertToServiceDisplayItem(ServiceDefinition service, int? port = null)
    {
        var serviceStatus = (Common.ServiceStatus)service.Status;
        var statusString = serviceStatus == Common.ServiceStatus.Unknown 
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
            Port = port ?? service.DefaultPort ?? 0,
            Path = service.BinaryPath ?? string.Empty,
            AutoStart = service.AutoStart
        };
    }

    #endregion

    private async Task BrowseProjectPath()
    {
        var initialDirectory = !string.IsNullOrEmpty(ProjectPath) 
            ? ProjectPath 
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            
        var selectedPath = await _dialogService.SelectFolderAsync("Select Project Directory", initialDirectory);
        if (!string.IsNullOrEmpty(selectedPath))
        {
            ProjectPath = selectedPath;
        }
    }

    private async Task BrowseDocumentRoot()
    {
        var initialDirectory = !string.IsNullOrEmpty(DocumentRoot) 
            ? DocumentRoot 
            : !string.IsNullOrEmpty(ProjectPath) 
                ? ProjectPath 
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                
        var selectedPath = await _dialogService.SelectFolderAsync("Select Document Root Directory", initialDirectory);
        if (!string.IsNullOrEmpty(selectedPath))
        {
            DocumentRoot = selectedPath;
        }
    }

    private void ApplyTemplate(string? templateName)
    {
        if (string.IsNullOrEmpty(templateName)) return;

        switch (templateName.ToLowerInvariant())
        {
            case "wordpress":
                ApplyWordPressTemplate();
                break;
            case "laravel":
                ApplyLaravelTemplate();
                break;
            case "react":
                ApplyReactTemplate();
                break;
            case "static":
                ApplyStaticTemplate();
                break;
        }
    }

    private void ApplyWordPressTemplate()
    {
        ProjectName = "wordpress-site";
        ProjectPath = _pathConfigurationService.GenerateProjectPath("wordpress-site");
        Domain = "wordpress.local";
        ServerAliases = "www.wordpress.local\nadmin.wordpress.local\nwp.wordpress.local";
        ExistingStackSelected = true;
        SelectedStack = AvailableStacks.FirstOrDefault(s => s.Id == 1);
        CreateVirtualHost = true;
        CreateDirectory = true;
    }

    private void ApplyLaravelTemplate()
    {
        ProjectName = "laravel-app";
        ProjectPath = _pathConfigurationService.GenerateProjectPath("laravel-app");
        Domain = "laravel.local";
        ServerAliases = "www.laravel.local\nadmin.laravel.local\napi.laravel.local";
        ExistingStackSelected = true;
        SelectedStack = AvailableStacks.FirstOrDefault(s => s.Id == 1);
        CreateVirtualHost = true;
        CreateDirectory = true;
    }

    private void ApplyReactTemplate()
    {
        ProjectName = "react-app";
        ProjectPath = _pathConfigurationService.GenerateProjectPath("react-app");
        Domain = "react.local";
        ServerAliases = "www.react.local\napp.react.local";
        NoStackSelected = true;
        CreateVirtualHost = true;
        CreateDirectory = true;
    }

    private void ApplyStaticTemplate()
    {
        ProjectName = "static-site";
        ProjectPath = _pathConfigurationService.GenerateProjectPath("static-site");
        Domain = "static.local";
        ServerAliases = "www.static.local";
        ExistingStackSelected = true;
        SelectedStack = AvailableStacks.FirstOrDefault(s => s.Id == 1);
        CreateVirtualHost = true;
        CreateDirectory = true;
    }

    #endregion

    // ✅ All events removed - using IDialogService and INavigationService directly

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Represents a service that can be bound to a project
/// </summary>
public class ServiceBindingItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public int DefaultPort { get; set; }
    public string Status { get; set; } = "Unknown";
    public bool IsSelected { get; set; } = false;
    public bool IsRunning { get; set; } = false;
}

/// <summary>
/// Represents a stack item for UI binding
/// </summary>
public class StackItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

