using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Modern confirmation dialog with professional styling
/// </summary>
public partial class ModernConfirmationDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = "Confirmation";
    public new string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    private string _message = string.Empty;
    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged(nameof(Message));
        }
    }

    private string _details = string.Empty;
    public string Details
    {
        get => _details;
        set
        {
            _details = value;
            OnPropertyChanged(nameof(Details));
        }
    }

    private List<string> _serviceList = new();
    public List<string> ServiceList
    {
        get => _serviceList;
        set
        {
            _serviceList = value;
            OnPropertyChanged(nameof(ServiceList));
        }
    }

    private string _iconText = "ℹ";
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            OnPropertyChanged(nameof(IconText));
        }
    }

    private Brush _iconBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue
    public Brush IconBackground
    {
        get => _iconBackground;
        set
        {
            _iconBackground = value;
            OnPropertyChanged(nameof(IconBackground));
        }
    }

    private bool _showOkButton = true;
    public bool ShowOkButton
    {
        get => _showOkButton;
        set
        {
            _showOkButton = value;
            OnPropertyChanged(nameof(ShowOkButton));
        }
    }

    private bool _showYesButton = false;
    public bool ShowYesButton
    {
        get => _showYesButton;
        set
        {
            _showYesButton = value;
            OnPropertyChanged(nameof(ShowYesButton));
        }
    }

    private bool _showNoButton = false;
    public bool ShowNoButton
    {
        get => _showNoButton;
        set
        {
            _showNoButton = value;
            OnPropertyChanged(nameof(ShowNoButton));
        }
    }

    private bool _showCancelButton = false;
    public bool ShowCancelButton
    {
        get => _showCancelButton;
        set
        {
            _showCancelButton = value;
            OnPropertyChanged(nameof(ShowCancelButton));
        }
    }

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    public ModernConfirmationDialog()
    {
        InitializeComponent();
        DataContext = this;
        
        // Ensure proper centering
        this.Loaded += (s, e) => CenterDialog();
    }

    private void CenterDialog()
    {
        if (Owner != null)
        {
            // Center relative to owner window
            Left = Owner.Left + (Owner.Width - Width) / 2;
            Top = Owner.Top + (Owner.Height - Height) / 2;
        }
        else
        {
            // Center on screen using WPF
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 2;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        DialogResult = false;
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        DialogResult = true;
        Close();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        DialogResult = false;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        DialogResult = false;
        Close();
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Show a modern confirmation dialog
    /// </summary>
    public static MessageBoxResult Show(string message, string title = "Confirmation", 
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information,
        List<string>? serviceList = null, string? details = null)
    {
        var dialog = new ModernConfirmationDialog
        {
            Title = title,
            Message = message,
            Details = details ?? string.Empty,
            ServiceList = serviceList ?? new List<string>(),
            Owner = Application.Current.MainWindow
        };

        // Configure buttons based on MessageBoxButton
        switch (buttons)
        {
            case MessageBoxButton.OK:
                dialog.ShowOkButton = true;
                dialog.ShowYesButton = false;
                dialog.ShowNoButton = false;
                dialog.ShowCancelButton = false;
                break;
            case MessageBoxButton.OKCancel:
                dialog.ShowOkButton = true;
                dialog.ShowYesButton = false;
                dialog.ShowNoButton = false;
                dialog.ShowCancelButton = true;
                break;
            case MessageBoxButton.YesNo:
                dialog.ShowOkButton = false;
                dialog.ShowYesButton = true;
                dialog.ShowNoButton = true;
                dialog.ShowCancelButton = false;
                break;
            case MessageBoxButton.YesNoCancel:
                dialog.ShowOkButton = false;
                dialog.ShowYesButton = true;
                dialog.ShowNoButton = true;
                dialog.ShowCancelButton = true;
                break;
        }

        // Configure icon based on MessageBoxImage
        switch (icon)
        {
            case MessageBoxImage.Information:
                dialog.IconText = "ℹ";
                dialog.IconBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue
                break;
            case MessageBoxImage.Question:
                dialog.IconText = "?";
                dialog.IconBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)); // Orange
                break;
            case MessageBoxImage.Warning:
                dialog.IconText = "⚠";
                dialog.IconBackground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)); // Orange
                break;
            case MessageBoxImage.Error:
                dialog.IconText = "✖";
                dialog.IconBackground = new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // Red
                break;
            default:
                dialog.IconText = "ℹ";
                dialog.IconBackground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)); // Blue
                break;
        }

        Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
        return dialog.Result;
    }
}