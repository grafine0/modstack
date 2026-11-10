using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.Services;
using ModStack.Core.Interfaces;
using ModStack.Core.Services;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;

namespace ModStack.GUI.Views
{
    public partial class ExtensionsView : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly ObservableCollection<PHPExtensionViewModel> _allExtensions = [];
        private ObservableCollection<PHPExtensionViewModel> _extensions = [];
        public ObservableCollection<PHPExtensionViewModel> Extensions
        {
            get => _extensions;
            set => SetProperty(ref _extensions, value);
        }

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private readonly PHPExtensionManager _extensionManager;
        private readonly IServiceService _serviceService;
        private readonly IPHPExtensionRepository _extensionRepository;
        private readonly string _phpVersion = "8.3.0"; // TODO: Get from selected PHP service

        // Filter properties
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set 
            { 
                SetProperty(ref _searchText, value);
                FilterExtensions();
            }
        }

        private string _selectedCategory = "All Categories";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                SetProperty(ref _selectedCategory, value);
                FilterExtensions();
            }
        }

        private string _selectedStatus = "All Status";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set 
            { 
                SetProperty(ref _selectedStatus, value);
                FilterExtensions();
            }
        }

        // Filter collections
        public List<string> Categories { get; set; } = ["All Categories", "Database", "Image Processing", "Encryption", "Web Services", "Performance", "Other"];
        public List<string> StatusOptions { get; set; } = ["All Status", "Installed", "Not Installed", "Enabled", "Disabled"];

        public ExtensionsView(
            PHPExtensionManager extensionManager,
            IServiceService serviceService,
            IPHPExtensionRepository extensionRepository)
        {
            InitializeComponent();
            DataContext = this;
            
            _extensionManager = extensionManager;
            _serviceService = serviceService;
            _extensionRepository = extensionRepository;
            
            Loaded += ExtensionsView_Loaded;
        }

        private async void ExtensionsView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadExtensions();
        }

        private async Task LoadExtensions()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading extensions...";

                // Get PHP service using proper service layer
                var phpServices = await _serviceService.GetServicesByTypeAsync(ModStack.Common.ServiceType.PHP);
                System.Diagnostics.Debug.WriteLine($"🔍 Found {phpServices.Count()} PHP services from service layer:");
                foreach (var service in phpServices)
                {
                    System.Diagnostics.Debug.WriteLine($"  - ID: {service.Id}, Name: {service.Name}, BinaryPath: {service.BinaryPath}, IsInstalled: {service.IsInstalled}");
                }
                
                // Get only installed PHP services
                var installedPhpServices = phpServices.Where(s => s.IsInstalled).ToList();
                System.Diagnostics.Debug.WriteLine($"🔍 Found {installedPhpServices.Count} installed PHP services:");
                foreach (var service in installedPhpServices)
                {
                    System.Diagnostics.Debug.WriteLine($"  - ID: {service.Id}, Name: {service.Name}, BinaryPath: {service.BinaryPath}");
                }
                
                var phpService = installedPhpServices.FirstOrDefault();
                if (phpService == null)
                {
                    StatusMessage = "No installed PHP service found";
                    System.Diagnostics.Debug.WriteLine("❌ No installed PHP service found");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"🔍 LoadExtensions using PHP service: ID={phpService.Id}, Name={phpService.Name}, BinaryPath={phpService.BinaryPath}");

                // Normalize PHP version (8.3.0 -> 8.3)
                var normalizedVersion = NormalizePHPVersion(_phpVersion);

                // Initialize default extensions in database first
                StatusMessage = "Initializing extension database...";
                await _extensionRepository.InitializeDefaultExtensionsAsync();
                
                // Verify extensions were seeded
                var seededExtensions = await _extensionRepository.GetAvailableExtensionsAsync();
                System.Diagnostics.Debug.WriteLine($"✅ Default extensions initialized in database: {seededExtensions.Count} extensions found");

                // First synchronize with filesystem to detect existing extensions
                StatusMessage = "Synchronizing with filesystem...";
                var syncResult = await _extensionManager.SynchronizeExtensionsAsync(phpService.Id, normalizedVersion);
                if (!syncResult.Success)
                {
                    StatusMessage = $"Synchronization warning: {syncResult.Message}";
                    System.Diagnostics.Debug.WriteLine($"⚠️ Synchronization failed: {syncResult.Message}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Synchronization successful: {syncResult.Message}");
                }

                // Load available extensions
                StatusMessage = "Loading available extensions...";
                var available = await _extensionManager.GetAvailableExtensionsAsync(normalizedVersion);
                System.Diagnostics.Debug.WriteLine($"🔍 Found {available.Count} available extensions");

                // Load installed extensions
                StatusMessage = "Loading installed extensions...";
                var installed = await _extensionManager.GetInstalledExtensionsAsync(phpService.Id, normalizedVersion);
                System.Diagnostics.Debug.WriteLine($"🔍 Found {installed.Count} installed extensions");
                
                // Debug: List installed extensions
                foreach (var inst in installed)
                {
                    System.Diagnostics.Debug.WriteLine($"  ✅ Installed: {inst.Extension?.Name} (Enabled: {inst.IsEnabled})");
                }

                // Create view models with comprehensive installation check
                Extensions.Clear();
                var availableVMs = new List<PHPExtensionViewModel>();
                
                foreach (var ext in available)
                {
                    var installedExt = installed.FirstOrDefault(i => i.ExtensionId == ext.Id);
                    
                    // Check if extension is actually installed using PHPExtensionManager
                    var isActuallyInstalled = await _extensionManager.IsExtensionActuallyInstalledAsync(phpService.Id, ext.Id);
                    
                    availableVMs.Add(new PHPExtensionViewModel(ext)
                    {
                        IsInstalled = installedExt != null || isActuallyInstalled,
                        IsEnabled = installedExt?.IsEnabled ?? false
                    });
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Created {availableVMs.Count} view models");

                foreach (var vm in availableVMs)
                {
                    _allExtensions.Add(vm);
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Added {_allExtensions.Count} extensions to collection");
                FilterExtensions(); // Apply initial filter
                StatusMessage = $"Loaded {_allExtensions.Count} extensions ({installed.Count} installed)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading extensions: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"❌ Error loading extensions: {ex}");
                
                // Add some sample data for testing
                _allExtensions.Clear();
                _allExtensions.Add(new PHPExtensionViewModel
                {
                    Name = "gd",
                    Description = "Image creation and manipulation library",
                    Category = "Image Processing",
                    IsInstalled = true,
                    IsEnabled = true,
                    IsPopular = true,
                    IsBuiltIn = true
                });
                _allExtensions.Add(new PHPExtensionViewModel
                {
                    Name = "mysqli",
                    Description = "MySQL Improved Extension",
                    Category = "Database",
                    IsInstalled = false,
                    IsEnabled = false,
                    IsPopular = true,
                    IsBuiltIn = true
                });
                FilterExtensions(); // Apply filter to sample data
                System.Diagnostics.Debug.WriteLine($"🔍 Added {_allExtensions.Count} sample extensions for testing");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterExtensions()
        {
            try
            {
                var filtered = _allExtensions.AsEnumerable();

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(ext => 
                        ext.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        ext.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        ext.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }

                // Apply category filter
                if (SelectedCategory != "All Categories")
                {
                    filtered = filtered.Where(ext => ext.Category == SelectedCategory);
                }

                // Apply status filter
                if (SelectedStatus != "All Status")
                {
                    filtered = SelectedStatus switch
                    {
                        "Installed" => filtered.Where(ext => ext.IsInstalled),
                        "Not Installed" => filtered.Where(ext => !ext.IsInstalled),
                        "Enabled" => filtered.Where(ext => ext.IsInstalled && ext.IsEnabled),
                        "Disabled" => filtered.Where(ext => ext.IsInstalled && !ext.IsEnabled),
                        _ => filtered
                    };
                }

                // Update the displayed collection
                Extensions.Clear();
                foreach (var ext in filtered)
                {
                    Extensions.Add(ext);
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Filtered to {Extensions.Count} extensions (from {_allExtensions.Count} total)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error filtering extensions: {ex.Message}");
            }
        }

        private static string NormalizePHPVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "8.3";
            
            var parts = version.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }
            
            return version;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadExtensions();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate back to the previous view
                var navigationService = ServiceLocator.GetService<INavigationService>();
                navigationService.NavigateBack();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error navigating back: {ex.Message}";
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PHPExtensionViewModel extension)
            {
                try
                {
                    StatusMessage = $"Installing {extension.Name}...";
                    
                    // Get PHP service
                    var phpServices = await _serviceService.GetServicesByTypeAsync(ModStack.Common.ServiceType.PHP);
                    var phpService = phpServices.FirstOrDefault();
                    if (phpService == null)
                    {
                        StatusMessage = "No PHP service found";
                        return;
                    }

                    var normalizedVersion = NormalizePHPVersion(_phpVersion);
                    
                    // Create installation request
                    var request = new InstallPHPExtensionRequest
                    {
                        ServiceId = phpService.Id,
                        ExtensionId = extension.Id,
                        PHPVersion = normalizedVersion,
                        EnableAfterInstall = true
                    };

                    // Install the extension
                    var result = await _extensionManager.InstallExtensionAsync(request);
                    
                    if (result.Success)
                    {
                        extension.IsInstalled = true;
                        extension.IsEnabled = true;
                        StatusMessage = $"{extension.Name} installed successfully";
                    }
                    else
                    {
                        StatusMessage = $"Error installing {extension.Name}: {result.Message}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error installing {extension.Name}: {ex.Message}";
                }
            }
        }

        private async void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PHPExtensionViewModel extension)
            {
                try
                {
                    StatusMessage = $"{(extension.IsEnabled ? "Disabling" : "Enabling")} {extension.Name}...";
                    
                    // Get PHP service using proper service layer
                    var phpServices = await _serviceService.GetServicesByTypeAsync(ModStack.Common.ServiceType.PHP);
                    var installedPhpServices = phpServices.Where(s => s.IsInstalled).ToList();
                    var phpService = installedPhpServices.FirstOrDefault();
                    
                    if (phpService == null)
                    {
                        StatusMessage = "No installed PHP service found";
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"🔍 Using PHP service: ID={phpService.Id}, Name={phpService.Name}, BinaryPath={phpService.BinaryPath}");
                    System.Diagnostics.Debug.WriteLine($"🔍 Toggling extension: ID={extension.Id}, Name={extension.Name}, Enable={!extension.IsEnabled}");

                    // Toggle the extension
                    var result = await _extensionManager.ToggleExtensionAsync(phpService.Id, extension.Id, !extension.IsEnabled);
                    
                    if (result.Success)
                    {
                        extension.IsEnabled = !extension.IsEnabled;
                        StatusMessage = $"{extension.Name} {(extension.IsEnabled ? "enabled" : "disabled")}";
                        
                        // Refresh the extensions to update the UI
                        await LoadExtensions();
                    }
                    else
                    {
                        StatusMessage = $"Error toggling {extension.Name}: {result.Message}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error toggling {extension.Name}: {ex.Message}";
                }
            }
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PHPExtensionViewModel extension)
            {
                try
                {
                    StatusMessage = $"Removing {extension.Name}...";
                    
                    // Get PHP service
                    var phpServices = await _serviceService.GetServicesByTypeAsync(ModStack.Common.ServiceType.PHP);
                    var phpService = phpServices.FirstOrDefault();
                    if (phpService == null)
                    {
                        StatusMessage = "No PHP service found";
                        return;
                    }

                    // Remove the extension
                    var result = await _extensionManager.UninstallExtensionAsync(phpService.Id, extension.Id);
                    
                    if (result.Success)
                    {
                        extension.IsInstalled = false;
                        extension.IsEnabled = false;
                        StatusMessage = $"{extension.Name} removed successfully";
                    }
                    else
                    {
                        StatusMessage = $"Error removing {extension.Name}: {result.Message}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error removing {extension.Name}: {ex.Message}";
                }
            }
        }

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
}
