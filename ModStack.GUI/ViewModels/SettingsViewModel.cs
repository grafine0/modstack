using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ModStack.Core.Services;

using ModStack.GUI.Models;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.ViewModels
{
    public sealed class SettingsViewModel : INotifyPropertyChanged, INotifyDataErrorInfo, IDisposable
    {
        private readonly ISettingsService _settingsService;
        private readonly IThemeService _themeService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly ILocalizationService? _localizationService;
        private string _statusMessage = "Ready";
        private string _lastSavedMessage = "Never saved";
        private bool _isLoading = false;
        
        // Validation error tracking
        private readonly Dictionary<string, List<string>> _validationErrors = new();
        private readonly CancellationTokenSource _masterCancellationTokenSource = new();
        private volatile bool _disposed = false;

        public SettingsViewModel(ISettingsService settingsService, IThemeService themeService, IDialogService dialogService, ILogger<SettingsViewModel> logger, ILocalizationService? localizationService = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Get localization service (optional - may not be available during initialization)
            _localizationService = localizationService;
            
            // Note: We don't subscribe to CultureChanged here to avoid duplicate UI refreshes
            // MainWindow will handle the UI refresh when culture changes
            
            // Initialize commands
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync, CanSaveSettings);
            ResetCommand = new AsyncRelayCommand(ResetSettingsAsync);
            BrowseRootDirectoryCommand = new RelayCommand(BrowseRootDirectory);
            ResetMinimizeChoiceCommand = new RelayCommand(ResetMinimizeChoice);
            SelectAccentColorCommand = new AsyncRelayCommand(SelectAccentColorAsync);
            
            // About section commands
            OpenWebsiteCommand = new AsyncRelayCommand(OpenWebsiteAsync);
            OpenDocsCommand = new AsyncRelayCommand(OpenDocsAsync);
            ReportIssueCommand = new AsyncRelayCommand(ReportIssueAsync);
            
            // Load current settings
            LoadSettingsFromService();
            
            // Apply notification settings to NotificationService
            ApplyNotificationSettings();
            
            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;
        }

        #region Properties

        // Application Settings
        public bool AutoStart
        {
            get => _settingsService.AutoStart;
            set
            {
                if (_settingsService.AutoStart != value)
                {
                    _settingsService.AutoStart = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MinimizeToTray
        {
            get => _settingsService.MinimizeToTray;
            set
            {
                if (_settingsService.MinimizeToTray != value)
                {
                    _settingsService.MinimizeToTray = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableLogging
        {
            get => _settingsService.EnableLogging;
            set
            {
                if (_settingsService.EnableLogging != value)
                {
                    _settingsService.EnableLogging = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsLogLevelEnabled));
                    
                    // Apply logging setting immediately
                    LogService.SetLoggingEnabled(value);
                    
                    // Update status
                    StatusMessage = value ? "Logging enabled - Log level can be configured" : "Logging disabled - Log level setting ignored";
                }
            }
        }

        public int RefreshInterval
        {
            get => _settingsService.RefreshInterval;
            set
            {
                if (_settingsService.RefreshInterval != value)
                {
                    _settingsService.RefreshInterval = value;
                    OnPropertyChanged();
                    ValidateProperty(value, nameof(RefreshInterval));
                    NotifyCommandsCanExecuteChanged();
                }
            }
        }

        public LogLevelOption LogLevel
        {
            get => Enum.TryParse<LogLevelOption>(_settingsService.LogLevel, out var level) ? level : LogLevelOption.Info;
            set
            {
                var stringValue = value.ToString();
                if (_settingsService.LogLevel != stringValue)
                {
                    _settingsService.LogLevel = stringValue;
                    OnPropertyChanged();
                }
            }
        }

        // Mail Catcher Settings
        public bool EnableMailCatcher
        {
            get => _settingsService.EnableMailCatcher;
            set
            {
                if (_settingsService.EnableMailCatcher != value)
                {
                    _settingsService.EnableMailCatcher = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MailCatcherPort
        {
            get => _settingsService.MailCatcherPort;
            set
            {
                if (_settingsService.MailCatcherPort != value)
                {
                    _settingsService.MailCatcherPort = value;
                    OnPropertyChanged();
                    ValidateProperty(value, nameof(MailCatcherPort));
                    NotifyCommandsCanExecuteChanged();
                }
            }
        }

        public bool IsLogLevelEnabled => EnableLogging;

        public IEnumerable<LogLevelOption> LogLevels { get; } = Enum.GetValues<LogLevelOption>();

        // Service Settings
        public bool AutoAssignPorts
        {
            get => _settingsService.AutoAssignPorts;
            set
            {
                if (_settingsService.AutoAssignPorts != value)
                {
                    _settingsService.AutoAssignPorts = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PortRangeStart
        {
            get => _settingsService.PortRangeStart;
            set
            {
                if (_settingsService.PortRangeStart != value)
                {
                    _settingsService.PortRangeStart = value;
                    OnPropertyChanged();
                    ValidateProperty(value, nameof(PortRangeStart));
                    NotifyCommandsCanExecuteChanged();
                }
            }
        }

        public int PortRangeEnd
        {
            get => _settingsService.PortRangeEnd;
            set
            {
                if (_settingsService.PortRangeEnd != value)
                {
                    _settingsService.PortRangeEnd = value;
                    OnPropertyChanged();
                    ValidateProperty(value, nameof(PortRangeEnd));
                    NotifyCommandsCanExecuteChanged();
                }
            }
        }

        public string RootDirectory
        {
            get => _settingsService.RootDirectory;
            set
            {
                if (_settingsService.RootDirectory != value)
                {
                    _settingsService.RootDirectory = value;
                    OnPropertyChanged();
                    ValidateProperty(value, nameof(RootDirectory));
                    NotifyCommandsCanExecuteChanged();
                }
            }
        }

        // Appearance Settings
        public ThemeOption Theme
        {
            get => Enum.TryParse<ThemeOption>(_settingsService.Theme, out var theme) ? theme : ThemeOption.Light;
            set
            {
                var stringValue = value.ToString();
                if (_settingsService.Theme != stringValue)
                {
                    _settingsService.Theme = stringValue;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<ThemeOption> Themes { get; } = Enum.GetValues<ThemeOption>();

        public string Language
        {
            get => _settingsService.Language;
            set
            {
                if (_settingsService.Language != value)
                {
                    _settingsService.Language = value;
                    OnPropertyChanged();
                    
                    // Apply language change immediately
                    ApplyLanguageSettings();
                }
            }
        }

        public IEnumerable<string> AvailableLanguages { get; } = new[]
        {
            "en-US", // English
            "fr-FR", // French
            "es-ES", // Spanish
            "ar-SA"  // Arabic
        };

        public string GetLanguageDisplayName(string cultureCode)
        {
            return cultureCode switch
            {
                "en-US" => "English",
                "fr-FR" => "Français",
                "es-ES" => "Español",
                "ar-SA" => "العربية",
                _ => cultureCode
            };
        }

        public bool AlwaysOnTop
        {
            get => _settingsService.AlwaysOnTop;
            set
            {
                if (_settingsService.AlwaysOnTop != value)
                {
                    _settingsService.AlwaysOnTop = value;
                    OnPropertyChanged();
                }
            }
        }

        // Notification Settings
        public bool ShowSuccessNotifications
        {
            get => _settingsService.ShowSuccessNotifications;
            set
            {
                if (_settingsService.ShowSuccessNotifications != value)
                {
                    _settingsService.ShowSuccessNotifications = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowErrorNotifications
        {
            get => _settingsService.ShowErrorNotifications;
            set
            {
                if (_settingsService.ShowErrorNotifications != value)
                {
                    _settingsService.ShowErrorNotifications = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoDismissNotifications
        {
            get => _settingsService.AutoDismissNotifications;
            set
            {
                if (_settingsService.AutoDismissNotifications != value)
                {
                    _settingsService.AutoDismissNotifications = value;
                    OnPropertyChanged();
                }
            }
        }

        public int DismissTimeout
        {
            get => _settingsService.DismissTimeout;
            set
            {
                if (_settingsService.DismissTimeout != value)
                {
                    _settingsService.DismissTimeout = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NotificationPosition
        {
            get => _settingsService.NotificationPosition;
            set
            {
                if (_settingsService.NotificationPosition != value)
                {
                    _settingsService.NotificationPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public IEnumerable<string> NotificationPositions { get; } = new[]
        {
            "TopLeft",
            "TopCenter",
            "TopRight",
            "BottomLeft",
            "BottomCenter",
            "BottomRight"
        };

        // Status Properties
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

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string LastSavedMessage
        {
            get => _lastSavedMessage;
            set
            {
                if (_lastSavedMessage != value)
                {
                    _lastSavedMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AccentColor
        {
            get => _settingsService.AccentColor;
            set
            {
                if (_settingsService.AccentColor != value)
                {
                    _settingsService.AccentColor = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccentColorBrush));
                }
            }
        }

        // Computed property for XAML binding
        public SolidColorBrush AccentColorBrush
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(AccentColor) && AccentColor.StartsWith("#"))
                    {
                        var color = (Color)ColorConverter.ConvertFromString(AccentColor);
                        return new SolidColorBrush(color);
                    }
                }
                catch
                {
                    // If parsing fails, return default color
                }
                return new SolidColorBrush(Colors.Blue); // Default color
            }
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BrowseRootDirectoryCommand { get; }
        public ICommand ResetMinimizeChoiceCommand { get; }
        public ICommand SelectAccentColorCommand { get; }
        
        // About section commands
        public ICommand OpenWebsiteCommand { get; }
        public ICommand OpenDocsCommand { get; }
        public ICommand ReportIssueCommand { get; }

        #endregion

        #region Command Methods

        private async Task SaveSettingsAsync()
        {
            // Set loading state
            IsLoading = true;
            
            // Give UI time to render animation before starting async operation
            await Task.Delay(50);
            
            // Create a linked token that can be cancelled either by this operation or by disposal
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(_masterCancellationTokenSource.Token);
            
            try
            {
                DispatchToUI(() => StatusMessage = "Saving and applying settings...");
                
                if (!ValidateAllSettings())
                {
                    DispatchToUI(() => StatusMessage = "Settings validation failed");
                    return;
                }
                
                // Apply settings that don't need UI thread
                await Task.Run(() =>
                {
                    // Ensure root directory exists before saving/applying
                    EnsureRootDirectoryExists();
                    _settingsService.ApplySettings();
                    _settingsService.SaveSettings();
                    // Apply settings to NotificationService
                    ApplyNotificationSettings();
                }, operationCts.Token);
                
                // Apply UI-specific settings - must be on UI thread
                DispatchToUI(() =>
                {
                    LogService.SetLoggingEnabled(EnableLogging);
                    ApplyThemeSettings();
                    ApplyWindowSettings();
                    
                    LastSavedMessage = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    StatusMessage = "Settings saved and applied successfully";
                });
                
                // Show success toast notification
                Services.NotificationService.Instance.ShowSuccess(
                    "Settings have been saved and applied",
                    "Settings Saved");
                
                LogService.Info("Settings saved and applied successfully");
            }
            catch (OperationCanceledException)
            {
                DispatchToUI(() => StatusMessage = "Save operation was cancelled");
                LogService.Info("Settings save operation was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
                DispatchToUI(() => StatusMessage = "Error saving settings");
                throw; // Re-throw for UI error handling
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetSettingsAsync()
        {
            try
            {
                // Ask user for confirmation using the dialog service
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "Are you sure you want to reset all settings to their default values?", 
                    "Reset Settings");
                
                if (confirmed)
                {
                    // Reset to defaults by loading fresh settings
                    _settingsService.LoadSettings();
                    LoadSettingsFromService();
                    DispatchToUI(() => StatusMessage = "Settings reset to defaults");
                    
                    LogService.Info("Settings reset to defaults");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                DispatchToUI(() => StatusMessage = "Error resetting settings");
                await _dialogService.ShowErrorAsync($"Error resetting settings: {ex.Message}", "Reset Error");
            }
        }

        private void BrowseRootDirectory()
        {
            try
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select root directory where ModStack creates all directories",
                    SelectedPath = RootDirectory
                };
                
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    RootDirectory = folderDialog.SelectedPath;
                    DispatchToUI(() => StatusMessage = "Root directory updated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing root directory");
                DispatchToUI(() => StatusMessage = "Error selecting directory");
            }
        }

        private void ResetMinimizeChoice()
        {
            try
            {
                _settingsService.RememberMinimizeChoice = false;
                _settingsService.SaveSettings();
                
                DispatchToUI(() => StatusMessage = "Minimize choice preference reset. You will be asked again on next close.");
                LogService.Info("Minimize choice preference reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting minimize choice preference");
                DispatchToUI(() => StatusMessage = "Error resetting minimize choice preference");
            }
        }

        private async Task SelectAccentColorAsync()
        {
            try
            {
                // Convert string color to Color object
                Color currentColor;
                try
                {
                    if (!string.IsNullOrEmpty(AccentColor) && AccentColor.StartsWith("#"))
                    {
                        currentColor = (Color)ColorConverter.ConvertFromString(AccentColor);
                    }
                    else
                    {
                        currentColor = Colors.Blue; // Default color
                    }
                }
                catch
                {
                    currentColor = Colors.Blue; // Default color if parsing fails
                }
                
                var selectedColor = await _dialogService.ShowColorPickerAsync(currentColor);
                
                if (selectedColor.HasValue)
                {
                    // Convert Color to hex string for storage
                    var color = selectedColor.Value;
                    AccentColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                    
                    // Log the user's explicit color choice
                    LogService.Info($"User selected accent color: RGB({selectedColor.Value.R}, {selectedColor.Value.G}, {selectedColor.Value.B})");
                    DispatchToUI(() => StatusMessage = "Accent color updated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting accent color");
                DispatchToUI(() => StatusMessage = "Error selecting color");
            }
        }

        private async Task OpenWebsiteAsync()
        {
            try
            {
                // Replace with actual website URL
                var websiteUrl = "https://github.com/ModStack/ModStack"; // Example URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = websiteUrl,
                    UseShellExecute = true
                });
                DispatchToUI(() => StatusMessage = "Website opened in browser");
                LogService.Info("Website opened via OpenWebsiteCommand");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening website");
                DispatchToUI(() => StatusMessage = "Error opening website");
                
                // Show user-friendly dialog
                await _dialogService.ShowErrorAsync(
                    "Unable to open the website in your default browser. Please ensure you have a web browser installed and try again.",
                    "Cannot Open Website");
            }
        }

        private async Task OpenDocsAsync()
        {
            try
            {
                // Replace with actual documentation URL
                var docsUrl = "https://github.com/ModStack/ModStack/wiki"; // Example URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = docsUrl,
                    UseShellExecute = true
                });
                DispatchToUI(() => StatusMessage = "Documentation opened in browser");
                LogService.Info("Documentation opened via OpenDocsCommand");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening documentation");
                DispatchToUI(() => StatusMessage = "Error opening documentation");
                
                // Show user-friendly dialog
                await _dialogService.ShowErrorAsync(
                    "Unable to open the documentation in your default browser. Please ensure you have a web browser installed and try again.",
                    "Cannot Open Documentation");
            }
        }

        private async Task ReportIssueAsync()
        {
            try
            {
                // Replace with actual issue reporting URL
                var issueUrl = "https://github.com/ModStack/ModStack/issues/new"; // Example URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = issueUrl,
                    UseShellExecute = true
                });
                DispatchToUI(() => StatusMessage = "Issue reporting page opened in browser");
                LogService.Info("Issue reporting page opened via ReportIssueCommand");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening issue reporting page");
                DispatchToUI(() => StatusMessage = "Error opening issue reporting page");
                
                // Show user-friendly dialog
                await _dialogService.ShowErrorAsync(
                    "Unable to open the issue reporting page in your default browser. Please ensure you have a web browser installed and try again.",
                    "Cannot Open Issue Reporting");
            }
        }

        private bool CanSaveSettings() => !HasErrors;
        

        #endregion

        #region Validation

        /// <summary>
        /// Validates all settings properties. Only call this when you need to ensure 
        /// all validation is up-to-date before performing an operation.
        /// For command CanExecute, rely on HasErrors which is kept current by property setters.
        /// </summary>
        private bool ValidateAllSettings()
        {
            try
            {
                // Trigger validation for all properties to ensure errors are current
                ValidateProperty(RefreshInterval, nameof(RefreshInterval));
                ValidateProperty(PortRangeStart, nameof(PortRangeStart));
                ValidateProperty(PortRangeEnd, nameof(PortRangeEnd));
                ValidateProperty(RootDirectory, nameof(RootDirectory));
                
                // Use INotifyDataErrorInfo validation results
                return !HasErrors;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during settings validation");
                DispatchToUI(() => StatusMessage = $"Error validating settings: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Safely dispatches an action to the UI thread
        /// </summary>
        private void DispatchToUI(Action action)
        {
            // Safety check: don't attempt UI updates after disposal
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("Skipped UI update attempt after ViewModel disposal");
                return;
            }

            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    // Already on UI thread - check disposal again before execution
                    if (!_disposed)
                    {
                        action();
                    }
                }
                else
                {
                    // Dispatch to UI thread asynchronously with disposal check
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!_disposed)
                        {
                            action();
                        }
                    });
                }
            }
            else
            {
                // Fallback - execute directly (e.g., in unit tests) with disposal check
                if (!_disposed)
                {
                    action();
                }
            }
        }

        /// <summary>
        /// Ensures the root directory exists, creating it if necessary.
        /// This method contains the side effects that were removed from validation.
        /// </summary>
        private void EnsureRootDirectoryExists()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(RootDirectory) && !Directory.Exists(RootDirectory))
                {
                    Directory.CreateDirectory(RootDirectory);
                    LogService.Info($"Created root directory: {RootDirectory}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create root directory '{RootDirectory}'");
                throw new InvalidOperationException($"Cannot create root directory '{RootDirectory}': {ex.Message}", ex);
            }
        }


        private void LoadSettingsFromService()
        {
            // Trigger property change notifications for all properties
            OnPropertyChanged(nameof(AutoStart));
            OnPropertyChanged(nameof(MinimizeToTray));
            OnPropertyChanged(nameof(EnableLogging));
            OnPropertyChanged(nameof(RefreshInterval));
            OnPropertyChanged(nameof(LogLevel));
            OnPropertyChanged(nameof(IsLogLevelEnabled));
            OnPropertyChanged(nameof(AutoAssignPorts));
            OnPropertyChanged(nameof(PortRangeStart));
            OnPropertyChanged(nameof(PortRangeEnd));
            OnPropertyChanged(nameof(RootDirectory));
            OnPropertyChanged(nameof(Theme));
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(AlwaysOnTop));
            
            // Notify UI of accent color changes
            OnPropertyChanged(nameof(AccentColor));
            OnPropertyChanged(nameof(AccentColorBrush));
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            LoadSettingsFromService();
            ApplyNotificationSettings();
        }

        private void ApplyThemeSettings()
        {
            try
            {
                _themeService.ApplyTheme(Theme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply theme settings");
            }
        }
        
        private void ApplyNotificationSettings()
        {
            try
            {
                // Sync notification settings from SettingsService to NotificationService
                var notificationService = NotificationService.Instance;
                notificationService.ShowSuccessNotifications = _settingsService.ShowSuccessNotifications;
                notificationService.ShowErrorNotifications = _settingsService.ShowErrorNotifications;
                notificationService.AutoDismissNotifications = _settingsService.AutoDismissNotifications;
                notificationService.DismissTimeout = _settingsService.DismissTimeout;
                
                if (Enum.TryParse<NotificationPosition>(_settingsService.NotificationPosition, out var position))
                {
                    notificationService.Position = position;
                    // Update the container position immediately
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.NotificationContainer?.UpdatePosition(position);
                    });
                }
                
                LogService.Info("Notification settings applied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply notification settings");
            }
        }

        private void ApplyWindowSettings()
        {
            try
            {
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.Topmost = AlwaysOnTop;
                    LogService.Info($"Window settings applied - AlwaysOnTop: {AlwaysOnTop}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply window settings");
            }
        }

        private void ApplyLanguageSettings()
        {
            try
            {
                if (_localizationService != null && !string.IsNullOrWhiteSpace(Language))
                {
                    // Store the display name before changing language (so we can use it after)
                    var languageDisplayName = GetLanguageDisplayName(Language);
                    
                    // Save language setting immediately so it persists
                    _settingsService.Language = Language;
                    _settingsService.SaveSettings();
                    
                    // Change the culture - this will trigger CultureChanged event
                    // which MainWindow subscribes to and will handle UI refresh
                    _localizationService.SetCulture(Language);
                    LogService.Info($"Language changed to: {Language}");
                    
                    // Update status message using the localization service (will be in the new language)
                    // Use a small delay to ensure culture change has propagated
                    DispatchToUI(() =>
                    {
                        var localizedMessage = _localizationService.GetString("LanguageChanged", languageDisplayName);
                        StatusMessage = localizedMessage;
                        
                        // Show success toast notification
                        NotificationService.Instance.ShowSuccess(
                            localizedMessage,
                            _localizationService.GetString("SettingsLanguageChanged"));
                    });
                    
                    // Don't refresh here - let MainWindow handle it via CultureChanged event
                    // This avoids double refresh
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply language settings");
                
                // Show error notification if language change fails
                if (_localizationService != null)
                {
                    NotificationService.Instance.ShowError(
                        _localizationService.GetString("ErrorChangingLanguage", ex.Message),
                        _localizationService.GetString("ErrorGeneric"));
                }
            }
        }


        private void ValidateProperty(object value, string propertyName)
        {
            var errors = new List<string>();
            
            switch (propertyName)
            {
                case nameof(RefreshInterval):
                    if (value is int refreshInterval && (refreshInterval < 5 || refreshInterval > 300))
                        errors.Add("Refresh interval must be between 5 and 300 seconds");
                    break;
                    
                case nameof(PortRangeStart):
                    if (value is int portStart && (portStart < 1024 || portStart > 65535))
                        errors.Add("Port range start must be between 1024 and 65535");
                    else if (value is int start && start >= PortRangeEnd)
                        errors.Add("Port range start must be less than port range end");
                    break;
                    
                case nameof(PortRangeEnd):
                    if (value is int endValue && (endValue < 1024 || endValue > 65535))
                        errors.Add("Port range end must be between 1024 and 65535.");
                    if (value is int end && end <= PortRangeStart)
                        errors.Add("Port range end must be greater than port range start.");
                    break;

                case nameof(MailCatcherPort):
                    if (value is int mcPort && (mcPort < 1 || mcPort > 65535))
                        errors.Add("Mail Catcher port must be between 1 and 65535.");
                    break;
                    
                case nameof(RootDirectory):
                    if (value is string rootDir)
                    {
                        if (string.IsNullOrWhiteSpace(rootDir))
                        {
                            errors.Add("Please provide a root directory path (e.g., C:\\ModStack)");
                        }
                        else
                        {
                            // Validate path format
                            try
                            {
                                var fullPath = Path.GetFullPath(rootDir);
                                
                                // Check if it's a valid path
                                if (!Path.IsPathRooted(fullPath))
                                    errors.Add("Please provide a full directory path (e.g., C:\\ModStack, not just ModStack)");
                                
                                // Note: We don't validate parent directory existence here
                                // Missing parent directories will be created when applying settings
                            }
                            catch (Exception ex)
                            {
                                // Provide user-friendly feedback for common path issues
                                if (ex is ArgumentException || ex is NotSupportedException)
                                {
                                    errors.Add("Please provide a valid directory path (avoid special characters like <, >, |, ?, *, \")");
                                }
                                else if (ex is PathTooLongException)
                                {
                                    errors.Add("Directory path is too long. Please use a shorter path.");
                                }
                                else
                                {
                                    errors.Add("Please provide a valid absolute directory path (e.g., C:\\ModStack)");
                                }
                            }
                        }
                    }
                    break;
            }
            
            SetValidationErrors(propertyName, errors);
        }
        
        private void SetValidationErrors(string propertyName, List<string> errors)
        {
            var hasErrorsBefore = HasErrors;
            
            if (errors.Count > 0)
                _validationErrors[propertyName] = errors;
            else
                _validationErrors.Remove(propertyName);
                
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            
            if (hasErrorsBefore != HasErrors)
            {
                OnPropertyChanged(nameof(HasErrors));
                NotifyCommandsCanExecuteChanged();
            }
        }
        
        private void NotifyCommandsCanExecuteChanged()
        {
            (SaveCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        }

        #endregion

        #region INotifyDataErrorInfo

        public bool HasErrors => _validationErrors.Count > 0;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _validationErrors.SelectMany(x => x.Value);
                
            return _validationErrors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _settingsService.SettingsChanged -= OnSettingsChanged;
            
            // Cancel all ongoing operations and dispose the master token source
            _masterCancellationTokenSource?.Cancel();
            _masterCancellationTokenSource?.Dispose();
            
            _disposed = true;
        }

        #endregion
    }
}
