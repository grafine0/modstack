using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ModStack.Common.Models;
using ModStack.Services;
using ModStack.GUI.Services;
using ModStack.GUI.Views.Dialogs;
using ModStack.GUI.Views;
using ModStack.Common;
using ModStack.Data.Interfaces;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace ModStack.GUI.ViewModels
{
    /// <summary>
    /// ViewModel for Package Manager following MVVM pattern
    /// </summary>
    public class PackageManagerViewModel : INotifyPropertyChanged
    {
        private readonly PackageManagerService _packageManagerService;
        private readonly PackageUninstaller _packageUninstaller;
        private readonly IServiceRepository _serviceRepository;
        private readonly ILogger<PackageManagerViewModel> _logger;
        
        private List<AvailablePackage> _allPackages = [];
        private string _searchText = "";
        private string _selectedCategory = "All Categories";
        private string _selectedStatus = "All Status";
        private bool _isLoading;
        private string _loadingMessage = "";
        private string _statusMessage = "Ready";
        private int _totalPackages;
        private int _installedPackages;
        private bool _hasPackages;

        public PackageManagerViewModel(PackageManagerService packageManagerService, PackageUninstaller packageUninstaller, IServiceRepository serviceRepository, ILogger<PackageManagerViewModel> logger)
        {
            _packageManagerService = packageManagerService ?? throw new ArgumentNullException(nameof(packageManagerService));
            _packageUninstaller = packageUninstaller ?? throw new ArgumentNullException(nameof(packageUninstaller));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize collections
            Packages = [];
            Categories = ["All Categories", "Web Server", "Database", "Runtime", "Cache"];
            StatusOptions = ["All Status", "Installed", "Not Installed"];

            // Initialize filtered view
            FilteredPackages = CollectionViewSource.GetDefaultView(Packages);
            FilteredPackages.Filter = FilterPackages;

            // Initialize commands
            RefreshCommand = new AsyncRelayCommand(async () => await LoadPackagesAsync());
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            AddPackageCommand = new AsyncRelayCommand(async () => await AddPackageAsync());
            InstallCommand = new AsyncRelayCommand<PackageDisplayItem>(async (package) => await InstallPackageAsync(package));
            EditCommand = new AsyncRelayCommand<PackageDisplayItem>(async (package) => await EditPackageAsync(package));
            DeleteCommand = new AsyncRelayCommand<PackageDisplayItem>(async (package) => await DeletePackageAsync(package));
        }

        #region Properties

        public ObservableCollection<PackageDisplayItem> Packages { get; }
        public ICollectionView FilteredPackages { get; }
        public ObservableCollection<string> Categories { get; }
        public ObservableCollection<string> StatusOptions { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilteredPackages.Refresh();
                }
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    FilteredPackages.Refresh();
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
                    FilteredPackages.Refresh();
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

        public int TotalPackages
        {
            get => _totalPackages;
            set => SetProperty(ref _totalPackages, value);
        }

        public int InstalledPackages
        {
            get => _installedPackages;
            set => SetProperty(ref _installedPackages, value);
        }

        public bool HasPackages
        {
            get => _hasPackages;
            set => SetProperty(ref _hasPackages, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand AddPackageCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initialize the ViewModel - call this after construction
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadPackagesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error initializing PackageManagerViewModel: {ex.Message}");
            }
        }


        #endregion

        #region Private Methods

        private bool FilterPackages(object obj)
        {
            if (obj is not PackageDisplayItem pkg) return false;

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !pkg.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !pkg.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                return false;

            // Apply category filter
            if (SelectedCategory != "All Categories" && pkg.Category != SelectedCategory)
                return false;

            // Apply status filter
            if (SelectedStatus == "Installed" && !pkg.IsInstalled) return false;
            if (SelectedStatus == "Not Installed" && pkg.IsInstalled) return false;

            return true;
        }

        private void ClearFilters()
        {
            SearchText = "";
            SelectedCategory = "All Categories";
            SelectedStatus = "All Status";
        }

        private async Task LoadPackagesAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Loading packages...";

                System.Diagnostics.Debug.WriteLine("📦 Loading packages from database...");
                System.Diagnostics.Debug.WriteLine("🔄 Clearing any cached data and forcing fresh load...");
                
                // Clear the collection view filter cache
                FilteredPackages.Refresh();
                
                _allPackages = await _packageManagerService.GetAvailablePackagesAsync();
                
                System.Diagnostics.Debug.WriteLine($"📊 Loaded {_allPackages.Count} packages from database");
                
                await UpdateInstallationStatus();
                UpdateDisplayPackages();
                
                System.Diagnostics.Debug.WriteLine($"✅ Package loading completed. Displaying {_allPackages.Count} packages.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error loading packages: {ex.Message}");
                Services.NotificationService.Instance.ShowError($"Error loading packages: {ex.Message}", "Load Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task UpdateInstallationStatus()
        {
            try
            {
                // Get all installed services from database - this is our single source of truth
                var installedServices = await _serviceRepository.GetInstalledAsync();
                System.Diagnostics.Debug.WriteLine($"🗃️ PackageManager: Found {installedServices.Count()} installed services in database");
                
                // Log details for each installed service
                foreach (var service in installedServices)
                {
                    System.Diagnostics.Debug.WriteLine($"  📋 Service: {service.Name} (ID: {service.Id})");
                    System.Diagnostics.Debug.WriteLine($"      DisplayName: '{service.DisplayName}'");
                    System.Diagnostics.Debug.WriteLine($"      ServiceType: {service.ServiceType}");
                    System.Diagnostics.Debug.WriteLine($"      Version: '{service.Version}'");
                    System.Diagnostics.Debug.WriteLine($"      Path: '{service.BinaryPath}'");
                    System.Diagnostics.Debug.WriteLine($"      IsInstalled: {service.IsInstalled}");
                    System.Diagnostics.Debug.WriteLine($"      IsEnabled: {service.IsEnabled}");
                    System.Diagnostics.Debug.WriteLine($"      InstallDate: {service.InstallDate}");
                    System.Diagnostics.Debug.WriteLine($"      CreatedAt: {service.CreatedAt}");
                    System.Diagnostics.Debug.WriteLine($"      UpdatedAt: {service.UpdatedAt}");
                    System.Diagnostics.Debug.WriteLine($"      ---");
                }
                
                foreach (var package in _allPackages)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Checking package: Name='{package.Name}', DisplayName='{package.DisplayName}', Category='{package.Category}'");
                    
                    // Log all available package versions
                    if (package.AvailableVersions?.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  📋 Available package versions ({package.AvailableVersions.Count}):");
                        foreach (var version in package.AvailableVersions)
                        {
                            System.Diagnostics.Debug.WriteLine($"    - {version.Version} (DisplayName: '{version.DisplayName}', Stable: {version.IsStable}, Recommended: {version.IsRecommended}, LTS: {version.IsLTS})");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  📋 No available versions found for package: {package.Name}");
                    }
                    
                    // Check if this package has any installed services in the database
                    // Match by PackageVersions.ServiceName -> Services.Name OR by package name pattern
                    var packageServices = new List<ServiceDefinition>();
                    
                    if (package.AvailableVersions?.Count > 0)
                    {
                        // Get all ServiceNames from this package's versions
                        var packageServiceNames = package.AvailableVersions
                            .Where(v => !string.IsNullOrEmpty(v.ServiceName))
                            .Select(v => v.ServiceName)
                            .ToList();
                        
                        System.Diagnostics.Debug.WriteLine($"  🔍 Package {package.Name} has ServiceNames: [{string.Join(", ", packageServiceNames)}]");
                        
                        // Find installed services that match any of these ServiceNames
                        packageServices = installedServices.Where(s => 
                            packageServiceNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
                            .ToList();
                        
                        // If no exact matches found, also try pattern matching on service names
                        // This handles cases like "PHP 8.1.0", "PHP 8.3.0" etc matching package "php"
                        if (packageServices.Count == 0 && !string.IsNullOrEmpty(package.Name))
                        {
                            System.Diagnostics.Debug.WriteLine($"  🔍 No exact ServiceName match, trying pattern matching for package '{package.Name}'...");
                            packageServices = installedServices.Where(s => 
                                s.Name.Contains(package.Name, StringComparison.OrdinalIgnoreCase) ||
                                s.DisplayName.Contains(package.DisplayName, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            if (packageServices.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"  ✅ Found {packageServices.Count} services via pattern matching:");
                                foreach (var svc in packageServices)
                                {
                                    System.Diagnostics.Debug.WriteLine($"    - {svc.Name} ({svc.DisplayName})");
                                }
                            }
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"  Found {packageServices.Count} matching services:");
                    foreach (var service in packageServices)
                    {
                        System.Diagnostics.Debug.WriteLine($"    - {service.Name} (ID: {service.Id}) - Version: {service.Version}");
                        System.Diagnostics.Debug.WriteLine($"      ✅ Matched: PackageVersion.ServiceName='{service.Name}' == Services.Name='{service.Name}'");
                    }
                    
                    if (packageServices.Count > 0)
                    {
                        // Verify that installation files actually exist before marking as installed
                        var hasValidInstallation = await VerifyInstallationFilesExist(packageServices);
                        
                        if (hasValidInstallation)
                        {
                            package.IsInstalled = true;
                            // Collect all installed versions for this package
                            var installedVersions = packageServices.Select(s => s.Version).Where(v => !string.IsNullOrEmpty(v)).ToList();
                            package.InstalledVersion = installedVersions.FirstOrDefault() ?? "Unknown";
                            package.InstalledVersions = installedVersions;
                            System.Diagnostics.Debug.WriteLine($"✅ Package {package.Name} is installed (database + files verified): {string.Join(", ", installedVersions)}");
                        }
                        else
                        {
                            package.IsInstalled = false;
                            package.InstalledVersion = null;
                            package.InstalledVersions = [];
                            System.Diagnostics.Debug.WriteLine($"❌ Package {package.Name} marked as installed in database but files are missing - marking as not installed");
                        }
                    }
                    else
                    {
                        package.IsInstalled = false;
                        package.InstalledVersion = null;
                        package.InstalledVersions = [];
                        System.Diagnostics.Debug.WriteLine($"❌ Package {package.Name} is not installed (database)");
                    }
                    System.Diagnostics.Debug.WriteLine($"  Final status: IsInstalled={package.IsInstalled}, Version='{package.InstalledVersion}'");
                    System.Diagnostics.Debug.WriteLine($"  ========================================");
                }
                
                // Summary
                var installedCount = _allPackages.Count(p => p.IsInstalled);
                var notInstalledCount = _allPackages.Count(p => !p.IsInstalled);
                System.Diagnostics.Debug.WriteLine($"📊 SUMMARY: {installedCount} packages installed, {notInstalledCount} packages not installed out of {_allPackages.Count} total packages");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error updating installation status from database: {ex.Message}");
                // Fallback: mark all packages as not installed
                foreach (var package in _allPackages)
                {
                    package.IsInstalled = false;
                    package.InstalledVersion = null;
                    package.InstalledVersions = [];
                }
            }
        }

        private void UpdateDisplayPackages()
        {
            Packages.Clear();
            
            foreach (var package in _allPackages)
            {
                System.Diagnostics.Debug.WriteLine($"  📦 Processing package: {package.DisplayName}");
                
                var displayItem = new PackageDisplayItem
                {
                    Name = package.Name,
                    DisplayName = package.DisplayName,
                    Description = package.Description,
                    Category = package.Category,
                    Icon = package.Icon,
                    IsPopular = package.IsPopular,
                    IsInstalled = package.IsInstalled, // Use database status
                    InstalledVersion = package.InstalledVersion, // Use database version
                    InstalledVersions = package.InstalledVersions ?? [],
                    FormattedSize = _packageManagerService.FormatFileSize(package.DownloadSize),
                    Tags = package.Tags,
                    SourcePackage = package
                };
                
                Packages.Add(displayItem);
                
                System.Diagnostics.Debug.WriteLine($"    ✅ Added display item: {displayItem.DisplayName}, Category: {displayItem.Category}, Popular: {displayItem.IsPopular}, Installed: {displayItem.IsInstalled}");
            }
            
            System.Diagnostics.Debug.WriteLine($"📊 Total display packages: {Packages.Count}");
            
            // Update status properties
            UpdateStatusProperties();
        }

        private void UpdateStatusProperties()
        {
            TotalPackages = _allPackages.Count;
            InstalledPackages = _allPackages.Count(p => p.IsInstalled); // Use database status
            
            HasPackages = _allPackages.Count > 0;
            StatusMessage = _allPackages.Count > 0 ? "Ready" : "No packages available";
        }

        private async Task AddPackageAsync()
        {
            try
            {
                // Get MainWindow and navigate to CreatePackage view
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.CreatePackageButton_Click(this, new RoutedEventArgs());
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Services.NotificationService.Instance.ShowError($"Failed to navigate to create package: {ex.Message}", "Navigation Error");
            }
        }

        private async Task InstallPackageAsync(PackageDisplayItem? packageDisplayItem)
        {
            if (packageDisplayItem?.SourcePackage == null)
            {
                Services.NotificationService.Instance.ShowWarning("Package information not found.", "Package Error");
                return;
            }

            try
            {
                var package = packageDisplayItem.SourcePackage;

                // Refresh package data from database to get latest versions
                System.Diagnostics.Debug.WriteLine($"🔄 Refreshing package data for {package.Name} before opening install dialog");
                var refreshedPackage = await _packageManagerService.GetPackageByIdAsync(package.Id);
                
                if (refreshedPackage == null)
                {
                    Services.NotificationService.Instance.ShowError($"Could not load package data for {package.DisplayName}.", "Load Error");
                    return;
                }
                
                // Get fresh installation status from database using ServiceName matching
                var installedServices = await _serviceRepository.GetInstalledAsync();
                var packageServices = new List<ServiceDefinition>();
                
                if (refreshedPackage.AvailableVersions?.Count > 0)
                {
                    // Get all ServiceNames from this package's versions
                    var packageServiceNames = refreshedPackage.AvailableVersions
                        .Where(v => !string.IsNullOrEmpty(v.ServiceName))
                        .Select(v => v.ServiceName)
                        .ToList();
                    
                    // Find installed services that match any of these ServiceNames
                    packageServices = installedServices.Where(s => 
                        packageServiceNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                }
                
                var refreshedInstalledVersions = packageServices.Select(s => s.Version).ToList();
                
                // Apply file verification to installed versions
                var fileVerifiedInstalledVersions = new List<string>();
                foreach (var version in refreshedInstalledVersions)
                {
                    var versionServices = packageServices.Where(s => s.Version == version).ToList();
                    var hasValidInstallation = await VerifyInstallationFilesExist(versionServices);
                    
                    if (hasValidInstallation)
                    {
                        fileVerifiedInstalledVersions.Add(version);
                        System.Diagnostics.Debug.WriteLine($"✅ Version {version} verified with files - keeping as installed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Version {version} missing files - removing from installed list");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"📦 Refreshed package versions: [{string.Join(", ", refreshedPackage.AvailableVersions?.Select(v => v.Version) ?? new List<string>())}]");
                System.Diagnostics.Debug.WriteLine($"💿 Installed versions from database: [{string.Join(", ", refreshedInstalledVersions)}]");
                System.Diagnostics.Debug.WriteLine($"✅ File-verified installed versions: [{string.Join(", ", fileVerifiedInstalledVersions)}]");

                // Open installation configuration dialog with file-verified data
                var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                var configDialog = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<PackageInstallDialog>(
                    serviceProvider, refreshedPackage, fileVerifiedInstalledVersions);
                
                if (Helpers.DialogBlurHelper.ShowDialogWithBlur(configDialog) == true)
                {
                    // Installation will be handled by the dialog
                    await LoadPackagesAsync(); // Refresh after installation
                }
            }
            catch (Exception ex)
            {
                Services.NotificationService.Instance.ShowError($"Error installing package: {ex.Message}", "Installation Error");
            }
        }

        private async Task EditPackageAsync(PackageDisplayItem? packageDisplayItem)
        {
            if (packageDisplayItem?.SourcePackage == null)
            {
                Services.NotificationService.Instance.ShowWarning("Package information not found.", "Package Error");
                return;
            }

            try
            {
                var package = packageDisplayItem.SourcePackage;

                System.Diagnostics.Debug.WriteLine($"✏️ Editing package: {package.DisplayName}");

                // Navigate to edit package view
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.EditPackageButton_Click(package);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Services.NotificationService.Instance.ShowError($"Error navigating to edit package: {ex.Message}", "Navigation Error");
            }
        }

        private async Task DeletePackageAsync(PackageDisplayItem? packageDisplayItem)
        {
            if (packageDisplayItem?.SourcePackage == null)
            {
                Services.NotificationService.Instance.ShowWarning("Package information not found.", "Package Error");
                return;
            }

            try
            {
                var package = packageDisplayItem.SourcePackage;

                System.Diagnostics.Debug.WriteLine($"🗑️ Attempting to delete package: {package.DisplayName}");

                // Check if there are installed services for this package
                if (_packageUninstaller != null)
                {
                    var validation = await _packageUninstaller.ValidateUninstallAsync(package.Name);
                    
                    if (validation.AffectedServices.Any())
                    {
                        // Show service uninstall confirmation
                        var serviceResult = ModernDialogService.ShowServiceUninstallConfirmation(validation);
                        
                        if (serviceResult == MessageBoxResult.Yes)
                        {
                            // Show progress window for uninstallation
                            var progressWindow = new ServiceInstallProgressWindow($"Uninstalling {package.DisplayName}", isUninstall: true);
                            progressWindow.Show();

                            var progressReporter = new ProgressWindowReporter(progressWindow);
                            var uninstallSuccess = await _packageUninstaller.UninstallPackageAsync(
                                package.Name, null, progressReporter);

                            progressWindow.Close();

                            if (!uninstallSuccess)
                            {
                                Services.NotificationService.Instance.ShowError($"Failed to uninstall services for '{package.DisplayName}'.", "Uninstall Failed");
                                return;
                            }
                            
                            // Show success toast notification
                            Services.NotificationService.Instance.ShowSuccess(
                                $"Services for '{package.DisplayName}' have been uninstalled",
                                "Services Uninstalled");
                            
                            // Notify all views that services have been changed
                            MainWindow.NotifyServiceInstalled();
                        }
                        else
                        {
                            return; // User cancelled service uninstallation
                        }
                    }
                }

                // Show confirmation dialog for catalog deletion
                var result = ModernDialogService.ShowPackageDeletionConfirmation(
                    package.DisplayName, 
                    package.AvailableVersions?.Count ?? 0);

                if (result == MessageBoxResult.Yes)
                {
                    // Delete package from database
                    var success = await _packageManagerService.DeletePackageAsync(package.Id);
                    
                    if (success)
                    {
                        // Show success toast notification
                        Services.NotificationService.Instance.ShowSuccess(
                            $"Package '{package.DisplayName}' deleted successfully",
                            "Package Deleted");
                        
                        // Refresh the package list
                        await LoadPackagesAsync();
                    }
                    else
                    {
                        Services.NotificationService.Instance.ShowError($"Failed to delete package '{package.DisplayName}'. The package may be in use or there was a database error.", "Delete Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                Services.NotificationService.Instance.ShowError($"Error deleting package: {ex.Message}", "Delete Error");
            }
        }


        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Verifies that installation files actually exist for the given services
        /// </summary>
        private async Task<bool> VerifyInstallationFilesExist(IEnumerable<ServiceDefinition> services)
        {
            try
            {
                foreach (var service in services)
                {
                    var installationPath = service.BinaryPath;
                    
                    if (string.IsNullOrEmpty(installationPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Service {service.Name} has no installation path");
                        return false;
                    }
                    
                    // Check if the installation directory exists
                    if (!Directory.Exists(installationPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Service {service.Name} installation directory does not exist: {installationPath}");
                        return false;
                    }
                    
                    // Check for key executable files based on service type
                    var hasRequiredFiles = await CheckServiceSpecificFiles(service, installationPath);
                    if (!hasRequiredFiles)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Service {service.Name} missing required executable files in: {installationPath}");
                        return false;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✅ Service {service.Name} installation verified: {installationPath}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error verifying installation files: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks for service-specific executable files
        /// </summary>
        private async Task<bool> CheckServiceSpecificFiles(ServiceDefinition service, string installationPath)
        {
            try
            {
                return await Task.Run(() =>
                {
                    return service.ServiceType switch
                    {
                        ModStack.Common.ServiceType.Apache =>
                            File.Exists(Path.Combine(installationPath, "bin", "httpd.exe")) ||
                            File.Exists(Path.Combine(installationPath, "httpd.exe")),

                        ModStack.Common.ServiceType.Nginx =>
                            File.Exists(Path.Combine(installationPath, "nginx.exe")),

                        ModStack.Common.ServiceType.MySQL =>
                            File.Exists(Path.Combine(installationPath, "bin", "mysqld.exe")) ||
                            File.Exists(Path.Combine(installationPath, "mysqld.exe")),

                        ModStack.Common.ServiceType.PHP =>
                            File.Exists(Path.Combine(installationPath, "php.exe")) ||
                            File.Exists(Path.Combine(installationPath, "bin", "php.exe")),

                        ModStack.Common.ServiceType.MariaDB =>
                            File.Exists(Path.Combine(installationPath, "bin", "mysqld.exe")) ||
                            File.Exists(Path.Combine(installationPath, "mysqld.exe")),

                        _ =>
                            Directory.GetFiles(installationPath, "*", SearchOption.TopDirectoryOnly).Length > 0
                    };
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error checking service-specific files for {service.Name}: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

}
