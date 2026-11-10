using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

using ModStack.GUI.Services;
using ModStack.Services;

namespace ModStack.GUI.ViewModels
{
    /// <summary>
    /// ViewModel for Apache Module Manager Dialog following MVVM pattern
    /// </summary>
    public class ApacheModuleManagerViewModel : INotifyPropertyChanged
    {
        private readonly IApacheModuleRepository _moduleRepository;
        private readonly ApacheModuleManager _moduleManager;
        private readonly ApacheConfigurationService _apacheConfigService;
        private readonly ILogger<ApacheModuleManagerViewModel> _logger;
        
        // Private fields for bindable properties
        private string _searchText = string.Empty;
        private string _categoryFilter = "All Categories";
        private string _statusFilter = "All Modules";
        private bool _isLoading = false;
        private string _loadingMessage = "Loading Apache modules...";
        private string _statusMessage = "Ready";
        private string _apacheVersion;

        // Collections
        public ObservableCollection<ApacheModuleViewModel> Modules { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new()
        {
            "All Modules", "Enabled", "Disabled", "Available", "Required"
        };

        // Collection view for filtering
        private ICollectionView? _filteredModulesView;
        public ICollectionView FilteredModules
        {
            get
            {
                if (_filteredModulesView == null)
                {
                    _filteredModulesView = CollectionViewSource.GetDefaultView(Modules);
                    _filteredModulesView.Filter = FilterModules;
                }
                return _filteredModulesView;
            }
        }

        // Bindable Properties
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilteredModules.Refresh();
                }
            }
        }

        public string CategoryFilter
        {
            get => _categoryFilter;
            set
            {
                if (SetProperty(ref _categoryFilter, value))
                {
                    FilteredModules.Refresh();
                }
            }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    FilteredModules.Refresh();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string ApacheVersion
        {
            get => _apacheVersion;
            set => SetProperty(ref _apacheVersion, value);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand SynchronizeCommand { get; }
        public ICommand ToggleModuleCommand { get; }
        public ICommand CloseCommand { get; }

        // Events
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? RequestClose;

        public ApacheModuleManagerViewModel(
            IApacheModuleRepository moduleRepository, 
            ApacheModuleManager moduleManager,
            ApacheConfigurationService apacheConfigService,
            ILogger<ApacheModuleManagerViewModel> logger)
        {
            _moduleRepository = moduleRepository;
            _moduleManager = moduleManager;
            _apacheConfigService = apacheConfigService;
            _logger = logger;

            // Get Apache version from configuration
            _apacheVersion = _apacheConfigService.GetApacheVersion();

            // Initialize commands
            RefreshCommand = new AsyncRelayCommand(async () => await LoadModulesAsync());
            SynchronizeCommand = new AsyncRelayCommand(async () => await SynchronizeModulesAsync());
            ToggleModuleCommand = new AsyncRelayCommand<ApacheModuleViewModel>(async (module) => await ToggleModuleAsync(module));
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));

            // Initialize categories
            Categories.Add("All Categories");
        }

        /// <summary>
        /// Initialize the ViewModel - call this after construction
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadModulesAsync();
        }

        private async Task LoadModulesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 LoadModulesAsync started - setting IsLoading = true");
                IsLoading = true;
                LoadingMessage = "Loading Apache modules...";
                System.Diagnostics.Debug.WriteLine($"📝 LoadingMessage set to: {LoadingMessage}");

                // Add a small delay to make loading visible
                await Task.Delay(500);

                _logger.LogInformation("Loading Apache modules from database...");

                // Get all modules from database
                var modules = await _moduleRepository.GetAllAsync();

                // If no modules found, try to initialize them
                if (!modules.Any())
                {
                    _logger.LogInformation("No modules found in database, attempting to initialize...");
                    LoadingMessage = "Initializing Apache modules...";
                    
                    // Find Apache installation
                    var apachePath = _moduleManager.GetApacheInstallationPath(ApacheVersion);
                    if (!string.IsNullOrEmpty(apachePath))
                    {
                        await _moduleManager.InitializeDefaultModulesAsync(apachePath, ApacheVersion);
                        modules = await _moduleRepository.GetAllAsync();
                        _logger.LogInformation("Initialized {Count} modules from Apache installation", modules.Count());
                    }
                    else
                    {
                        _logger.LogError("Apache installation not found for module initialization");
                        StatusMessage = "Apache installation not found. Click Synchronize to set up modules.";
                        return;
                    }
                }

                var moduleViewModels = modules.Select(CreateModuleViewModel).ToList();

                // Update UI collections
                Modules.Clear();
                foreach (var module in moduleViewModels)
                {
                    Modules.Add(module);
                }

                // Load categories for filter
                await LoadCategoriesAsync();

                StatusMessage = $"Loaded {Modules.Count} Apache modules";
                _logger.LogInformation("Loaded {Count} Apache modules", Modules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Apache modules");
                ModernDialogService.ShowError($"Error loading Apache modules: {ex.Message}", "Loading Error");
                StatusMessage = "Error loading modules";
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("🔄 LoadModulesAsync completed - setting IsLoading = false");
                IsLoading = false;
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _moduleRepository.GetCategoriesAsync();
                
                Categories.Clear();
                Categories.Add("All Categories");
                
                foreach (var category in categories.OrderBy(c => c))
                {
                    Categories.Add(category);
                }
                
                // Reset filters to defaults
                CategoryFilter = "All Categories";
                StatusFilter = "All Modules";
                
                _logger.LogDebug("Loaded {Count} categories, filters reset to defaults", categories.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
            }
        }

        private async Task SynchronizeModulesAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Synchronizing Apache modules...";

                // Find Apache installation
                var apachePath = _moduleManager.GetApacheInstallationPath(ApacheVersion);
                if (string.IsNullOrEmpty(apachePath))
                {
                    ModernDialogService.ShowError("Apache installation not found. Please check your installation.", "Apache Not Found");
                    return;
                }

                // First, initialize default modules if database is empty
                LoadingMessage = "Initializing Apache modules...";
                await _moduleManager.InitializeDefaultModulesAsync(apachePath, ApacheVersion);

                // Then synchronize with current state
                LoadingMessage = "Synchronizing module states...";
                var success = await _moduleManager.SynchronizeModulesAsync(apachePath, ApacheVersion);
                
                if (success)
                {
                    await LoadModulesAsync();
                    
                    // Show success toast notification
                    Services.NotificationService.Instance.ShowSuccess(
                        "Apache modules have been synchronized successfully",
                        "Modules Synchronized");
                }
                else
                {
                    ModernDialogService.ShowError("Failed to synchronize Apache modules. Check the logs for details.", "Synchronization Failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synchronizing modules");
                ModernDialogService.ShowError($"Error synchronizing modules: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleModuleAsync(ApacheModuleViewModel? module)
        {
            if (module == null) return;

            try
            {
                // Get Apache installation path
                var apachePath = _moduleManager.GetApacheInstallationPath(module.ApacheVersion);
                if (string.IsNullOrEmpty(apachePath))
                {
                    ModernDialogService.ShowError("Apache installation not found. Please check your installation.", "Apache Not Found");
                    return;
                }

                bool success;
                if (module.IsEnabled)
                {
                    success = await _moduleManager.DisableModuleAsync(module.Id, apachePath);
                    if (success)
                    {
                        module.IsEnabled = false;
                        StatusMessage = $"Disabled module: {module.Name}";
                    }
                }
                else
                {
                    success = await _moduleManager.EnableModuleAsync(module.Id, apachePath);
                    if (success)
                    {
                        module.IsEnabled = true;
                        StatusMessage = $"Enabled module: {module.Name}";
                    }
                }

                if (!success)
                {
                    ModernDialogService.ShowError($"Failed to {(module.IsEnabled ? "disable" : "enable")} module: {module.Name}", "Module Operation Failed");
                    StatusMessage = $"Failed to toggle module: {module.Name}";
                }
                else
                {
                    // Refresh the display
                    FilteredModules.Refresh();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling module {ModuleName}", module.Name);
                ModernDialogService.ShowError($"Error toggling module: {ex.Message}", "Error");
            }
        }

        private bool FilterModules(object item)
        {
            if (item is not ApacheModuleViewModel module) return false;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var fileNameMatches = !string.IsNullOrEmpty(module.FileName) && 
                                     module.FileName.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
                
                if (!module.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !module.ModuleName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !module.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !fileNameMatches)
                {
                    return false;
                }
            }

            // Category filter
            if (CategoryFilter != "All Categories" && module.Category != CategoryFilter)
            {
                return false;
            }

            // Status filter
            return StatusFilter switch
            {
                "Enabled" => module.IsEnabled,
                "Disabled" => !module.IsEnabled,
                "Available" => module.IsAvailable,
                "Required" => module.IsRequired,
                _ => true
            };
        }

        private ApacheModuleViewModel CreateModuleViewModel(ApacheModule module)
        {
            var viewModel = new ApacheModuleViewModel
            {
                Id = module.Id,
                Name = module.Name,
                ModuleName = module.ModuleName,
                FileName = module.FileName,
                Description = module.Description ?? "No description available",
                Category = module.Category ?? "Other",
                ApacheVersion = module.ApacheVersion ?? "Unknown"
            };
            
            // Set properties that have OnPropertyChanged to ensure notifications are triggered
            viewModel.IsEnabled = module.IsEnabled;
            viewModel.IsRequired = module.IsRequired;
            viewModel.IsAvailable = module.IsAvailable;
            
            return viewModel;
        }

        protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
