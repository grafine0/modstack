using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ModStack.Common.Models;
using ModStack.GUI.Services;
using ModStack.Services;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Dialog for managing PHP extensions for a specific PHP service
/// </summary>
public partial class PHPExtensionManagerDialog : Window, INotifyPropertyChanged
{
    private readonly PHPExtensionManager _extensionManager;
    private readonly ServiceDefinition _phpService;
    private readonly string _phpVersion;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    public string DialogTitle => $"PHP Extensions - {_phpService.Name} v{_phpVersion}";
    public string DialogDescription => "Manage PHP extensions for this specific PHP installation";
    public string ServiceName => _phpService.DisplayName ?? _phpService.Name;
    public string PHPVersion => _phpVersion;

    private ObservableCollection<PHPExtensionViewModel> _availableExtensions = new();
    public ObservableCollection<PHPExtensionViewModel> AvailableExtensions
    {
        get => _availableExtensions;
        set
        {
            _availableExtensions = value;
            OnPropertyChanged(nameof(AvailableExtensions));
            UpdateFilteredExtensions();
        }
    }

    private ObservableCollection<InstalledExtensionViewModel> _installedExtensions = new();
    public ObservableCollection<InstalledExtensionViewModel> InstalledExtensions
    {
        get => _installedExtensions;
        set
        {
            _installedExtensions = value;
            OnPropertyChanged(nameof(InstalledExtensions));
        }
    }

    private ObservableCollection<PHPExtensionViewModel> _filteredAvailableExtensions = new();
    public ObservableCollection<PHPExtensionViewModel> FilteredAvailableExtensions
    {
        get => _filteredAvailableExtensions;
        set
        {
            _filteredAvailableExtensions = value;
            OnPropertyChanged(nameof(FilteredAvailableExtensions));
        }
    }

    private ObservableCollection<PHPExtensionViewModel> _filteredExtensions = new();
    public ObservableCollection<PHPExtensionViewModel> FilteredExtensions
    {
        get => _filteredExtensions;
        set
        {
            _filteredExtensions = value;
            OnPropertyChanged(nameof(FilteredExtensions));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            UpdateFilteredExtensions();
        }
    }

    private string _selectedCategory = "All Categories";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged(nameof(SelectedCategory));
            UpdateFilteredExtensions();
        }
    }

    private int _extensionFilterIndex = 0;
    public int ExtensionFilterIndex
    {
        get => _extensionFilterIndex;
        set
        {
            _extensionFilterIndex = value;
            OnPropertyChanged(nameof(ExtensionFilterIndex));
            UpdateFilteredExtensions();
        }
    }

    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(IsContentVisible));
        }
    }

    public bool IsContentVisible => !IsLoading;

    private string _loadingMessage = "Loading extensions...";
    public string LoadingMessage
    {
        get => _loadingMessage;
        set
        {
            _loadingMessage = value;
            OnPropertyChanged(nameof(LoadingMessage));
        }
    }

    // Filter logic based on combo box selection
    public bool ShowAvailableOnly => ExtensionFilterIndex == 1;
    public bool ShowInstalledOnly => ExtensionFilterIndex == 2;
    public bool ShowPopularOnly => ExtensionFilterIndex == 3;

    public ObservableCollection<string> Categories { get; set; } = new()
    {
        "All Categories",
        PHPExtensionCategories.Database,
        PHPExtensionCategories.WebServices,
        PHPExtensionCategories.ImageProcessing,
        PHPExtensionCategories.Performance,
        PHPExtensionCategories.Development,
        PHPExtensionCategories.Compression,
        PHPExtensionCategories.Encryption,
        PHPExtensionCategories.FileSystem,
        PHPExtensionCategories.Network,
        PHPExtensionCategories.Other
    };

    #endregion

    #region Commands

    public ICommand InstallExtensionCommand { get; }
    public ICommand UninstallExtensionCommand { get; }
    public ICommand ToggleExtensionCommand { get; }
    public ICommand RefreshCommand { get; }

    #endregion

    private readonly ModStack.GUI.Services.IViewFactoryService _viewFactoryService;

    public PHPExtensionManagerDialog(
        PHPExtensionManager extensionManager,
        ModStack.GUI.Services.IViewFactoryService viewFactoryService,
        ServiceDefinition phpService, 
        string phpVersion)
    {
        InitializeComponent();
        DataContext = this;

        _phpService = phpService;
        _phpVersion = phpVersion;
        _extensionManager = extensionManager;
        _viewFactoryService = viewFactoryService;

        // Initialize commands
        InstallExtensionCommand = new RelayCommand(ext => _ = InstallExtension(ext as PHPExtensionViewModel));
        UninstallExtensionCommand = new RelayCommand(ext => _ = UninstallExtension(ext as PHPExtensionViewModel));
        ToggleExtensionCommand = new RelayCommand(ext => _ = ToggleExtension(ext as PHPExtensionViewModel));
        RefreshCommand = new RelayCommand(_ => _ = LoadExtensions());

        Loaded += PHPExtensionManagerDialog_Loaded;
    }

    private async void PHPExtensionManagerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadExtensions();
    }

    private async Task LoadExtensions()
    {
        try
        {
            IsLoading = true;
            LoadingMessage = "Loading extensions...";
            
            // Load available extensions - normalize version (8.3.0 -> 8.3)
            var normalizedVersion = NormalizePHPVersion(_phpVersion);
            
            // First synchronize with filesystem to detect existing extensions
            LoadingMessage = "Synchronizing with filesystem...";
            var syncResult = await _extensionManager.SynchronizeExtensionsAsync(_phpService.Id, normalizedVersion);
            if (!syncResult.Success)
            {
                // Log warning but continue
            }
            
            LoadingMessage = "Loading available extensions...";
            var available = await _extensionManager.GetAvailableExtensionsAsync(normalizedVersion);
            
            LoadingMessage = "Loading installed extensions...";
            var installed = await _extensionManager.GetInstalledExtensionsAsync(_phpService.Id, normalizedVersion);
            
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"🔍 LoadExtensions Debug:");
            System.Diagnostics.Debug.WriteLine($"  - PHP Version: {_phpVersion}");
            System.Diagnostics.Debug.WriteLine($"  - Normalized Version: {normalizedVersion}");
            System.Diagnostics.Debug.WriteLine($"  - Available Extensions: {available.Count}");
            System.Diagnostics.Debug.WriteLine($"  - Installed Extensions: {installed.Count}");
            
            foreach (var inst in installed)
            {
                System.Diagnostics.Debug.WriteLine($"  - Installed: {inst.Extension?.Name} (ID: {inst.ExtensionId}, Enabled: {inst.IsEnabled})");
            }

            // Create view models for available extensions
            var availableVMs = available.Select(ext => 
            {
                var installedExt = installed.FirstOrDefault(i => i.ExtensionId == ext.Id);
                var isInstalled = installedExt != null;
                
                if (ext.Name == "gd")
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 GD Extension Debug:");
                    System.Diagnostics.Debug.WriteLine($"  - Extension ID: {ext.Id}");
                    System.Diagnostics.Debug.WriteLine($"  - Found in installed: {isInstalled}");
                    System.Diagnostics.Debug.WriteLine($"  - InstalledExt: {installedExt?.Extension?.Name}");
                    System.Diagnostics.Debug.WriteLine($"  - IsEnabled in DB: {installedExt?.IsEnabled}");
                }
                
                if (ext.Name == "mysqli")
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 MySQLi Extension Debug:");
                    System.Diagnostics.Debug.WriteLine($"  - Extension ID: {ext.Id}");
                    System.Diagnostics.Debug.WriteLine($"  - Found in installed: {isInstalled}");
                    System.Diagnostics.Debug.WriteLine($"  - InstalledExt: {installedExt?.Extension?.Name}");
                    System.Diagnostics.Debug.WriteLine($"  - IsEnabled in DB: {installedExt?.IsEnabled}");
                }
                
                return new PHPExtensionViewModel(ext)
                {
                    IsInstalled = isInstalled,
                    IsEnabled = installedExt?.IsEnabled ?? false // Use actual enabled state from database
                };
            }).ToList();

            AvailableExtensions.Clear();
            foreach (var vm in availableVMs)
            {
                AvailableExtensions.Add(vm);
            }

            // Create view models for installed extensions
            var installedVMs = installed.Select(inst => new InstalledExtensionViewModel(inst)).ToList();

            InstalledExtensions.Clear();
            foreach (var vm in installedVMs)
            {
                InstalledExtensions.Add(vm);
            }

            LoadingMessage = "Organizing extensions...";
            UpdateFilteredExtensions();
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Failed to load PHP extensions: {ex.Message}", "Load Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateFilteredExtensions()
    {
        var filtered = AvailableExtensions.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(ext =>
                ext.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                ext.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);
        }

        // Apply category filter
        if (SelectedCategory != "All Categories")
        {
            filtered = filtered.Where(ext => ext.Category == SelectedCategory);
        }

        // Apply extension type filter
        switch (ExtensionFilterIndex)
        {
            case 1: // Available Extensions
                filtered = filtered.Where(ext => !ext.IsInstalled);
                break;
            case 2: // Installed Extensions
                filtered = filtered.Where(ext => ext.IsInstalled);
                break;
            case 3: // Popular Extensions
                filtered = filtered.Where(ext => ext.IsPopular);
                break;
            default: // All Extensions (0)
                // No additional filtering
                break;
        }

        // Update both collections for backward compatibility
        FilteredAvailableExtensions.Clear();
        FilteredExtensions.Clear();
        
        foreach (var ext in filtered.OrderByDescending(e => e.IsInstalled).ThenByDescending(e => e.IsPopular).ThenBy(e => e.DisplayName))
        {
            FilteredAvailableExtensions.Add(ext);
            FilteredExtensions.Add(ext);
        }
    }

    private async Task InstallExtension(PHPExtensionViewModel? extensionVM)
    {
        if (extensionVM == null) return;

        try
        {
            // Get the full extension details
            var extension = await _extensionManager.GetExtensionByIdAsync(extensionVM.Id);
            if (extension == null)
            {
                ModernDialogService.ShowError($"Extension '{extensionVM.DisplayName}' not found.", "Installation Error");
                return;
            }

            // Show progress dialog
            var progressWindow = new Views.ExtensionInstallProgressWindow(_phpService, extension, _phpVersion)
            {
                Owner = this
            };

            var result = progressWindow.ShowDialog();
            
            if (result == true)
            {
                // Installation was successful, refresh the extensions list
                await LoadExtensions();
                ModernDialogService.ShowInformation($"Extension '{extensionVM.DisplayName}' installed successfully!", "Installation Complete");
            }
            else if (progressWindow.InstallationCompleted && !progressWindow.InstallationSuccessful)
            {
                // Installation completed but failed
                ModernDialogService.ShowError($"Failed to install extension '{extensionVM.DisplayName}'.", "Installation Failed");
            }
            // If cancelled, no message needed
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error installing extension: {ex.Message}", "Installation Error");
        }
    }

    private async Task UninstallExtension(PHPExtensionViewModel? extensionVM)
    {
        if (extensionVM == null || !extensionVM.IsInstalled) return;


        var result = ModernDialogService.Show(
            $"Are you sure you want to uninstall '{extensionVM.DisplayName}'?",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var uninstallResult = await _extensionManager.UninstallExtensionAsync(_phpService.Id, extensionVM.Id);
                
                if (uninstallResult.Success)
                {
                    ModernDialogService.ShowInformation(uninstallResult.Message, "Uninstall Complete");
                    await LoadExtensions(); // Refresh the lists
                }
                else
                {
                    ModernDialogService.ShowError($"Failed to uninstall extension '{extensionVM.DisplayName}': {uninstallResult.Message}", "Uninstall Failed");
                }
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error uninstalling extension: {ex.Message}", "Uninstall Error");
            }
        }
    }

    private async Task ToggleExtension(PHPExtensionViewModel? extensionVM)
    {
        if (extensionVM == null || !extensionVM.IsInstalled) return;

        try
        {
            var toggleResult = await _extensionManager.ToggleExtensionAsync(_phpService.Id, extensionVM.Id, !extensionVM.IsEnabled);
            
            // Check if user consent is required for service restart
            if (!toggleResult.Success && toggleResult.ErrorCode == "USER_CONSENT_REQUIRED")
            {
                // Show service restart consent dialog
                var consentDialog = _viewFactoryService.CreateDialog<ServiceRestartConsentDialog>(toggleResult.Message, new List<ServiceDefinition>());
                var dialogResult = consentDialog.ShowDialog();
                
                if (dialogResult == true && consentDialog.UserConsented)
                {
                    // User consented - for now, show a message that this feature will be implemented
                    ModernDialogService.ShowInformation(
                        "Service restart consent received. In a full implementation, the extension would be toggled with automatic service management.", 
                        "Feature Preview");
                }
                else
                {
                    // User cancelled
                    ModernDialogService.ShowInformation("Extension toggle cancelled by user.", "Cancelled");
                }
                return;
            }
            
            if (toggleResult.Success)
            {
                ModernDialogService.ShowInformation(toggleResult.Message, "Toggle Complete");
                await LoadExtensions(); // Refresh the lists
            }
            else
            {
                ModernDialogService.ShowError($"Failed to toggle extension '{extensionVM.DisplayName}': {toggleResult.Message}", "Toggle Failed");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error toggling extension: {ex.Message}", "Toggle Error");
        }
    }


    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Normalizes PHP version from "8.3.0" to "8.3" for extension compatibility
    /// </summary>
    private string NormalizePHPVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return "8.3";
        
        var parts = version.Split('.');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}.{parts[1]}";
        }
        
        return version;
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

/// <summary>
/// View model for available PHP extensions
/// </summary>
public class PHPExtensionViewModel : INotifyPropertyChanged
{
    private readonly PHPExtension _extension;
    private bool _isInstalled;
    private bool _isEnabled;

    public PHPExtensionViewModel(PHPExtension extension)
    {
        _extension = extension;
        _isEnabled = true; // Default to enabled
    }

    public long Id => _extension.Id;
    public string Name => _extension.Name;
    public string DisplayName => _extension.DisplayName;
    public string? Description => _extension.Description;
    public string Category => _extension.Category;
    public bool IsBuiltIn => _extension.IsBuiltIn;
    public bool IsZendExtension => _extension.IsZendExtension;
    public bool IsPopular => _extension.IsPopular;
    public string ExtensionType => _extension.IsZendExtension ? "Zend Extension" : "PHP Extension";

    public bool IsInstalled
    {
        get => _isInstalled;
        set
        {
            _isInstalled = value;
            OnPropertyChanged(nameof(IsInstalled));
            OnPropertyChanged(nameof(ToggleButtonText));
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(ToggleButtonText));
        }
    }

    public string ToggleButtonText => IsEnabled ? "Disable" : "Enable";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for installed PHP extensions
/// </summary>
public class InstalledExtensionViewModel : INotifyPropertyChanged
{
    private readonly InstalledPHPExtension _installedExtension;

    public InstalledExtensionViewModel(InstalledPHPExtension installedExtension)
    {
        _installedExtension = installedExtension;
    }

    public long Id => _installedExtension.Id;
    public long extensionId => _installedExtension.ExtensionId;
    public PHPExtension? Extension => _installedExtension.Extension;
    public string PHPVersion => _installedExtension.PHPVersion;
    public bool IsEnabled => _installedExtension.IsEnabled;
    public DateTime InstalledAt => _installedExtension.InstalledAt;

    public string StatusText => IsEnabled ? "ENABLED" : "DISABLED";
    public string StatusColor => IsEnabled ? "#4CAF50" : "#FF9800";
    public string ToggleButtonText => IsEnabled ? "Disable" : "Enable";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Converter that inverts a boolean value and converts it to Visibility
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}

public class ExtensionToggleBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnabled)
        {
            // When IsEnabled = true, button shows "Disable" (orange/red background)
            // When IsEnabled = false, button shows "Enable" (green background)
            return isEnabled ? "#FF6B47" : "#28A745"; // Orange for Disable, Green for Enable
        }
        return "#6C757D"; // Default gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

