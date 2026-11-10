using System;
using Microsoft.Win32;
using ModStack.Common;

namespace ModStack.Core.Services
{
    public class SettingsService : ISettingsService
    {
        public SettingsService()
        {
            LoadSettings();
        }
        
        // Application Settings
        public bool AutoStart { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool RememberMinimizeChoice { get; set; }
        public bool EnableLogging { get; set; }
        public int RefreshInterval { get; set; }
        public string LogLevel { get; set; }
        
        // Service Settings
        public bool AutoAssignPorts { get; set; }
        public int PortRangeStart { get; set; }
        public int PortRangeEnd { get; set; }
        public string InstallPath { get; set; }
        
        // Directory Settings
        public string RootDirectory { get; set; }
        public string ProjectsDirectory { get; set; }
        public string ServicesDirectory { get; set; }
        public string LogsDirectory { get; set; }
        
        // Mail Catcher Settings
        public bool EnableMailCatcher { get; set; }
        public int MailCatcherPort { get; set; }
        
        // Appearance Settings
        public string Theme { get; set; }
        public bool AlwaysOnTop { get; set; }
        public string AccentColor { get; set; }
        public string Language { get; set; }
        
        // Notification Settings
        public bool ShowSuccessNotifications { get; set; }
        public bool ShowErrorNotifications { get; set; }
        public bool AutoDismissNotifications { get; set; }
        public int DismissTimeout { get; set; }
        public string NotificationPosition { get; set; }
        
        public event EventHandler SettingsChanged;
        
        public void LoadSettings()
        {
            try
            {
                // Loading application settings from registry
                
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\ModStack"))
                {
                    if (key != null)
                    {
                        AutoStart = Convert.ToBoolean(key.GetValue("AutoStart", false));
                        MinimizeToTray = Convert.ToBoolean(key.GetValue("MinimizeToTray", true));
                        RememberMinimizeChoice = Convert.ToBoolean(key.GetValue("RememberMinimizeChoice", false));
                        EnableLogging = Convert.ToBoolean(key.GetValue("EnableLogging", true));
                        RefreshInterval = Convert.ToInt32(key.GetValue("RefreshInterval", 30));
                        LogLevel = key.GetValue("LogLevel", "Info").ToString();
                        
                        AutoAssignPorts = Convert.ToBoolean(key.GetValue("AutoAssignPorts", true));
                        PortRangeStart = Convert.ToInt32(key.GetValue("PortRangeStart", 8000));
                        PortRangeEnd = Convert.ToInt32(key.GetValue("PortRangeEnd", 9000));
                        InstallPath = key.GetValue("InstallPath", @"C:\ModStack\Services").ToString();
                        
                        // Load directory settings with cross-platform defaults
                        RootDirectory = key.GetValue("RootDirectory", Constants.DefaultPaths.ModStackRoot).ToString();
                        ProjectsDirectory = key.GetValue("ProjectsDirectory", Constants.DefaultPaths.Projects).ToString();
                        ServicesDirectory = key.GetValue("ServicesDirectory", Constants.DefaultPaths.Services).ToString();
                        LogsDirectory = key.GetValue("LogsDirectory", Constants.DefaultPaths.Logs).ToString();
                        
                        EnableMailCatcher = Convert.ToBoolean(key.GetValue("EnableMailCatcher", true));
                        MailCatcherPort = Convert.ToInt32(key.GetValue("MailCatcherPort", 1025));
                        
                        Theme = key.GetValue("Theme", "Light").ToString();
                        AlwaysOnTop = Convert.ToBoolean(key.GetValue("AlwaysOnTop", false));
                        // Load accent color from registry (stored as hex string)
                        AccentColor = key.GetValue("AccentColor", "#2196F3")?.ToString() ?? "#2196F3";
                        // Load language from registry (default to "en-US")
                        Language = key.GetValue("Language", "en-US")?.ToString() ?? "en-US";
                        
                        // Load notification settings
                        ShowSuccessNotifications = Convert.ToBoolean(key.GetValue("ShowSuccessNotifications", true));
                        ShowErrorNotifications = Convert.ToBoolean(key.GetValue("ShowErrorNotifications", true));
                        AutoDismissNotifications = Convert.ToBoolean(key.GetValue("AutoDismissNotifications", true));
                        DismissTimeout = Convert.ToInt32(key.GetValue("DismissTimeout", 5));
                        NotificationPosition = key.GetValue("NotificationPosition", "BottomRight")?.ToString() ?? "BottomRight";
                        
                        // Settings loaded successfully from registry
                    }
                    else
                    {
                        LoadDefaultSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex}");
                LoadDefaultSettings();
            }
        }
        
        public void SaveSettings()
        {
            try
            {
                // Saving application settings to registry
                
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\ModStack"))
                {
                    key?.SetValue("AutoStart", AutoStart);
                    key?.SetValue("MinimizeToTray", MinimizeToTray);
                    key?.SetValue("RememberMinimizeChoice", RememberMinimizeChoice);
                    key?.SetValue("EnableLogging", EnableLogging);
                    key?.SetValue("RefreshInterval", RefreshInterval);
                    key?.SetValue("LogLevel", LogLevel);
                    
                    key?.SetValue("AutoAssignPorts", AutoAssignPorts);
                    key?.SetValue("PortRangeStart", PortRangeStart);
                    key?.SetValue("PortRangeEnd", PortRangeEnd);
                    key?.SetValue("InstallPath", InstallPath);
                    
                    // Save directory settings
                    key?.SetValue("RootDirectory", RootDirectory);
                    key?.SetValue("ProjectsDirectory", ProjectsDirectory);
                    key?.SetValue("ServicesDirectory", ServicesDirectory);
                    key?.SetValue("LogsDirectory", LogsDirectory);
                    
                    key?.SetValue("EnableMailCatcher", EnableMailCatcher);
                    key?.SetValue("MailCatcherPort", MailCatcherPort);
                    
                    key?.SetValue("Theme", Theme);
                    key?.SetValue("AlwaysOnTop", AlwaysOnTop);
                    // Save accent color as hex string
                    key?.SetValue("AccentColor", AccentColor);
                    // Save language
                    key?.SetValue("Language", Language);
                    
                    // Save notification settings
                    key?.SetValue("ShowSuccessNotifications", ShowSuccessNotifications);
                    key?.SetValue("ShowErrorNotifications", ShowErrorNotifications);
                    key?.SetValue("AutoDismissNotifications", AutoDismissNotifications);
                    key?.SetValue("DismissTimeout", DismissTimeout);
                    key?.SetValue("NotificationPosition", NotificationPosition);
                }
                
                // Settings saved successfully to registry
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex}");
                throw;
            }
        }
        
        public void ApplySettings()
        {
            try
            {
                // Applying settings to application
                
                // Apply theme
                ApplyTheme();
                
                // Apply window settings
                ApplyWindowSettings();
                
                // Apply logging level
                ApplyLogLevel();
                
                // Settings applied successfully
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply settings: {ex}");
                throw;
            }
        }
        
        private void LoadDefaultSettings()
        {
            AutoStart = false;
            MinimizeToTray = true;
            EnableLogging = true;
            RefreshInterval = 30;
            LogLevel = "Info";
            
            AutoAssignPorts = true;
            PortRangeStart = 8000;
            PortRangeEnd = 9000;
            InstallPath = @"C:\ModStack\Services";
            
            // Set cross-platform default directories
            RootDirectory = Constants.DefaultPaths.ModStackRoot;
            ProjectsDirectory = Constants.DefaultPaths.Projects;
            ServicesDirectory = Constants.DefaultPaths.Services;
            LogsDirectory = Constants.DefaultPaths.Logs;
            
            EnableMailCatcher = true;
            MailCatcherPort = 1025;
            
            Theme = "Light";
            AlwaysOnTop = false;
            AccentColor = "#2196F3"; // Default blue
            Language = "en-US"; // Default language
            
            // Notification defaults
            ShowSuccessNotifications = true;
            ShowErrorNotifications = true;
            AutoDismissNotifications = true;
            DismissTimeout = 5;
            NotificationPosition = "BottomCenter";
            
            // Default settings loaded
        }
        
        private void ApplyTheme()
        {
            try
            {
                // Theme application will be handled in the GUI layer
                // This is just a placeholder for the Core service
                System.Diagnostics.Debug.WriteLine($"Theme setting: {Theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme: {ex}");
            }
        }
        
        private void ApplyWindowSettings()
        {
            try
            {
                // Window settings application will be handled in the GUI layer
                // This is just a placeholder for the Core service
                System.Diagnostics.Debug.WriteLine($"Window settings - AlwaysOnTop: {AlwaysOnTop}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply window settings: {ex}");
            }
        }
        
        private void ApplyLogLevel()
        {
            try
            {
                // Update the log service level
                // Note: Log level changes would need to be implemented in the GUI layer
                // since the Core project doesn't have access to the GUI's LogService
                System.Diagnostics.Debug.WriteLine($"Log level setting would be applied: {LogLevel}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply log level: {ex}");
            }
        }
    }
}
