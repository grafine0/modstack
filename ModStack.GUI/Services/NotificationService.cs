using System;
using System.Windows;
using System.Windows.Threading;
using ModStack.GUI.Controls;
using ModStack.Logging;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for displaying toast notifications
/// </summary>
public class NotificationService
{
    private static NotificationService? _instance;
    private NotificationContainer? _notificationContainer;

    // Notification settings
    public bool ShowSuccessNotifications { get; set; } = true;
    public bool ShowErrorNotifications { get; set; } = true;
    public bool AutoDismissNotifications { get; set; } = true;
    public int DismissTimeout { get; set; } = 5; // seconds
    public NotificationPosition Position { get; set; } = NotificationPosition.BottomRight;

    public static NotificationService Instance
    {
        get
        {
            _instance ??= new NotificationService();
            return _instance;
        }
    }

    private NotificationService()
    {
        LoadSettings();
    }

    /// <summary>
    /// Initialize the notification container
    /// </summary>
    public void Initialize(NotificationContainer container)
    {
        _notificationContainer = container;
        UpdateContainerPosition();
    }

    /// <summary>
    /// Show a success notification
    /// </summary>
    public void ShowSuccess(string message, string? title = null)
    {
        if (!ShowSuccessNotifications) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationContainer?.ShowNotification(
                NotificationType.Success,
                message,
                title ?? "Success",
                AutoDismissNotifications ? DismissTimeout : 0
            );
        });

        LogService.Info($"[Notification] Success: {message}");
    }

    /// <summary>
    /// Show an error notification
    /// </summary>
    public void ShowError(string message, string? title = null)
    {
        if (!ShowErrorNotifications) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationContainer?.ShowNotification(
                NotificationType.Error,
                message,
                title ?? "Error",
                AutoDismissNotifications ? DismissTimeout : 0
            );
        });

        LogService.Error($"[Notification] Error: {message}");
    }

    /// <summary>
    /// Show an info notification
    /// </summary>
    public void ShowInfo(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationContainer?.ShowNotification(
                NotificationType.Info,
                message,
                title ?? "Info",
                AutoDismissNotifications ? DismissTimeout : 0
            );
        });

        LogService.Info($"[Notification] Info: {message}");
    }

    /// <summary>
    /// Show a warning notification
    /// </summary>
    public void ShowWarning(string message, string? title = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationContainer?.ShowNotification(
                NotificationType.Warning,
                message,
                title ?? "Warning",
                AutoDismissNotifications ? DismissTimeout : 0
            );
        });

        LogService.Warning($"[Notification] Warning: {message}");
    }

    /// <summary>
    /// Load settings from main registry location (now managed by SettingsService)
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ModStack");
            
            if (key != null)
            {
                ShowSuccessNotifications = Convert.ToBoolean(key.GetValue("ShowSuccessNotifications", true));
                ShowErrorNotifications = Convert.ToBoolean(key.GetValue("ShowErrorNotifications", true));
                AutoDismissNotifications = Convert.ToBoolean(key.GetValue("AutoDismissNotifications", true));
                DismissTimeout = Convert.ToInt32(key.GetValue("DismissTimeout", 5));
                
                var positionValue = key.GetValue("NotificationPosition", "BottomRight")?.ToString() ?? "BottomRight";
                Position = Enum.TryParse<NotificationPosition>(positionValue, out var position) 
                    ? position 
                    : NotificationPosition.BottomRight;

                UpdateContainerPosition();
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to load notification settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Update container position based on settings
    /// </summary>
    private void UpdateContainerPosition()
    {
        _notificationContainer?.UpdatePosition(Position);
    }
}

/// <summary>
/// Notification type
/// </summary>
public enum NotificationType
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Notification position on screen
/// </summary>
public enum NotificationPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

