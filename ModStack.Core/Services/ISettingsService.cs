using System;

namespace ModStack.Core.Services
{
    public interface ISettingsService
    {
        // Application Settings
        bool AutoStart { get; set; }
        bool MinimizeToTray { get; set; }
        bool RememberMinimizeChoice { get; set; }
        bool EnableLogging { get; set; }
        int RefreshInterval { get; set; }
        string LogLevel { get; set; }
        
        // Service Settings
        bool AutoAssignPorts { get; set; }
        int PortRangeStart { get; set; }
        int PortRangeEnd { get; set; }
        string InstallPath { get; set; }
        
        // Directory Settings
        string RootDirectory { get; set; }
        string ProjectsDirectory { get; set; }
        string ServicesDirectory { get; set; }
        string LogsDirectory { get; set; }
        
        // Mail Catcher Settings
        bool EnableMailCatcher { get; set; }
        int MailCatcherPort { get; set; }
        
        // Appearance Settings
        string Theme { get; set; }
        bool AlwaysOnTop { get; set; }
        string AccentColor { get; set; }
        string Language { get; set; }
        
        // Notification Settings
        bool ShowSuccessNotifications { get; set; }
        bool ShowErrorNotifications { get; set; }
        bool AutoDismissNotifications { get; set; }
        int DismissTimeout { get; set; }
        string NotificationPosition { get; set; }
        
        // Methods
        void LoadSettings();
        void SaveSettings();
        void ApplySettings();
        event EventHandler SettingsChanged;
    }
}
