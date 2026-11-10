using System.Windows;
using System.Windows.Controls;
using ModStack.Core.Interfaces;
using ModStack.Common.Models;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Dialog for configuring service settings within a stack
/// </summary>
public partial class StackServiceConfigurationDialog : Window
{
    public ServiceConfigurationViewModel ViewModel { get; private set; }
    private readonly IServiceInstallationChecker _serviceInstallationChecker;

    public StackServiceConfigurationDialog(
        IServiceInstallationChecker serviceInstallationChecker,
        ServiceDisplayItem service)
    {
        InitializeComponent();
        
        _serviceInstallationChecker = serviceInstallationChecker;
        
        // Ensure the service has a proper status if it's "Unknown"
        if (string.Equals(service.Status, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            service.Status = _serviceInstallationChecker.GetServiceInstallationStatus(service.Path, service.Type);
        }
        
        ViewModel = new ServiceConfigurationViewModel(service);
        DataContext = ViewModel;
        
        // Ensure proper centering
        this.Loaded += (s, e) => CenterDialog();
    }
    
    public StackServiceConfigurationDialog(
        IServiceInstallationChecker serviceInstallationChecker,
        StackService stackService)
    {
        InitializeComponent();
        
        _serviceInstallationChecker = serviceInstallationChecker;
        
        // Convert StackService to ServiceDisplayItem
        var serviceDisplayItem = new ServiceDisplayItem
        {
            Id = stackService.Id,
            Name = stackService.Service.DisplayName,
            Version = stackService.Service.Version,
            Type = stackService.Service.ServiceType,
            Status = _serviceInstallationChecker.GetServiceInstallationStatus(stackService.Service),
            Port = stackService.Port ?? 0,
            Path = stackService.Service.BinaryPath,
            AutoStart = stackService.IsEnabled
        };
        
        ViewModel = new ServiceConfigurationViewModel(serviceDisplayItem);
        DataContext = ViewModel;
        
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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate and apply the JSON configuration using the ViewModel
            if (ViewModel.TryApplyJsonConfiguration(out var errors))
            {
                DialogResult = true;
                Close();
            }
            else
            {
                var errorMessage = string.Join("\n", errors);
                ModernDialogService.ShowError($"Configuration validation failed:\n\n{errorMessage}", "Configuration Error");
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Error saving configuration: {ex.Message}", "Save Error");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = null; // User closed without explicit choice
        Close();
    }

    private void ConfigurationFilesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenConfigurationFiles();
    }
}

