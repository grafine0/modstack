using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ModStack.Core.Services;
using ModStack.GUI.Models;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Helpers;
using ModStack.Common;

namespace ModStack.GUI.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>
    /// Strongly-typed ViewModel property for convenience in code-behind and design-time
    /// </summary>
    public SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        // Set version dynamically from assembly
        if (VersionTextBlock != null)
        {
            VersionTextBlock.Text = $"Version {Constants.ApplicationVersion}";
        }
        
        // Apply current logging and theme settings
        var settingsService = ModStack.GUI.Services.ServiceLocator.ServiceProvider.GetService(typeof(ModStack.Core.Services.ISettingsService)) as ModStack.Core.Services.ISettingsService;
        var themeService = ModStack.GUI.Services.ServiceLocator.ServiceProvider.GetService(typeof(ModStack.GUI.Services.Interfaces.IThemeService)) as ModStack.GUI.Services.Interfaces.IThemeService;
        
        if (settingsService != null && themeService != null)
        {
            LogService.SetLoggingEnabled(settingsService.EnableLogging);
            themeService.ApplyTheme(Enum.TryParse<ThemeOption>(settingsService.Theme, out var theme) ? theme : ThemeOption.Light);
        }
    }

    #region Button Animation

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Trigger highlight animation on the save button
        // Use Dispatcher to ensure animation starts before command execution blocks UI
        if (sender is Button button)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UIAnimationHelper.HighlightButton(button);
            }, DispatcherPriority.Loaded);
        }
    }

    #endregion

}
