using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ModStack.GUI.Services;

namespace ModStack.GUI.Controls;

public partial class ToastNotification : UserControl
{
    private DispatcherTimer? _dismissTimer;
    public event EventHandler? Dismissed;

    public ToastNotification()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Show the notification
    /// </summary>
    public void Show(NotificationType type, string message, string title, int autoDismissSeconds = 0)
    {
        // Set content
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        // Set style based on type
        switch (type)
        {
            case NotificationType.Success:
                AccentBar.Background = (Brush)FindResource("SuccessBrush");
                IconTextBlock.Foreground = (Brush)FindResource("SuccessBrush");
                IconTextBlock.Text = "✓";
                break;

            case NotificationType.Error:
                AccentBar.Background = (Brush)FindResource("DangerBrush");
                IconTextBlock.Foreground = (Brush)FindResource("DangerBrush");
                IconTextBlock.Text = "✗";
                break;

            case NotificationType.Warning:
                AccentBar.Background = (Brush)FindResource("WarningBrush");
                IconTextBlock.Foreground = (Brush)FindResource("WarningBrush");
                IconTextBlock.Text = "⚠";
                break;

            case NotificationType.Info:
                AccentBar.Background = (Brush)FindResource("InfoBrush");
                IconTextBlock.Foreground = (Brush)FindResource("InfoBrush");
                IconTextBlock.Text = "ℹ";
                break;
        }

        // Slide in animation
        var slideIn = new DoubleAnimation
        {
            From = 50,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(300)
        };

        SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
        this.BeginAnimation(OpacityProperty, fadeIn);

        // Auto-dismiss timer
        if (autoDismissSeconds > 0)
        {
            _dismissTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(autoDismissSeconds)
            };
            _dismissTimer.Tick += (s, e) =>
            {
                _dismissTimer.Stop();
                Dismiss();
            };
            _dismissTimer.Start();
        }
    }

    /// <summary>
    /// Dismiss the notification
    /// </summary>
    public void Dismiss()
    {
        _dismissTimer?.Stop();

        // Slide out animation
        var slideOut = new DoubleAnimation
        {
            From = 0,
            To = 50,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };

        slideOut.Completed += (s, e) =>
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
        };

        SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
        this.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
    }
}

