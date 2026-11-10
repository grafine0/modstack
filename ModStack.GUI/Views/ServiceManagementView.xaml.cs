using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ModStack.GUI.ViewModels;
using ModStack.Common.Models;

namespace ModStack.GUI.Views;

/// <summary>
/// Interaction logic for ServiceManagementView.xaml
/// Following MVVM pattern - this code-behind only handles view-specific events
/// All business logic is in ServiceManagementViewModel
/// </summary>
public partial class ServiceManagementView : UserControl
{
        private readonly ILogger<ServiceManagementView> _logger;

        /// <summary>
        /// Gets the ViewModel for this view. Public for design-time data and testing support.
        /// </summary>
        public ServiceManagementViewModel? ViewModel => DataContext as ServiceManagementViewModel;

    #if DEBUG
        public ServiceManagementView()
        {
            InitializeComponent();
            // Only used by XAML designer — no logger
            _logger = null!; // Designer-only, will never be used
        }
    #endif

        public ServiceManagementView(ServiceManagementViewModel viewModel, ILogger<ServiceManagementView> logger)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    #if !DEBUG
        public ServiceManagementView()
        {
            throw new InvalidOperationException("This constructor is only for design-time support. Use DI to provide dependencies.");
        }
    #endif


    /// <summary>
    /// Public method to refresh services (called from MainWindow after installations)
    /// </summary>
    public async System.Threading.Tasks.Task RefreshServicesAsync()
    {
        if (ViewModel == null) 
        {
            _logger?.LogWarning("ViewModel is null, cannot refresh services");
            return;
        }

        try
        {
            // ViewModel handles its own logging and UI thread marshalling
            await ViewModel.RefreshAfterInstallationAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log at View level for infrastructure issues
            _logger?.LogError(ex, "Failed to refresh services in ServiceManagementView");
            
            // Fallback to debug output if logger is not available
            if (_logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"ServiceManagementView: Error refreshing services: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Public method to update progress (called from external progress indicators)
    /// </summary>
    public void UpdateProgress(string status, int percentage)
    {
        try
        {
            // ViewModel handles its own logging
            ViewModel?.UpdateProgress(status, percentage);
        }
        catch (Exception ex)
        {
            // Log at View level for infrastructure issues
            _logger?.LogError(ex, "Failed to update progress in ServiceManagementView");
            
            // Fallback to debug output if logger is not available
            if (_logger == null)
            {
                System.Diagnostics.Debug.WriteLine($"ServiceManagementView: Error updating progress: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handle AutoStart checkbox checked event
    /// </summary>
    private void AutoStartCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("🔔 AutoStartCheckBox_Checked event fired");
        
        if (sender is CheckBox checkBox && checkBox.DataContext is ModStack.Common.Models.ServiceDisplayItem service)
        {
            System.Diagnostics.Debug.WriteLine($"🔔 AutoStart checkbox checked for {service.Name}, current value: {service.AutoStart}");
            _logger?.LogDebug("AutoStart checkbox checked for {ServiceName}, current value: {AutoStart}", service.Name, service.AutoStart);
            
            // Execute the AutoStart toggle command
            if (ViewModel?.AutoStartToggleCommand?.CanExecute(service) == true)
            {
                System.Diagnostics.Debug.WriteLine($"🔔 Executing AutoStartToggleCommand for {service.Name}");
                ViewModel.AutoStartToggleCommand.Execute(service);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔔 AutoStartToggleCommand cannot execute for {service.Name}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("🔔 AutoStartCheckBox_Checked: Invalid sender or DataContext");
        }
    }

    /// <summary>
    /// Handle AutoStart checkbox unchecked event
    /// </summary>
    private void AutoStartCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("🔔 AutoStartCheckBox_Unchecked event fired");
        
        if (sender is CheckBox checkBox && checkBox.DataContext is ModStack.Common.Models.ServiceDisplayItem service)
        {
            System.Diagnostics.Debug.WriteLine($"🔔 AutoStart checkbox unchecked for {service.Name}, current value: {service.AutoStart}");
            _logger?.LogDebug("AutoStart checkbox unchecked for {ServiceName}, current value: {AutoStart}", service.Name, service.AutoStart);
            
            // Execute the AutoStart toggle command
            if (ViewModel?.AutoStartToggleCommand?.CanExecute(service) == true)
            {
                System.Diagnostics.Debug.WriteLine($"🔔 Executing AutoStartToggleCommand for {service.Name}");
                ViewModel.AutoStartToggleCommand.Execute(service);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"🔔 AutoStartToggleCommand cannot execute for {service.Name}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("🔔 AutoStartCheckBox_Unchecked: Invalid sender or DataContext");
        }
    }

    /// <summary>
    /// Handle Start Service button click
    /// </summary>
    private async void StartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceDisplayItem service)
        {
            if (service.StartServiceAction != null)
            {
                await service.StartServiceAction(service);
            }
        }
    }

    /// <summary>
    /// Handle Stop Service button click
    /// </summary>
    private async void StopService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceDisplayItem service)
        {
            if (service.StopServiceAction != null)
            {
                await service.StopServiceAction(service);
            }
        }
    }

    /// <summary>
    /// Handle Configure Service button click
    /// </summary>
    private async void ConfigureService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceDisplayItem service)
        {
            if (service.ConfigureServiceAction != null)
            {
                await service.ConfigureServiceAction(service);
            }
        }
    }

    /// <summary>
    /// Handle Delete Service button click
    /// </summary>
    private async void DeleteService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceDisplayItem service)
        {
            if (service.DeleteServiceAction != null)
            {
                await service.DeleteServiceAction(service);
            }
        }
    }
}
