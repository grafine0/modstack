using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ModStack.Common.Models;
using ModStack.Common.Enums;
using ModStack.Data.Interfaces;
using ModStack.Services;
using ModStack.GUI.Services;
using ModStack.GUI.Views.Controls;

namespace ModStack.GUI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for AddPackageDialog.xaml
    /// </summary>
    public partial class AddPackageDialog : Window, INotifyPropertyChanged
    {
        private readonly PackageManagerService _packageManagerService;
        private readonly AvailablePackage? _editingPackage;
        private readonly bool _isEditMode;
        private PackageVersionViewModel? _originalVersionBeingEdited;

        public event PropertyChangedEventHandler? PropertyChanged;

        #region Properties

        private string _packageName = string.Empty;
        public string PackageName
        {
            get => _packageName;
            set
            {
                if (_packageName != value)
                {
                    System.Diagnostics.Debug.WriteLine($"📝 PackageName property being set: '{value}' (was: '{_packageName}')");
                    _packageName = value;
                    OnPropertyChanged(nameof(PackageName));
                    
                    // Real-time validation
                    ValidatePackageNameRealTime(value);
                    UpdateCommandStatesForProperty(nameof(PackageName));
                }
            }
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
                    
                    // Real-time validation
                    ValidateDisplayNameRealTime(value);
                    UpdateCommandStatesForProperty(nameof(DisplayName));
                }
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        private string _category = "Other";
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
                    UpdateCommandStatesForProperty(nameof(Category));
                }
            }
        }

        private string _packageType = "Service";
        public string PackageType
        {
            get => _packageType;
            set
            {
                if (_packageType != value)
                {
                    _packageType = value;
                    OnPropertyChanged(nameof(PackageType));
                    UpdateCommandStatesForProperty(nameof(PackageType));
                }
            }
        }

        private string _packageIcon = "📦";
        public new string Icon
        {
            get => _packageIcon;
            set
            {
                _packageIcon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }

        private string _downloadSizeMB = "0";
        public string DownloadSizeMB
        {
            get => _downloadSizeMB;
            set
            {
                _downloadSizeMB = value;
                OnPropertyChanged(nameof(DownloadSizeMB));
            }
        }

        private string _homepage = string.Empty;
        public string Homepage
        {
            get => _homepage;
            set
            {
                _homepage = value;
                OnPropertyChanged(nameof(Homepage));
            }
        }

        private string _license = string.Empty;
        public string License
        {
            get => _license;
            set
            {
                _license = value;
                OnPropertyChanged(nameof(License));
            }
        }

        private string _tagsString = string.Empty;
        public string TagsString
        {
            get => _tagsString;
            set
            {
                _tagsString = value;
                OnPropertyChanged(nameof(TagsString));
            }
        }

        private string _version = string.Empty;
        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
                UpdateSaveButtonState();
            }
        }

        private string _versionDisplayName = string.Empty;
        public string VersionDisplayName
        {
            get => _versionDisplayName;
            set
            {
                _versionDisplayName = value;
                OnPropertyChanged(nameof(VersionDisplayName));
            }
        }

        private string _downloadUrl = string.Empty;
        public string DownloadUrl
        {
            get => _downloadUrl;
            set
            {
                _downloadUrl = value;
                OnPropertyChanged(nameof(DownloadUrl));
                UpdateSaveButtonState();
            }
        }

        private string _defaultPort = "0";
        public string DefaultPort
        {
            get => _defaultPort;
            set
            {
                _defaultPort = value;
                OnPropertyChanged(nameof(DefaultPort));
            }
        }

        private string _installPath = @"C:\ModStack\Services\";
        public string InstallPath
        {
            get => _installPath;
            set
            {
                _installPath = value;
                OnPropertyChanged(nameof(InstallPath));
            }
        }

        private bool _isPopular = false;
        public bool IsPopular
        {
            get => _isPopular;
            set
            {
                _isPopular = value;
                OnPropertyChanged(nameof(IsPopular));
            }
        }

        private bool _autoStart = false;
        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                OnPropertyChanged(nameof(AutoStart));
            }
        }

        private bool _registerAsService = false;
        public bool RegisterAsService
        {
            get => _registerAsService;
            set
            {
                _registerAsService = value;
                OnPropertyChanged(nameof(RegisterAsService));
            }
        }

        private ObservableCollection<PackageVersionViewModel> _packageVersions = [];
        public ObservableCollection<PackageVersionViewModel> PackageVersions
        {
            get => _packageVersions;
            set
            {
                if (_packageVersions != value)
            {
                _packageVersions = value;
                OnPropertyChanged(nameof(PackageVersions));
                    UpdateCommandStatesForProperty(nameof(PackageVersions));
                }
            }
        }

        #endregion

        #region Validation State Properties

        private bool _isPackageNameValid = false;
        public bool IsPackageNameValid
        {
            get => _isPackageNameValid;
            set
            {
                if (_isPackageNameValid != value)
                {
                    _isPackageNameValid = value;
                    OnPropertyChanged(nameof(IsPackageNameValid));
                    UpdateCommandStatesForProperty(nameof(PackageName));
                }
            }
        }

        private bool _isDisplayNameValid = false;
        public bool IsDisplayNameValid
        {
            get => _isDisplayNameValid;
            set
            {
                if (_isDisplayNameValid != value)
                {
                    _isDisplayNameValid = value;
                    OnPropertyChanged(nameof(IsDisplayNameValid));
                    UpdateCommandStatesForProperty(nameof(DisplayName));
                }
            }
        }

        private bool _isVersionValid = false;
        public bool IsVersionValid
        {
            get => _isVersionValid;
            set
            {
                if (_isVersionValid != value)
                {
                    _isVersionValid = value;
                    OnPropertyChanged(nameof(IsVersionValid));
                    UpdateVersionCommandStates();
                }
            }
        }

        private bool _isDownloadUrlValid = false;
        public bool IsDownloadUrlValid
        {
            get => _isDownloadUrlValid;
            set
            {
                if (_isDownloadUrlValid != value)
                {
                    _isDownloadUrlValid = value;
                    OnPropertyChanged(nameof(IsDownloadUrlValid));
                    UpdateVersionCommandStates();
                }
            }
        }

        private string _packageNameValidationMessage = string.Empty;
        public string PackageNameValidationMessage
        {
            get => _packageNameValidationMessage;
            set
            {
                if (_packageNameValidationMessage != value)
                {
                    _packageNameValidationMessage = value;
                    OnPropertyChanged(nameof(PackageNameValidationMessage));
                }
            }
        }

        private string _versionValidationMessage = string.Empty;
        public string VersionValidationMessage
        {
            get => _versionValidationMessage;
            set
            {
                if (_versionValidationMessage != value)
                {
                    _versionValidationMessage = value;
                    OnPropertyChanged(nameof(VersionValidationMessage));
                }
            }
        }

        private string _urlValidationMessage = string.Empty;
        public string UrlValidationMessage
        {
            get => _urlValidationMessage;
            set
            {
                if (_urlValidationMessage != value)
                {
                    _urlValidationMessage = value;
                    OnPropertyChanged(nameof(UrlValidationMessage));
                }
            }
        }

        #endregion

        #region Commands

        public ICommand AddVersionCommand { get; private set; } = null!;
        public ICommand EditVersionCommand { get; private set; } = null!;
        public ICommand RemoveVersionCommand { get; private set; } = null!;
        public ICommand CancelEditVersionCommand { get; private set; } = null!;
        public ICommand SavePackageCommand { get; private set; } = null!;
        public ICommand CancelCommand { get; private set; } = null!;

        #endregion

        #region Command State Management

        /// <summary>
        /// Updates specific commands that depend on the given property
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        private void UpdateCommandStatesForProperty(string propertyName)
        {
            switch (propertyName)
            {
                case nameof(PackageName):
                case nameof(DisplayName):
                case nameof(Category):
                    // These affect the SavePackageCommand
                    (SavePackageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    break;
                    
                case nameof(PackageVersions):
                    // Version changes affect both SavePackage and CancelEditVersion commands
                    (SavePackageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelEditVersionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    break;
                    
                default:
                    // For other properties, use general update
                    CommandManager.InvalidateRequerySuggested();
                    break;
            }
        }

        /// <summary>
        /// Updates command states for version-related operations
        /// </summary>
        private void UpdateVersionCommandStates()
        {
            (AddVersionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelEditVersionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        public AddPackageDialog() : this(null)
        {
        }

        public AddPackageDialog(AvailablePackage? packageToEdit = null)
        {
            InitializeComponent();
            DataContext = this;
            _packageManagerService = ServiceLocator.GetService<PackageManagerService>();
            _editingPackage = packageToEdit;
            _isEditMode = packageToEdit != null;
            
            // Initialize commands
            InitializeCommands();
            
            // Update dialog title and button text based on mode
            if (_isEditMode)
            {
                Title = "Edit Package";
                // Load package data asynchronously after the window is loaded
                Loaded += async (s, e) => await LoadPackageForEditingAsync();
            }
            else
            {
                Title = "Add Package";
            }
            
            // Set initial button text and wire up real-time validation after UI is loaded
            Loaded += (s, e) => 
            {
                if (SaveButtonText != null)
                {
                    SaveButtonText.Text = _isEditMode ? "Update Package" : "Add Package";
                }
                
                // Wire up real-time validation for version form fields
                WireUpRealTimeValidation();
            };
            
            UpdateSaveButtonState();
        }

        private async Task LoadPackageForEditingAsync()
        {
            if (_editingPackage == null) return;

            PackageName = _editingPackage.Name;
            
            // Disable package name editing for existing packages to maintain database integrity
            if (NameTextBox != null)
            {
                NameTextBox.IsEnabled = false;
                NameTextBox.ToolTip = "Package name cannot be changed for existing packages";
            }
            
            // Show warning message for edit mode and hide validation elements
            if (EditModeWarningTextBlock != null)
            {
                EditModeWarningTextBlock.Visibility = Visibility.Visible;
            }
            
            // Hide package name validation in edit mode since field is disabled
            if (PackageNameValidationTextBlock != null)
            {
                PackageNameValidationTextBlock.Visibility = Visibility.Collapsed;
            }
            DisplayName = _editingPackage.DisplayName;
            Description = _editingPackage.Description ?? string.Empty;
            Category = _editingPackage.Category;
            PackageType = _editingPackage.PackageType.ToString();
            Icon = _editingPackage.Icon ?? "📦";
            DownloadSizeMB = (_editingPackage.DownloadSize / 1024 / 1024).ToString();
            Homepage = _editingPackage.Homepage ?? string.Empty;
            License = _editingPackage.License ?? string.Empty;
            TagsString = _editingPackage.Tags != null ? string.Join(", ", _editingPackage.Tags) : string.Empty;
            IsPopular = _editingPackage.IsPopular;

            // Load all versions into the PackageVersions collection using optimized batch processing
            PackageVersions.Clear();
            if (_editingPackage.AvailableVersions != null && _editingPackage.AvailableVersions.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"📥 Loading {_editingPackage.AvailableVersions.Count} versions for editing...");
                
                // Convert to ViewModels efficiently
                var versionViewModels = _editingPackage.AvailableVersions.Select(version => new PackageVersionViewModel
                {
                    Version = version.Version,
                    DisplayName = version.DisplayName,
                    DownloadUrl = version.DownloadUrls?.FirstOrDefault() ?? string.Empty,
                    IsStable = version.IsStable,
                    IsRecommended = version.IsRecommended,
                    IsLTS = version.IsLTS,
                    ReleaseDate = version.ReleaseDate,
                    Size = version.Size
                }).ToList();

                // Check installation status for each version
                System.Diagnostics.Debug.WriteLine($"🔍 About to check installation status for {versionViewModels.Count} versions of package '{_editingPackage.Name}'");
                await UpdateInstallationStatusAsync(versionViewModels, _editingPackage.Name);
                
                // Debug: Log installation status results
                var installedCount = versionViewModels.Count(v => v.IsInstalled);
                System.Diagnostics.Debug.WriteLine($"📊 Installation status check complete: {installedCount}/{versionViewModels.Count} versions are installed");

                // Use bulk add for better performance
                AddVersionsBulk(versionViewModels);
                
                System.Diagnostics.Debug.WriteLine($"✅ Loaded and sorted {PackageVersions.Count} versions");
            }

            // Load configuration information
            if (_editingPackage.DefaultConfiguration != null)
            {
                DefaultPort = _editingPackage.DefaultConfiguration.Port.ToString();
                InstallPath = _editingPackage.DefaultConfiguration.InstallPath ?? @"C:\ModStack\Services\";
                AutoStart = _editingPackage.DefaultConfiguration.AutoStart;
                RegisterAsService = _editingPackage.DefaultConfiguration.RegisterAsService;
            }
        }

        private void InitializeCommands()
        {
            AddVersionCommand = new RelayCommand(ExecuteAddVersion, CanExecuteAddVersion);
            EditVersionCommand = new RelayCommand(ExecuteEditVersion);
            RemoveVersionCommand = new RelayCommand(ExecuteRemoveVersion);
            CancelEditVersionCommand = new RelayCommand(ExecuteCancelEditVersion, CanExecuteCancelEditVersion);
            SavePackageCommand = new RelayCommand(ExecuteSavePackage, CanExecuteSavePackage);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void UpdateSaveButtonState()
        {
            // Use precise command state updates for better performance
            (SavePackageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #region Command Implementations

        private bool CanExecuteAddVersion(object? parameter)
        {
            var version = NewVersionTextBox?.Text?.Trim() ?? string.Empty;
            var downloadUrl = NewVersionDownloadUrlTextBox?.Text?.Trim() ?? string.Empty;
            
            return !string.IsNullOrWhiteSpace(version) && 
                   !string.IsNullOrWhiteSpace(downloadUrl) &&
                   IsValidVersion(version) &&
                   IsValidUrl(downloadUrl);
        }

        private void ExecuteAddVersion(object? parameter)
        {
            try
            {
                var isEditMode = IsEditingVersion();
                var version = NewVersionTextBox.Text.Trim();
                var downloadUrl = NewVersionDownloadUrlTextBox.Text.Trim();

                System.Diagnostics.Debug.WriteLine($"🚀 ExecuteAddVersion called: Version={version}, URL={downloadUrl}, EditMode={isEditMode}");

                // Validate input
                if (!ValidateVersionInput(version, downloadUrl, isEditMode))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Validation failed for version {version}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Validation passed for version {version}");

                // Create version data from form
                var versionData = CreateVersionFromForm();
                System.Diagnostics.Debug.WriteLine($"📦 Created version data: {versionData.Version} - {versionData.DisplayName}");

                // Add or update the version
                ManageVersion(versionData, isEditMode ? "edit" : "add");

                // Clean up and exit edit mode
                ExitEditMode(restoreOriginal: false);
                
                // Refresh version-related command states
                UpdateVersionCommandStates();
                
                System.Diagnostics.Debug.WriteLine($"🎉 ExecuteAddVersion completed successfully");
            }
            catch (Exception ex)
            {
                var errorActionText = IsEditingVersion() ? "update" : "add";
                System.Diagnostics.Debug.WriteLine($"💥 ExecuteAddVersion error: {ex.Message}");
                ModernDialogService.ShowError($"Failed to {errorActionText} version: {ex.Message}", "Error");
            }
        }

        private void ExecuteEditVersion(object? parameter)
        {
            try
            {
                if (parameter is PackageVersionViewModel versionToEdit)
                {
                    EnterEditMode(versionToEdit);
                    UpdateVersionCommandStates();
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error editing version: {ex.Message}", "Error");
            }
        }

        private async void ExecuteRemoveVersion(object? parameter)
        {
            try
            {
                if (parameter is PackageVersionViewModel versionToRemove)
                {
                    await HandleVersionDeletionAsync(versionToRemove);
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Failed to remove version: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Handles comprehensive version deletion with service validation and user confirmation
        /// </summary>
        private async Task HandleVersionDeletionAsync(PackageVersionViewModel versionToRemove)
        {
            try
            {
                // Get package name from UI thread
                string packageName = NameTextBox.Text;
                System.Diagnostics.Debug.WriteLine($"🔍 Starting version deletion process for {packageName} v{versionToRemove.Version}");
                
                // Get package uninstaller service
                PackageUninstaller? packageUninstaller = null;
                try
                {
                    packageUninstaller = ServiceLocator.GetService<PackageUninstaller>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error getting PackageUninstaller service: {ex.Message}");
                    ModernDialogService.ShowError($"Service initialization error: {ex.Message}", "Service Error");
                    return;
                }
                
                if (packageUninstaller == null)
                {
                    ModernDialogService.ShowError("Package uninstaller service not available.", "Service Error");
                    return;
                }
                
                // Validate if this version has any installed services
                ServiceUninstallValidation validation;
                try
                {
                    validation = await packageUninstaller.ValidateUninstallAsync(packageName, versionToRemove.Version);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error during validation: {ex.Message}");
                    ModernDialogService.ShowError($"Validation error: {ex.Message}", "Validation Error");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"📋 Validation results: CanUninstall={validation.CanUninstall}, AffectedServices={validation.AffectedServices.Count}, RunningServices={validation.RunningServices.Count}");
                
                // Show comprehensive deletion confirmation dialog
                VersionDeletionResult result;
                try
                {
                    result = ModernDialogService.ShowVersionDeletionConfirmation(
                        packageName, 
                        versionToRemove.Version, 
                        validation);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error showing confirmation dialog: {ex.Message}");
                    ModernDialogService.ShowError($"Dialog error: {ex.Message}", "Dialog Error");
                    return;
                }
                
                if (!result.Confirmed)
                {
                    System.Diagnostics.Debug.WriteLine("❌ User cancelled version deletion");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"✅ User confirmed deletion. DeleteFiles={result.DeleteFiles}");
                
                // If there are services to uninstall, handle them first
                if (validation.AffectedServices.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"🛠️ Uninstalling {validation.AffectedServices.Count} affected services");
                    
                    // Show progress window for service uninstallation
                    var progressWindow = new ServiceInstallProgressWindow($"Uninstalling {packageName} v{versionToRemove.Version}", isUninstall: true)
                    {
                        Owner = this
                    };
                    progressWindow.Show();

                    var progressReporter = new ProgressWindowReporter(progressWindow);
                    var uninstallSuccess = await packageUninstaller.UninstallPackageAsync(
                        packageName, versionToRemove.Version, progressReporter);

                    progressWindow.Close();

                    if (!uninstallSuccess)
                    {
                        ModernDialogService.ShowError($"Failed to uninstall services for version {versionToRemove.Version}.", "Uninstall Failed");
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine("✅ Services uninstalled successfully");
                    
                    // Notify that services have been changed
                    MainWindow.NotifyServiceInstalled();
                }
                
                // Remove version from UI list
                ManageVersion(versionToRemove, "remove");
                UpdateVersionCommandStates();
                
                // Show success message
                var message = validation.AffectedServices.Count > 0 
                    ? $"Version {versionToRemove.Version} and its services have been removed successfully."
                    : $"Version {versionToRemove.Version} has been removed from the catalog.";
                    
                if (result.DeleteFiles && validation.FilesToDeleteCount > 0)
                {
                    message += $" {validation.FilesToDeleteCount:N0} files were deleted from disk.";
                }
                
                ModernDialogService.ShowInformation(message, "Version Deleted");
                
                System.Diagnostics.Debug.WriteLine($"🎉 Version deletion completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error during version deletion: {ex.Message}");
                ModernDialogService.ShowError($"Failed to delete version: {ex.Message}", "Deletion Error");
            }
        }

        private bool CanExecuteCancelEditVersion(object? parameter)
        {
            return IsEditingVersion();
        }

        private void ExecuteCancelEditVersion(object? parameter)
        {
            try
            {
                ExitEditMode(restoreOriginal: true);
                UpdateVersionCommandStates();
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error cancelling version edit: {ex.Message}", "Error");
            }
        }

        private bool CanExecuteSavePackage(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(PackageName) &&
                                 !string.IsNullOrWhiteSpace(DisplayName) &&
                                 !string.IsNullOrWhiteSpace(Category) &&
                                 PackageVersions.Count > 0;
        }

        #region Save Package Helper Methods

        /// <summary>
        /// Validates all package data before saving
        /// </summary>
        /// <returns>True if validation passes</returns>
        private bool ValidatePackageData()
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(PackageName) || 
                    string.IsNullOrWhiteSpace(DisplayName) ||
                    string.IsNullOrWhiteSpace(Category))
                {
                    ModernDialogService.ShowWarning("Please fill in all required fields (Name, Display Name, Category).", "Validation Error");
                return false;
                }

            // Check versions
                if (PackageVersions.Count == 0)
                {
                    ModernDialogService.ShowWarning("Please add at least one version for this package.", "Validation Error");
                return false;
            }

            // Validate download size
            if (!long.TryParse(DownloadSizeMB, out _))
            {
                ModernDialogService.ShowWarning("Please enter a valid download size in MB.", "Validation Error");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks for duplicate package names
        /// </summary>
        /// <returns>True if no duplicates found</returns>
        private async Task<bool> CheckForDuplicatesAsync()
        {
            if (!_isEditMode || (_isEditMode && PackageName != _editingPackage?.Name))
            {
                if (await _packageManagerService.PackageExistsAsync(PackageName))
                {
                    ModernDialogService.ShowWarning($"A package with the name '{PackageName}' already exists.", "Package Exists");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Builds the package object from form data
        /// </summary>
        /// <returns>Configured AvailablePackage object</returns>
        private AvailablePackage BuildPackageObject()
        {
            // Parse values
            _ = long.TryParse(DownloadSizeMB, out long sizeMB);
            _ = int.TryParse(DefaultPort, out int port);
            
                var tags = string.IsNullOrWhiteSpace(TagsString) 
                    ? [] 
                    : TagsString.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

                var package = _isEditMode ? _editingPackage! : new AvailablePackage();
                
                // Update package properties
            package.Name = PackageName.Trim();
                package.DisplayName = DisplayName.Trim();
                package.Description = Description.Trim();
                package.Category = Category;
                package.PackageType = Enum.TryParse<ModStack.Common.Enums.PackageType>(PackageType, out var packageType) ? packageType : ModStack.Common.Enums.PackageType.Service;
                package.Icon = string.IsNullOrWhiteSpace(Icon) ? "📦" : Icon.Trim();
                package.IsPopular = IsPopular;
            package.DownloadSize = sizeMB * 1024 * 1024;
                package.Tags = tags;
                package.Homepage = Homepage.Trim();
                package.License = License.Trim();
                package.LastUpdated = DateTime.Now;

            // Convert versions
                package.AvailableVersions = [.. PackageVersions.Select(v => new PackageVersion
                {
                    Version = v.Version,
                    DisplayName = v.DisplayName,
                    ServiceName = v.ServiceName,
                    ReleaseDate = v.ReleaseDate,
                    IsStable = v.IsStable,
                    IsRecommended = v.IsRecommended,
                    IsLTS = v.IsLTS,
                    Size = v.Size,
                    DownloadUrls = string.IsNullOrWhiteSpace(v.DownloadUrl) ? [] : [v.DownloadUrl]
                })];

            // Configure package settings
                package.DefaultConfiguration ??= new PackageConfiguration();

                package.DefaultConfiguration.Port = port;
                package.DefaultConfiguration.InstallPath = InstallPath.Trim();
                package.DefaultConfiguration.AutoStart = AutoStart;
                package.DefaultConfiguration.RegisterAsService = RegisterAsService;
                package.DefaultConfiguration.AvailableOptions ??= [];

            return package;
        }

        /// <summary>
        /// Saves the package to the database with comprehensive error handling and retry logic
        /// </summary>
        /// <param name="package">Package to save</param>
        private async Task SavePackageToDatabaseAsync(AvailablePackage package)
        {
            System.Diagnostics.Debug.WriteLine($"💾 About to save package to database:");
            System.Diagnostics.Debug.WriteLine($"  - Package Name Property: '{PackageName}'");
            System.Diagnostics.Debug.WriteLine($"  - Package.Name: '{package.Name}'");
            System.Diagnostics.Debug.WriteLine($"  - Package.DisplayName: '{package.DisplayName}'");
            System.Diagnostics.Debug.WriteLine($"  - Edit Mode: {_isEditMode}");
            System.Diagnostics.Debug.WriteLine($"  - Versions Count: {package.AvailableVersions?.Count ?? 0}");

            const int maxRetries = 3;
            const int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        UpdateProgress($"Retrying database operation (attempt {attempt}/{maxRetries})...");
                        await Task.Delay(retryDelayMs * attempt); // Exponential backoff
                    }

                if (_isEditMode)
                {
                        System.Diagnostics.Debug.WriteLine($"🔄 Calling UpdatePackageAsync for package: {package.Name} (attempt {attempt})");
                    await _packageManagerService.UpdatePackageAsync(package);
                        System.Diagnostics.Debug.WriteLine($"✅ UpdatePackageAsync completed for package: {package.Name}");
                }
                else
                {
                        System.Diagnostics.Debug.WriteLine($"➕ Calling AddPackageAsync for package: {package.Name} (attempt {attempt})");
                    await _packageManagerService.AddPackageAsync(package);
                        System.Diagnostics.Debug.WriteLine($"✅ AddPackageAsync completed for package: {package.Name}");
                    }

                    // Success - exit retry loop
                    return;
                }
                catch (Microsoft.Data.Sqlite.SqliteException sqliteEx)
                {
                    System.Diagnostics.Debug.WriteLine($"💥 SQLite Database error (attempt {attempt}): {sqliteEx.Message}");
                    
                    // Check for retryable SQLite errors by message content and error code
                    var errorCode = sqliteEx.SqliteErrorCode;
                    var errorMessage = sqliteEx.Message.ToLower();
                    
                    if (attempt < maxRetries && (
                        errorCode == 5 ||  // SQLITE_BUSY
                        errorCode == 6 ||  // SQLITE_LOCKED  
                        errorMessage.Contains("busy") ||
                        errorMessage.Contains("locked") ||
                        errorMessage.Contains("database is locked")))
                    {
                        // Database is busy or locked, retry with longer delay
                        System.Diagnostics.Debug.WriteLine($"🔄 Database busy/locked (code {errorCode}: {sqliteEx.Message}), retrying...");
                        continue;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Database operation failed: {sqliteEx.Message}. " +
                            "This might be due to database corruption or permission issues.", sqliteEx);
                    }
                }
                catch (System.IO.IOException ioEx)
                {
                    System.Diagnostics.Debug.WriteLine($"💥 File I/O error (attempt {attempt}): {ioEx.Message}");
                    
                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException($"File system error after {maxRetries} attempts. " +
                            $"Please check file permissions and disk space. Details: {ioEx.Message}", ioEx);
                    }
                }
                catch (UnauthorizedAccessException authEx)
                {
                    System.Diagnostics.Debug.WriteLine($"💥 Access denied error: {authEx.Message}");
                    throw new InvalidOperationException($"Access denied: {authEx.Message}. " +
                        "Please run the application as administrator or check file permissions.", authEx);
                }
                catch (TimeoutException timeoutEx)
                {
                    System.Diagnostics.Debug.WriteLine($"💥 Timeout error (attempt {attempt}): {timeoutEx.Message}");
                    
                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException($"Operation timed out after {maxRetries} attempts. " +
                            $"The database might be overloaded. Please try again later. Details: {timeoutEx.Message}", timeoutEx);
                    }
                }
                catch (Exception ex) when (IsTransientError(ex))
                {
                    System.Diagnostics.Debug.WriteLine($"💥 Transient error (attempt {attempt}): {ex.Message}");
                    
                    if (attempt == maxRetries)
                    {
                        throw new InvalidOperationException($"Operation failed after {maxRetries} attempts due to temporary issues. " +
                            $"Please try again later. Details: {ex.Message}", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Determines if an exception represents a transient error that might succeed on retry
        /// </summary>
        /// <param name="ex">Exception to check</param>
        /// <returns>True if the error might be transient</returns>
        private static bool IsTransientError(Exception ex)
        {
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("busy", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("temporary", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Updates progress with consistent formatting
        /// </summary>
        /// <param name="message">Progress message to display</param>
        private void UpdateProgress(string message)
        {
            UpdateProgressText(message);
        }

        #endregion

        private async void ExecuteSavePackage(object? parameter)
        {
            try
            {
                ToggleSaveButtonState(true, "Initializing...");

                // Handle unsaved version being edited
                if (IsEditingVersion() && _originalVersionBeingEdited != null)
                {
                    var shouldContinue = ModernDialogService.Show(
                        "You have an unsaved version being edited. Do you want to save the package without this version? " +
                        "Click 'Yes' to save without the edited version, 'No' to cancel and finish editing the version first.",
                        "Unsaved Version",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (shouldContinue == MessageBoxResult.No)
                    {
                        ToggleSaveButtonState(false);
                    return;
                }

                    // User chose to continue - restore the original version
                    System.Diagnostics.Debug.WriteLine($"⚠️ Restoring unsaved version {_originalVersionBeingEdited.Version} before package save");
                    ManageVersion(_originalVersionBeingEdited, "add");
                    ExitEditMode(restoreOriginal: false);
                }

                // Phase 1: Validation
                UpdateProgress("Validating input data...");
                await Task.Delay(100);

                if (!ValidatePackageData())
                {
                    return;
                }

                // Phase 2: Check for duplicates
                UpdateProgress("Checking for existing packages...");
                if (!await CheckForDuplicatesAsync())
                {
                    return;
                }

                // Phase 3: Build package object
                UpdateProgress("Building package structure...");
                await Task.Delay(150);

                var package = BuildPackageObject();

                // Debug: Log version information
                System.Diagnostics.Debug.WriteLine($"🔍 DEBUG: Processing {PackageVersions.Count} versions for save:");
                foreach (var v in PackageVersions)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Version: {v.Version}, DisplayName: {v.DisplayName}, URL: {v.DownloadUrl}");
                }

                System.Diagnostics.Debug.WriteLine($"🔍 DEBUG: Converted to {package.AvailableVersions.Count} AvailableVersions:");
                foreach (var v in package.AvailableVersions)
                {
                    System.Diagnostics.Debug.WriteLine($"  - Version: {v.Version}, DisplayName: {v.DisplayName}, URLs: {string.Join(", ", v.DownloadUrls)}");
                }

                // Phase 4: Save to database with success overlay
                var saveAction = _isEditMode ? "Updating" : "Saving";
                var successMessage = _isEditMode ? "Package Updated!" : "Package Added!";
                var subtitleMessage = _isEditMode ? "Your package has been updated successfully." : "Your new package has been added successfully.";
                
                await SuccessOverlay.ExecuteWithSuccessAsync(
                    async () =>
                    {
                        UpdateProgress($"{saveAction} package to database...");
                        await SavePackageToDatabaseAsync(package);
                    },
                    loadingMessage: $"{saveAction} package...",
                    successMessage: successMessage,
                    subtitleMessage: subtitleMessage,
                    showSuccessDuration: 2000
                );

                DialogResult = true;
                Close();
            }
            catch (InvalidOperationException opEx)
            {
                // Specific operational errors with detailed context
                var action = _isEditMode ? "update" : "add";
                UpdateProgress($"Error occurred during {action} operation");
                await Task.Delay(500);
                
                ModernDialogService.ShowError(opEx.Message, $"Operation Failed");
            }
            catch (ArgumentException argEx)
            {
                // Data validation or argument errors
                UpdateProgress("Data validation error occurred");
                await Task.Delay(500);
                
                ModernDialogService.ShowError($"Invalid data provided: {argEx.Message}\n\n" +
                    "Please check your input values and try again.", "Validation Error");
            }
            catch (UnauthorizedAccessException authEx)
            {
                // Permission-related errors
                UpdateProgress("Access permission error occurred");
                await Task.Delay(500);
                
                ModernDialogService.ShowError($"Access denied: {authEx.Message}\n\n" +
                    "Please run the application as administrator or check file permissions.", "Permission Error");
            }
            catch (System.IO.IOException ioEx)
            {
                // File system errors
                UpdateProgress("File system error occurred");
                await Task.Delay(500);
                
                ModernDialogService.ShowError($"File system error: {ioEx.Message}\n\n" +
                    "Please check available disk space and file permissions.", "File System Error");
            }
            catch (System.Net.NetworkInformation.NetworkInformationException netEx)
            {
                // Network-related errors
                UpdateProgress("Network error occurred");
                await Task.Delay(500);
                
                ModernDialogService.ShowError($"Network error: {netEx.Message}\n\n" +
                    "Please check your internet connection and try again.", "Network Error");
            }
            catch (TaskCanceledException)
            {
                // Operation was cancelled
                UpdateProgress("Operation was cancelled");
                await Task.Delay(300);
                
                ModernDialogService.ShowInformation("The operation was cancelled by the user.", "Operation Cancelled");
            }
            catch (Exception ex)
            {
                // Generic fallback for unexpected errors
                var action = _isEditMode ? "update" : "add";
                UpdateProgress($"Unexpected error during {action} operation");
                await Task.Delay(500);
                
                var errorDetails = $"An unexpected error occurred while trying to {action} the package.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"If this problem persists, please contact support with the following details:\n" +
                    $"- Package: {PackageName}\n" +
                    $"- Operation: {action}\n" +
                    $"- Error Type: {ex.GetType().Name}";
                
                ModernDialogService.ShowError(errorDetails, "Unexpected Error");
                
                // Log detailed error for debugging
                System.Diagnostics.Debug.WriteLine($"💥 Unexpected error in ExecuteSavePackage:");
                System.Diagnostics.Debug.WriteLine($"  - Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"  - Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  - Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                ToggleSaveButtonState(false);
            }
        }

        private void ExecuteCancel(object? parameter)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        /// <summary>
        /// Wires up real-time validation for form fields
        /// </summary>
        private void WireUpRealTimeValidation()
        {
            // Version form fields real-time validation
            if (NewVersionTextBox != null)
            {
                NewVersionTextBox.TextChanged += (s, e) =>
                {
                    ValidateVersionRealTime(NewVersionTextBox.Text);
                    UpdateServiceName();
                };
            }

            if (NewVersionDownloadUrlTextBox != null)
            {
                NewVersionDownloadUrlTextBox.TextChanged += (s, e) =>
                {
                    ValidateDownloadUrlRealTime(NewVersionDownloadUrlTextBox.Text);
                };
            }

            // Initial validation for existing values
            ValidatePackageNameRealTime(PackageName);
            ValidateDisplayNameRealTime(DisplayName);
            if (NewVersionTextBox != null) ValidateVersionRealTime(NewVersionTextBox.Text);
            if (NewVersionDownloadUrlTextBox != null) ValidateDownloadUrlRealTime(NewVersionDownloadUrlTextBox.Text);
            
            // Update service name when package name changes
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PackageName))
                {
                    UpdateServiceName();
                }
            };
            
            // Initial service name update
            this.Loaded += (s, e) => UpdateServiceName();
        }

        #region Real-Time Validation Methods

        /// <summary>
        /// Validates package name in real-time as user types
        /// </summary>
        /// <param name="value">Package name to validate</param>
        private void ValidatePackageNameRealTime(string value)
        {
            // Skip validation in edit mode since the field is disabled
            if (_isEditMode)
            {
                IsPackageNameValid = true; // Consider it valid since it's an existing package
                PackageNameValidationMessage = string.Empty; // No message needed
                return;
            }
            
            if (string.IsNullOrWhiteSpace(value))
            {
                IsPackageNameValid = false;
                PackageNameValidationMessage = "Package name is required";
            }
            else if (value.Length < 2)
            {
                IsPackageNameValid = false;
                PackageNameValidationMessage = "Package name must be at least 2 characters";
            }
            else if (!PackageNameRegex().IsMatch(value))
            {
                IsPackageNameValid = false;
                PackageNameValidationMessage = "Package name can only contain letters, numbers, hyphens, underscores, and dots";
            }
            else if (value.Length > 50)
            {
                IsPackageNameValid = false;
                PackageNameValidationMessage = "Package name must be 50 characters or less";
            }
            else
            {
                IsPackageNameValid = true;
                PackageNameValidationMessage = "✓ Valid package name";
            }
        }

        /// <summary>
        /// Validates display name in real-time as user types
        /// </summary>
        /// <param name="value">Display name to validate</param>
        private void ValidateDisplayNameRealTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsDisplayNameValid = false;
            }
            else if (value.Length < 2)
            {
                IsDisplayNameValid = false;
            }
            else if (value.Length > 100)
            {
                IsDisplayNameValid = false;
            }
            else
            {
                IsDisplayNameValid = true;
            }
        }

        /// <summary>
        /// Validates version format in real-time with enhanced conflict detection
        /// </summary>
        /// <param name="value">Version to validate</param>
        private void ValidateVersionRealTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsVersionValid = false;
                VersionValidationMessage = "Version is required";
                    return;
                }

            if (!IsValidVersion(value))
            {
                IsVersionValid = false;
                VersionValidationMessage = "Invalid format. Use: 1.0.0, 2.1.0-alpha, 3.0.0-beta.1";
                return;
            }

            // Enhanced conflict detection
            var (hasConflict, conflictMessage) = DetectVersionConflicts(value);
            if (hasConflict)
            {
                IsVersionValid = false;
                VersionValidationMessage = conflictMessage;
                return;
            }

            // Check for semantic version conflicts (similar versions)
            var similarVersions = FindSimilarVersions(value);
            if (similarVersions.Count > 0)
            {
                IsVersionValid = true; // Not blocking, just warning
                VersionValidationMessage = $"⚠️ Similar versions exist: {string.Join(", ", similarVersions.Take(3))}";
                return;
            }

            // All validations passed
            IsVersionValid = true;
            VersionValidationMessage = "✓ Valid version format";
        }

        /// <summary>
        /// Validates download URL in real-time
        /// </summary>
        /// <param name="value">URL to validate</param>
        private void ValidateDownloadUrlRealTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                IsDownloadUrlValid = false;
                UrlValidationMessage = "Download URL is required";
            }
            else if (!IsValidUrl(value))
            {
                IsDownloadUrlValid = false;
                UrlValidationMessage = "Invalid URL format. Must start with http:// or https://";
            }
            else
            {
                IsDownloadUrlValid = true;
                UrlValidationMessage = "✓ Valid URL format";
            }
        }

        /// <summary>
        /// Detects version conflicts with existing package versions
        /// </summary>
        /// <param name="version">Version to check for conflicts</param>
        /// <returns>Conflict detection result</returns>
        private (bool HasConflict, string ConflictMessage) DetectVersionConflicts(string version)
        {
            var isEditMode = IsEditingVersion();
            
            // Check for exact duplicate versions
            var exactMatch = PackageVersions.FirstOrDefault(v => 
                string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));
                
            if (exactMatch != null && !isEditMode)
            {
                return (true, $"❌ Version '{version}' already exists");
            }
            
            // If editing, allow the same version (user might be editing other properties)
            if (isEditMode && _originalVersionBeingEdited != null && 
                string.Equals(_originalVersionBeingEdited.Version, version, StringComparison.OrdinalIgnoreCase))
            {
                return (false, string.Empty);
            }
            
            // Check for semantic version conflicts (same major.minor.patch but different pre-release)
            var parsedVersion = ParseSemanticVersion(version);
            var conflictingVersions = PackageVersions
                .Where(v => !string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase))
                .Where(v =>
                {
                    var existingParsed = ParseSemanticVersion(v.Version);
                    return parsedVersion.Major == existingParsed.Major &&
                           parsedVersion.Minor == existingParsed.Minor &&
                           parsedVersion.Build == existingParsed.Build;
                })
                .ToList();
                
            if (conflictingVersions.Count > 0)
            {
                var conflictList = string.Join(", ", conflictingVersions.Select(v => v.Version).Take(2));
                return (true, $"❌ Conflicts with existing version(s): {conflictList}");
            }
            
            return (false, string.Empty);
        }

        /// <summary>
        /// Finds versions that are similar to the given version (for warnings)
        /// </summary>
        /// <param name="version">Version to find similar versions for</param>
        /// <returns>List of similar version strings</returns>
        private List<string> FindSimilarVersions(string version)
        {
            var parsedVersion = ParseSemanticVersion(version);
            var similarVersions = new List<string>();
            
            foreach (var existingVersion in PackageVersions)
            {
                if (string.Equals(existingVersion.Version, version, StringComparison.OrdinalIgnoreCase))
                    continue;
                    
                var existingParsed = ParseSemanticVersion(existingVersion.Version);
                
                // Consider versions similar if they share major.minor
                if (parsedVersion.Major == existingParsed.Major && 
                    parsedVersion.Minor == existingParsed.Minor)
                {
                    similarVersions.Add(existingVersion.Version);
                }
            }
            
            return [.. similarVersions.OrderByDescending(v => ParseSemanticVersion(v))];
        }

        /// <summary>
        /// Toggles the save button state and shows enhanced progress indication
        /// </summary>
        /// <param name="isSaving">Whether the save operation is in progress</param>
        /// <param name="progressText">Optional custom progress text</param>
        private void ToggleSaveButtonState(bool isSaving, string? progressText = null)
        {
            SaveButton.IsEnabled = !isSaving;
            CancelButton.IsEnabled = !isSaving;
            
            if (isSaving)
            {
                // Show progress indicators
                ShowProgressIndicators(true);
                
                // Update button content with spinner
                SaveButtonText.Text = _isEditMode ? "Updating..." : "Adding...";
                LoadingSpinner.Visibility = Visibility.Visible;
                
                // Update progress text
                var defaultProgressText = _isEditMode ? "Updating package..." : "Adding package...";
                ProgressTextBlock.Text = progressText ?? defaultProgressText;
                
                // Disable form interaction during save
                SetFormInteractionEnabled(false);
            }
            else
            {
                // Hide progress indicators
                ShowProgressIndicators(false);
                
                // Reset button content
                SaveButtonText.Text = _isEditMode ? "Update Package" : "Add Package";
                LoadingSpinner.Visibility = Visibility.Collapsed;
                
                // Re-enable form interaction
                SetFormInteractionEnabled(true);
            }
        }

        /// <summary>
        /// Shows or hides progress indicators
        /// </summary>
        /// <param name="show">Whether to show progress indicators</param>
        private void ShowProgressIndicators(bool show)
        {
            SaveProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ProgressTextBlock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }


        /// <summary>
        /// Updates the progress text during long operations
        /// </summary>
        /// <param name="text">Progress text to display</param>
        private void UpdateProgressText(string text)
        {
            ProgressTextBlock.Text = text;
        }

        /// <summary>
        /// Enables or disables form interaction during operations
        /// </summary>
        /// <param name="enabled">Whether form should be interactive</param>
        private void SetFormInteractionEnabled(bool enabled)
        {
            // Disable main input areas during save (but respect edit mode restrictions)
            if (!_isEditMode) // Only enable NameTextBox if not in edit mode
            {
                NameTextBox.IsEnabled = enabled;
            }
            DisplayNameTextBox.IsEnabled = enabled;
            DescriptionTextBox.IsEnabled = enabled;
            CategoryComboBox.IsEnabled = enabled;
            IconTextBox.IsEnabled = enabled;
            SizeTextBox.IsEnabled = enabled;
            HomepageTextBox.IsEnabled = enabled;
            LicenseTextBox.IsEnabled = enabled;
            TagsTextBox.IsEnabled = enabled;
            PortTextBox.IsEnabled = enabled;
            InstallPathTextBox.IsEnabled = enabled;
            
            // Disable version management during save
            AddVersionButton.IsEnabled = enabled;
            NewVersionTextBox.IsEnabled = enabled;
            NewVersionDisplayNameTextBox.IsEnabled = enabled;
            NewVersionDownloadUrlTextBox.IsEnabled = enabled;
            NewVersionIsStableCheckBox.IsEnabled = enabled;
            NewVersionIsRecommendedCheckBox.IsEnabled = enabled;
            NewVersionIsLTSCheckBox.IsEnabled = enabled;
            
            // Disable toggles
            IsPopularCheckBox.IsEnabled = enabled;
            AutoStartCheckBox.IsEnabled = enabled;
            RegisterAsServiceCheckBox.IsEnabled = enabled;
        }

        #endregion

        // Note: Event handlers replaced with Command pattern for better MVVM compliance

        #region Version Management Helper Methods

        /// <summary>
        /// Manages version operations (add, edit, remove) with optimized batch processing
        /// </summary>
        /// <param name="versionData">The version data to manage</param>
        /// <param name="action">The action to perform: "add", "edit", or "remove"</param>
        /// <param name="skipSort">Whether to skip automatic sorting (for batch operations)</param>
        /// <param name="skipValidation">Whether to skip validation checks (for internal operations)</param>
        private void ManageVersion(PackageVersionViewModel versionData, string action, bool skipSort = false, bool skipValidation = false)
        {
            System.Diagnostics.Debug.WriteLine($"🔄 ManageVersion called: Action={action}, Version={versionData.Version}, Current Count={PackageVersions.Count}, SkipSort={skipSort}");
            
            // Perform pre-operation validation if needed
            if (!skipValidation && !ValidateVersionOperation(versionData, action))
            {
                System.Diagnostics.Debug.WriteLine($"❌ Version operation validation failed for {action} on {versionData.Version}");
                return;
            }
            
            switch (action.ToLower())
            {
                case "add":
                    PackageVersions.Add(versionData);
                    System.Diagnostics.Debug.WriteLine($"✅ Added version {versionData.Version} to package versions list (New Count: {PackageVersions.Count})");
                    break;
                    
                case "edit":
                    var existingVersion = PackageVersions.FirstOrDefault(v => string.Equals(v.Version, versionData.Version, StringComparison.OrdinalIgnoreCase));
                    if (existingVersion != null)
                    {
                        var index = PackageVersions.IndexOf(existingVersion);
                        PackageVersions[index] = versionData;
                        System.Diagnostics.Debug.WriteLine($"✅ Updated version {versionData.Version} in package versions list (Count: {PackageVersions.Count})");
                    }
                    else
                    {
                        PackageVersions.Add(versionData);
                        System.Diagnostics.Debug.WriteLine($"✅ Added new version {versionData.Version} (edit mode fallback) (New Count: {PackageVersions.Count})");
                    }
                    break;
                    
                case "remove":
                    var versionToRemove = PackageVersions.FirstOrDefault(v => 
                        string.Equals(v.Version, versionData.Version, StringComparison.OrdinalIgnoreCase));
                if (versionToRemove != null)
                {
                    PackageVersions.Remove(versionToRemove);
                        System.Diagnostics.Debug.WriteLine($"🗑️ Removed version {versionData.Version} from package versions list (New Count: {PackageVersions.Count})");
                    }
                    break;
                    
                default:
                    throw new ArgumentException($"Unknown action type: {action}", nameof(action));
            }
            
            // Trigger real-time validation updates if version fields are affected
            if (!skipValidation)
            {
                TriggerVersionValidationUpdate();
            }
            
            // Sort versions after modification (unless skipped for batch operations)
            if (!skipSort)
            {
                SortVersionsOptimized();
            }
            
            // Debug: List current versions (only if not in batch mode to reduce noise)
            if (!skipSort)
            {
                System.Diagnostics.Debug.WriteLine($"📋 Current PackageVersions collection ({PackageVersions.Count} items):");
                foreach (var v in PackageVersions.Take(5)) // Limit debug output
                {
                    System.Diagnostics.Debug.WriteLine($"  - {v.Version} ({v.DisplayName})");
                }
                if (PackageVersions.Count > 5)
                {
                    System.Diagnostics.Debug.WriteLine($"  ... and {PackageVersions.Count - 5} more versions");
                }
            }
            
                    UpdateSaveButtonState();
        }

        /// <summary>
        /// Validates that a version operation can be performed
        /// </summary>
        /// <param name="versionData">Version data to validate</param>
        /// <param name="action">Action being performed</param>
        /// <returns>True if operation is valid</returns>
        private bool ValidateVersionOperation(PackageVersionViewModel versionData, string action)
        {
            if (versionData == null) return false;
            
            return action switch
            {
                var a when string.Equals(a, "add", StringComparison.OrdinalIgnoreCase) => !PackageVersions.Any(v => string.Equals(v.Version, versionData.Version, StringComparison.OrdinalIgnoreCase)),
                var a when string.Equals(a, "edit", StringComparison.OrdinalIgnoreCase) => true,
                var a when string.Equals(a, "remove", StringComparison.OrdinalIgnoreCase) => PackageVersions.Any(v => string.Equals(v.Version, versionData.Version, StringComparison.OrdinalIgnoreCase)),
                _ => false
            };
        }

        /// <summary>
        /// Triggers real-time validation update for version fields
        /// </summary>
        private void TriggerVersionValidationUpdate()
        {
            if (NewVersionTextBox != null && !string.IsNullOrWhiteSpace(NewVersionTextBox.Text))
            {
                ValidateVersionRealTime(NewVersionTextBox.Text);
            }
        }

        /// <summary>
        /// Optimized version sorting that only sorts when needed
        /// </summary>
        private void SortVersionsOptimized()
        {
            if (PackageVersions.Count <= 1) 
            {
                System.Diagnostics.Debug.WriteLine("⏭️ Skipping sort - collection has ≤1 items");
                return; // No need to sort single item or empty collection
            }
            
            // Check if collection is already sorted to avoid unnecessary work
            if (IsCollectionSorted())
            {
                System.Diagnostics.Debug.WriteLine("⏭️ Skipping sort - collection already sorted");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("🔄 Sorting versions collection...");
            SortVersions();
        }

        /// <summary>
        /// Checks if the PackageVersions collection is already sorted
        /// </summary>
        /// <returns>True if collection is sorted in descending order</returns>
        private bool IsCollectionSorted()
        {
            for (int i = 0; i < PackageVersions.Count - 1; i++)
            {
                var current = ParseSemanticVersion(PackageVersions[i].Version);
                var next = ParseSemanticVersion(PackageVersions[i + 1].Version);
                
                if (current.CompareTo(next) < 0) // Should be descending
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Manages multiple version operations efficiently with single sort at the end
        /// </summary>
        /// <param name="operations">List of version operations to perform</param>
        private void ManageVersionsBatch(List<(PackageVersionViewModel versionData, string action)> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ ManageVersionsBatch called with empty operations list");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"🔄 ManageVersionsBatch called with {operations.Count} operations");
            var startTime = DateTime.Now;
            
            // Group operations by type for potential optimizations
            var addOperations = operations.Where(op => string.Equals(op.action, "add", StringComparison.OrdinalIgnoreCase)).ToList();
            var editOperations = operations.Where(op => string.Equals(op.action, "edit", StringComparison.OrdinalIgnoreCase)).ToList();
            var removeOperations = operations.Where(op => string.Equals(op.action, "remove", StringComparison.OrdinalIgnoreCase)).ToList();
            
            System.Diagnostics.Debug.WriteLine($"📊 Operations breakdown: {addOperations.Count} adds, {editOperations.Count} edits, {removeOperations.Count} removes");
            
            // Perform all operations without sorting or validation (for performance)
            foreach (var (versionData, action) in operations)
            {
                ManageVersion(versionData, action, skipSort: true, skipValidation: true);
            }
            
            // Trigger validation update once for all changes
            TriggerVersionValidationUpdate();
            
            // Sort once at the end (only if needed)
            SortVersionsOptimized();
            
            var duration = DateTime.Now - startTime;
            System.Diagnostics.Debug.WriteLine($"✅ Batch operations completed in {duration.TotalMilliseconds:F1}ms, final collection size: {PackageVersions.Count}");
        }

        /// <summary>
        /// Efficiently adds multiple versions at once (optimized for bulk operations)
        /// </summary>
        /// <param name="versions">Versions to add</param>
        public void AddVersionsBulk(IEnumerable<PackageVersionViewModel> versions)
        {
            var versionList = versions?.ToList();
            if (versionList == null || versionList.Count == 0) return;
            
            System.Diagnostics.Debug.WriteLine($"📦 Adding {versionList.Count} versions in bulk...");
            
            var operations = versionList.Select(v => (v, "add")).ToList();
            ManageVersionsBatch(operations);
        }

        /// <summary>
        /// Validates if a URL is in correct format
        /// </summary>
        /// <param name="url">URL to validate</param>
        /// <returns>True if URL format is valid</returns>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;
                
            return UrlRegex().IsMatch(url);
        }

        [GeneratedRegex(@"^https?:\/\/[^\s$.?#].[^\s]*$", RegexOptions.IgnoreCase)]
        private static partial Regex UrlRegex();

        /// <summary>
        /// Validates if a version number is in correct format (e.g., 1.0.0, 2.1.0-alpha, 3.0.0-beta.1)
        /// </summary>
        /// <param name="version">Version string to validate</param>
        /// <returns>True if version format is valid</returns>
        private static bool IsValidVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;
                
            // Support semantic versioning: major.minor.patch with optional pre-release identifiers
            // Examples: 1.0.0, 2.1.0-alpha, 3.0.0-beta.1, 4.0.0-rc.2
            return VersionRegex().IsMatch(version);
        }

        [GeneratedRegex(@"^\d+\.\d+\.\d+(-[\w\.\-]+)?$", RegexOptions.IgnoreCase)]
        private static partial Regex VersionRegex();

        [GeneratedRegex(@"^[a-zA-Z0-9\-_\.]+$")]
        private static partial Regex PackageNameRegex();

        [GeneratedRegex(@"-+")]
        private static partial Regex ConsecutiveHyphensRegex();

        /// <summary>
        /// Parses a semantic version string into a comparable format
        /// </summary>
        /// <param name="version">Version string to parse</param>
        /// <returns>Version object for comparison</returns>
        private static Version ParseSemanticVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new Version(0, 0, 0);

            try
            {
                // Remove pre-release suffix for basic version comparison
                var versionPart = version.Split('-')[0];
                
                // Parse the major.minor.patch part
                var parts = versionPart.Split('.');
                if (parts.Length >= 3)
                {
                    if (int.TryParse(parts[0], out int major) &&
                        int.TryParse(parts[1], out int minor) &&
                        int.TryParse(parts[2], out int patch))
                    {
                        return new Version(major, minor, patch);
                    }
                }
                
                // Fallback for incomplete versions
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], out int major) &&
                        int.TryParse(parts[1], out int minor))
                    {
                        return new Version(major, minor, 0);
                    }
                }
                
                // Last resort fallback
                return new Version(0, 0, 0);
            }
            catch
            {
                return new Version(0, 0, 0);
            }
        }

        /// <summary>
        /// Sorts the PackageVersions collection by semantic version (newest first)
        /// Optimized to replace the entire collection at once
        /// </summary>
        private void SortVersions()
        {
            if (PackageVersions.Count <= 1) return; // No need to sort single item or empty collection
            
            var sortedVersions = PackageVersions
                .OrderByDescending(v => ParseSemanticVersion(v.Version))
                .ToList();

            // Replace the entire collection efficiently
            PackageVersions = new ObservableCollection<PackageVersionViewModel>(sortedVersions);
            OnPropertyChanged(nameof(PackageVersions));
        }

        /// <summary>
        /// Validates version input data with enhanced format checking
        /// </summary>
        /// <param name="version">Version string</param>
        /// <param name="downloadUrl">Download URL</param>
        /// <param name="isEditMode">Whether this is an edit operation</param>
        /// <returns>True if validation passes</returns>
        private bool ValidateVersionInput(string version, string downloadUrl, bool isEditMode = false)
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                var actionText = isEditMode ? "update" : "add";
                    ModernDialogService.ShowWarning($"Please fill in Version and Download URL to {actionText} a version.", "Validation Error");
                return false;
            }

            // Validate version format
            if (!IsValidVersion(version))
            {
                ModernDialogService.ShowWarning(
                    "Please enter a valid version number format (e.g., 1.0.0, 2.1.0-alpha, 3.0.0-beta.1).", 
                    "Invalid Version Format");
                return false;
            }

            // Validate download URL format
            if (!IsValidUrl(downloadUrl))
            {
                ModernDialogService.ShowWarning(
                    "Please enter a valid download URL starting with http:// or https://.", 
                    "Invalid URL Format");
                return false;
            }

            // Check for duplicate versions (only when adding new, not when editing)
            if (!isEditMode && PackageVersions.Any(v => v.Version.Equals(version, StringComparison.OrdinalIgnoreCase)))
            {
                ModernDialogService.ShowWarning($"Version '{version}' already exists for this package.", "Duplicate Version");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a PackageVersionViewModel from form input
        /// </summary>
        /// <returns>New PackageVersionViewModel with current form data</returns>
        private PackageVersionViewModel CreateVersionFromForm()
        {
                var version = NewVersionTextBox.Text.Trim();
                var displayName = string.IsNullOrWhiteSpace(NewVersionDisplayNameTextBox.Text) 
                    ? $"{version} (Custom)" 
                    : NewVersionDisplayNameTextBox.Text.Trim();
                var downloadUrl = NewVersionDownloadUrlTextBox.Text.Trim();
                var serviceName = GenerateServiceName(PackageName, version);

            return new PackageVersionViewModel
                {
                    Version = version,
                    DisplayName = displayName,
                    DownloadUrl = downloadUrl,
                    ServiceName = serviceName,
                    IsStable = NewVersionIsStableCheckBox.IsChecked ?? true,
                    IsRecommended = NewVersionIsRecommendedCheckBox.IsChecked ?? false,
                    IsLTS = NewVersionIsLTSCheckBox.IsChecked ?? false,
                    ReleaseDate = DateTime.Now,
                    Size = long.TryParse(DownloadSizeMB, out long sizeMB) ? sizeMB * 1024 * 1024 : 0
                };
        }

        /// <summary>
        /// Populates form fields with version data
        /// </summary>
        /// <param name="version">Version data to populate form with</param>
        private void PopulateFormWithVersion(PackageVersionViewModel version)
        {
            NewVersionTextBox.Text = version.Version;
            NewVersionDisplayNameTextBox.Text = version.DisplayName;
            NewVersionDownloadUrlTextBox.Text = version.DownloadUrl;
            NewVersionIsStableCheckBox.IsChecked = version.IsStable;
            NewVersionIsRecommendedCheckBox.IsChecked = version.IsRecommended;
            NewVersionIsLTSCheckBox.IsChecked = version.IsLTS;
            
            // Set the service name field (either from existing data or generate new)
            if (NewVersionServiceNameTextBox != null)
            {
                NewVersionServiceNameTextBox.Text = !string.IsNullOrEmpty(version.ServiceName) 
                    ? version.ServiceName 
                    : GenerateServiceName(PackageName, version.Version);
            }
        }

        /// <summary>
        /// Enters edit mode for a version
        /// </summary>
        /// <param name="versionToEdit">Version to edit</param>
        private void EnterEditMode(PackageVersionViewModel versionToEdit)
        {
            // Store original for potential restoration
            _originalVersionBeingEdited = new PackageVersionViewModel
            {
                Version = versionToEdit.Version,
                DisplayName = versionToEdit.DisplayName,
                DownloadUrl = versionToEdit.DownloadUrl,
                IsStable = versionToEdit.IsStable,
                IsRecommended = versionToEdit.IsRecommended,
                IsLTS = versionToEdit.IsLTS,
                ReleaseDate = versionToEdit.ReleaseDate,
                Size = versionToEdit.Size
            };
            
            // Populate form and remove from list
            PopulateFormWithVersion(versionToEdit);
            ManageVersion(versionToEdit, "remove");
            
            // Update UI for edit mode
            AddVersionButton.Content = "✏️ Update Version";
            AddVersionButton.Tag = "editing";
            CancelEditVersionButton.Visibility = Visibility.Visible;
            
            System.Diagnostics.Debug.WriteLine($"✏️ Entered edit mode for version {versionToEdit.Version}");
        }

        /// <summary>
        /// Exits edit mode and restores original version if needed
        /// </summary>
        /// <param name="restoreOriginal">Whether to restore the original version</param>
        private void ExitEditMode(bool restoreOriginal = false)
        {
            if (restoreOriginal && _originalVersionBeingEdited != null)
            {
                ManageVersion(_originalVersionBeingEdited, "add");
                System.Diagnostics.Debug.WriteLine($"🔄 Restored original version {_originalVersionBeingEdited.Version}");
            }
            
            ClearVersionForm();
            ResetAddVersionButton();
            _originalVersionBeingEdited = null;
            
            System.Diagnostics.Debug.WriteLine("❌ Exited edit mode");
        }

        #endregion

        private bool IsEditingVersion()
        {
            return AddVersionButton.Tag?.ToString() == "editing";
        }

        private void ClearVersionForm()
        {
            NewVersionTextBox.Clear();
            NewVersionDisplayNameTextBox.Clear();
            NewVersionDownloadUrlTextBox.Clear();
            NewVersionIsStableCheckBox.IsChecked = true;
            NewVersionIsRecommendedCheckBox.IsChecked = false;
            NewVersionIsLTSCheckBox.IsChecked = false;
            UpdateServiceName();
        }

        /// <summary>
        /// Updates the service name field based on package name and version
        /// </summary>
        private void UpdateServiceName()
        {
            if (NewVersionServiceNameTextBox != null)
            {
                var version = NewVersionTextBox?.Text?.Trim() ?? string.Empty;
                var serviceName = GenerateServiceName(PackageName, version);
                System.Diagnostics.Debug.WriteLine($"🔧 UpdateServiceName: PackageName='{PackageName}', Version='{version}', ServiceName='{serviceName}'");
                NewVersionServiceNameTextBox.Text = serviceName;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ UpdateServiceName: NewVersionServiceNameTextBox is null");
            }
        }

        /// <summary>
        /// Generates a service name based on package name and version
        /// </summary>
        /// <param name="packageName">The package name</param>
        /// <param name="version">The version number</param>
        /// <returns>Generated service name</returns>
        private static string GenerateServiceName(string packageName, string version)
        {
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
            {
                return string.Empty;
            }

            // Convert package name to lowercase and replace spaces/special chars with hyphens
            var cleanPackageName = packageName.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace(".", "-");

            // Remove any consecutive hyphens
            cleanPackageName = ConsecutiveHyphensRegex().Replace(cleanPackageName, "-");

            // Remove leading/trailing hyphens
            cleanPackageName = cleanPackageName.Trim('-');

            // Generate service name: package-name-version
            return $"{cleanPackageName}-{version}";
        }

        private void ResetAddVersionButton()
        {
            AddVersionButton.Content = "➕ Add Version";
            AddVersionButton.Tag = null; // Clear editing mode
            CancelEditVersionButton.Visibility = Visibility.Collapsed;
            _originalVersionBeingEdited = null; // Clear stored original version
        }

        /// <summary>
        /// Updates installation status for all version view models
        /// </summary>
        /// <param name="versionViewModels">List of version view models to update</param>
        /// <param name="packageName">Name of the package</param>
        private static async Task UpdateInstallationStatusAsync(List<PackageVersionViewModel> versionViewModels, string packageName)
        {
            try
            {
                // Get service repository to check for installed services
                var serviceRepository = ServiceLocator.GetService<IServiceRepository>();
                if (serviceRepository == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ Service repository not available for installation status check");
                    return;
                }

                // Get all services to check for installed versions
                var allServices = await serviceRepository.GetAllAsync();
                System.Diagnostics.Debug.WriteLine($"📊 Total services in database: {allServices.Count()}");
                System.Diagnostics.Debug.WriteLine($"🔍 Looking for services matching package: '{packageName}'");
                
                // Filter services that belong to this package
                var packageServices = allServices.Where(service => 
                    service.Name.StartsWith(packageName, StringComparison.OrdinalIgnoreCase) && service.IsInstalled).ToList();
                
                System.Diagnostics.Debug.WriteLine($"🔍 Found {packageServices.Count} installed services for package '{packageName}'");
                
                // Debug: Log all matching services
                foreach (var service in packageServices)
                {
                    System.Diagnostics.Debug.WriteLine($"  📋 Service: {service.Name}, Version: {service.Version}, IsInstalled: {service.IsInstalled}");
                }

                // Update installation status for each version
                foreach (var versionViewModel in versionViewModels)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Checking version {versionViewModel.Version}...");
                    
                    // Check if this specific version is installed
                    // Services are named either "packagename" or "packagename-version" for multi-version packages
                    var isInstalled = packageServices.Any(service => 
                        (service.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase) &&
                         service.Version?.Equals(versionViewModel.Version, StringComparison.OrdinalIgnoreCase) == true) ||
                        service.Name.Equals($"{packageName}-{versionViewModel.Version}", StringComparison.OrdinalIgnoreCase));

                    versionViewModel.IsInstalled = isInstalled;
                    
                    System.Diagnostics.Debug.WriteLine($"  📊 Version {versionViewModel.Version}: IsInstalled = {versionViewModel.IsInstalled}");
                    
                    if (versionViewModel.IsInstalled)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ Version {versionViewModel.Version} is marked as installed");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Version {versionViewModel.Version} is NOT installed");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error checking installation status: {ex.Message}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for package versions in the dialog
    /// </summary>
    public class PackageVersionViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _version = string.Empty;
        public string Version
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(Version));
            }
        }

        private string _displayName = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private string _downloadUrl = string.Empty;
        public string DownloadUrl
        {
            get => _downloadUrl;
            set
            {
                _downloadUrl = value;
                OnPropertyChanged(nameof(DownloadUrl));
            }
        }

        private string _serviceName = string.Empty;
        public string ServiceName
        {
            get => _serviceName;
            set
            {
                _serviceName = value;
                OnPropertyChanged(nameof(ServiceName));
            }
        }

        public bool IsStable { get; set; } = true;
        public bool IsRecommended { get; set; } = false;
        public bool IsLTS { get; set; } = false;
        public DateTime ReleaseDate { get; set; } = DateTime.Now;
        public long Size { get; set; } = 0;

        private bool _isInstalled = false;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged(nameof(IsInstalled));
                    OnPropertyChanged(nameof(InstallationStatusText));
                    OnPropertyChanged(nameof(InstallationStatusIcon));
                }
            }
        }

        public string InstallationStatusText => IsInstalled ? "Installed" : "Not Installed";
        public string InstallationStatusIcon => IsInstalled ? "✅" : "⭕";

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Enhanced implementation of ICommand for MVVM pattern with precise state management
    /// </summary>
    public class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
    {
        private readonly Action<object?> _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        private readonly Func<object?, bool>? _canExecute = canExecute;
        private readonly List<WeakReference<EventHandler>> _canExecuteChangedHandlers = [];

        public event EventHandler? CanExecuteChanged
        {
            add 
            { 
                if (value != null)
                {
                    _canExecuteChangedHandlers.Add(new WeakReference<EventHandler>(value));
                }
                CommandManager.RequerySuggested += value; 
            }
            remove 
            { 
                if (value != null)
                {
                    _canExecuteChangedHandlers.RemoveAll(wr => 
                        !wr.TryGetTarget(out var target) || target == value);
                }
                CommandManager.RequerySuggested -= value; 
            }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>
        /// Manually trigger CanExecuteChanged to refresh command states more efficiently
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // Clean up dead weak references
            _canExecuteChangedHandlers.RemoveAll(wr => !wr.TryGetTarget(out _));
            
            // Trigger specific handlers first (more responsive)
            foreach (var weakRef in _canExecuteChangedHandlers)
            {
                if (weakRef.TryGetTarget(out var handler))
                {
                    try
                    {
                        handler.Invoke(this, EventArgs.Empty);
                    }
                    catch
                    {
                        // Ignore handler errors to prevent cascade failures
                    }
                }
            }
            
            // Fallback to global invalidation
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
