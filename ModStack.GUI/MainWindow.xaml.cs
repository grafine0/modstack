using System;
using System.Windows;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ModStack.GUI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private TaskbarIcon? _taskbarIcon;
    private bool _userRequestedMinimizeToTray = false;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        
        // Set DataContext for MVVM bindings
        DataContext = viewModel;

        // Set version dynamically from assembly
        if (VersionTextBlock != null)
        {
            VersionTextBlock.Text = $"v{ModStack.Common.Constants.ApplicationVersion}";
        }

        // Initialize system tray
        InitializeSystemTray();

        // Initialize notification service
        NotificationService.Instance.Initialize(NotificationContainer);

        // Register this window with the dialog blur helper
        Helpers.DialogBlurHelper.SetMainWindow(this);

        this.IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                _userRequestedMinimizeToTray = false;
            }
        };

        // On Load, navigate to dashboard
        this.Loaded += (s, e) =>
        {
            viewModel.NavigateCommand.Execute("Dashboard");
        };
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                DragMove();
            }
        }
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsService = ServiceLocator.GetServiceOptional<ModStack.Core.Services.ISettingsService>();
        if (settingsService != null && settingsService.MinimizeToTray)
        {
            _userRequestedMinimizeToTray = true;
            this.Hide();
        }
        else
        {
            WindowState = WindowState.Minimized;
        }
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var settingsService = ServiceLocator.GetServiceOptional<ModStack.Core.Services.ISettingsService>();
        if (settingsService != null && settingsService.MinimizeToTray)
        {
            _userRequestedMinimizeToTray = true;
            this.Hide();
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

    private void InitializeSystemTray()
    {
        try
        {
            _taskbarIcon = new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app-icon.ico")).Stream),
                ToolTipText = "ModStack"
            };

            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var openItem = new System.Windows.Controls.MenuItem { Header = "Open ModStack" };
            openItem.Click += (s, e) => ShowWindow();
            
            var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(exitItem);

            _taskbarIcon.ContextMenu = contextMenu;

            _taskbarIcon.TrayMouseDoubleClick += (s, e) => ShowWindow();
        }
        catch (Exception ex)
        {
            LogService.Warning($"Failed to initialize system tray icon: {ex.Message}");
        }
    }

    private void ShowWindow()
    {
        this.Show();
        if (this.WindowState == WindowState.Minimized)
        {
            this.WindowState = WindowState.Normal;
        }
        this.Activate();
        this.Topmost = true;
        this.Topmost = false;
        this.Focus();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        
        var settingsService = ServiceLocator.GetServiceOptional<ModStack.Core.Services.ISettingsService>();
        if (WindowState == WindowState.Minimized && 
            settingsService != null && 
            settingsService.MinimizeToTray && 
            !_userRequestedMinimizeToTray)
        {
            this.Hide();
        }
    }

    #region MVVM Legacy Adapters

    // These methods provide backward compatibility for ViewModels that are still
    // tightly coupled to MainWindow. They route requests through the new MVVM architecture.

    public static void UpdateStatusBar(string message)
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage(message));
    }

    public static void ResetStatusBar()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StatusBarMessage("Ready"));
    }

    public static void NotifyServiceInstalled()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.ServiceInstalledMessage());
    }

    public static void NotifyStackUpdated()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.StackUpdatedMessage());
    }

    public void CreateStackButton_Click(object sender, RoutedEventArgs e, ModStack.Common.Models.Stack? stack = null)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        if (stack == null)
            nav?.NavigateToCreateStack();
        else
            nav?.NavigateToEditStack(stack);
    }

    public void CreateProjectButton_Click(object sender, RoutedEventArgs e)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToCreateProject();
    }

    public void StacksButton_Click(object sender, RoutedEventArgs e)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToStackManagement();
    }

    public void ServicesButton_Click(object sender, RoutedEventArgs e)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToServiceManagement();
    }

    public void PackageManagerButton_Click(object sender, RoutedEventArgs e)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToPackageManager();
    }

    public void CreatePackageButton_Click(object sender, RoutedEventArgs e)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToCreatePackage();
    }

    public void EditPackageButton_Click(object package)
    {
        // For now, route to package manager as edit isn't explicitly defined in INavigationService yet
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToPackageManager();
    }

    public void EditProjectButton_Click(object project)
    {
        var nav = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.INavigationService>();
        nav?.NavigateToProjectManagement();
    }

    public void ShowBlurOverlay()
    {
        // The blur overlay effect has been refactored or can be ignored in the new MVVM layout
    }

    public void HideBlurOverlay()
    {
        // The blur overlay effect has been refactored or can be ignored in the new MVVM layout
    }

    public void SetCurrentView(System.Windows.Controls.UserControl view)
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new ModStack.GUI.Models.NavigationMessage(view));
    }

    public void EnableAllNavigationButtons()
    {
        // No longer needed, handled by bindings and commands
    }

    #endregion
}
