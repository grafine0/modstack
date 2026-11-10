using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ModStack.GUI.Services;

namespace ModStack.GUI.Controls;

public partial class NotificationContainer : UserControl
{
    private readonly ObservableCollection<ToastNotification> _notifications = new();
    private NotificationPosition _currentPosition = NotificationPosition.BottomRight;

    public NotificationContainer()
    {
        InitializeComponent();
        NotificationsItemsControl.ItemsSource = _notifications;
        
        // Allow click-through on empty areas, but allow notifications to be interactive
        this.Background = null; // Null background allows click-through
        
        // Set default position (will be updated by NotificationService)
        UpdatePosition(NotificationPosition.BottomRight);
    }

    /// <summary>
    /// Show a notification
    /// </summary>
    public void ShowNotification(NotificationType type, string message, string title, int autoDismissSeconds = 0)
    {
        var notification = new ToastNotification();
        notification.Dismissed += (s, e) =>
        {
            _notifications.Remove(notification);
        };

        // Set the alignment based on current position
        HorizontalAlignment alignment;
        if (_currentPosition == NotificationPosition.TopLeft || _currentPosition == NotificationPosition.BottomLeft)
        {
            alignment = HorizontalAlignment.Left;
        }
        else if (_currentPosition == NotificationPosition.TopCenter || _currentPosition == NotificationPosition.BottomCenter)
        {
            alignment = HorizontalAlignment.Center;
        }
        else
        {
            alignment = HorizontalAlignment.Right;
        }
        notification.HorizontalAlignment = alignment;

        _notifications.Add(notification);
        notification.Show(type, message, title, autoDismissSeconds);

        // Limit to 5 notifications
        while (_notifications.Count > 5)
        {
            _notifications[0].Dismiss();
        }
    }

    /// <summary>
    /// Update position based on settings
    /// </summary>
    public void UpdatePosition(NotificationPosition position)
    {
        const int margin = 20;
        _currentPosition = position;
        
        switch (position)
        {
            case NotificationPosition.TopLeft:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Top;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Left;
                NotificationsItemsControl.Margin = new Thickness(margin, margin, 0, 0);
                break;

            case NotificationPosition.TopCenter:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Top;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Center;
                NotificationsItemsControl.Margin = new Thickness(0, margin, 0, 0);
                break;

            case NotificationPosition.TopRight:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Top;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Right;
                NotificationsItemsControl.Margin = new Thickness(0, margin, margin, 0);
                break;

            case NotificationPosition.BottomLeft:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Bottom;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Left;
                NotificationsItemsControl.Margin = new Thickness(margin, 0, 0, margin);
                break;

            case NotificationPosition.BottomCenter:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Bottom;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Center;
                NotificationsItemsControl.Margin = new Thickness(0, 0, 0, margin);
                break;

            case NotificationPosition.BottomRight:
                NotificationsItemsControl.VerticalAlignment = VerticalAlignment.Bottom;
                NotificationsItemsControl.HorizontalAlignment = HorizontalAlignment.Right;
                NotificationsItemsControl.Margin = new Thickness(0, 0, margin, margin);
                break;
        }
    }
}

