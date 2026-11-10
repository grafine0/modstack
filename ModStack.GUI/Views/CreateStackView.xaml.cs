using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ModStack.Common.Models;
using ModStack.Core.Interfaces;
using ModStack.Services.Interfaces;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Views.Dialogs;
using ModStack.Data.Interfaces;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views;

/// <summary>
/// Interaction logic for CreateStackView.xaml
/// Clean MVVM implementation with minimal code-behind
/// </summary>
public partial class CreateStackView : UserControl
{
    private readonly CreateStackViewModel _viewModel;

    public CreateStackView(CreateStackViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        // Wire up events for navigation
        _viewModel.BackRequested += OnBackRequested;
        _viewModel.StackCreated += OnStackCreated;
        
        Loaded += OnViewLoaded;
    }

    private async void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowError($"Failed to initialize view: {ex.Message}", "Initialization Error");
        }
    }

    #region Event Handlers

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateBack();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateBack();
    }

    private void OnBackRequested(object? sender, EventArgs e)
    {
        NavigateBack();
    }

    private void OnStackCreated(object? sender, Stack stack)
    {
        NavigateToEditStack(stack);
    }

    #endregion

    #region Navigation Methods

    private void NavigateBack()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.StacksButton_Click(this, new RoutedEventArgs());
        }
    }

    private async void NavigateToEditStack(Stack stack)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(100);
            
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.CreateStackButton_Click(this, new RoutedEventArgs(), stack);
            }
            else
            {
                ModernDialogService.ShowWarning("Stack was created but could not navigate to edit page. Please navigate manually.", "Navigation Warning");
                NavigateBack();
            }
        }
        catch (Exception ex)
        {
            ModernDialogService.ShowWarning($"Stack was created but navigation failed: {ex.Message}. Please navigate manually.", "Navigation Warning");
            NavigateBack();
        }
    }

    #endregion

    #region Event Handlers

    private async void ViewServiceInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceSelectionItem service)
        {
            var dialogService = ModStack.GUI.Services.ServiceLocator.ServiceProvider.GetService(typeof(ModStack.GUI.Services.Interfaces.IDialogService)) as ModStack.GUI.Services.Interfaces.IDialogService;
            if (dialogService != null)
            {
                await dialogService.ShowInformationAsync(
                    $"Service: {service.Name}\nVersion: {service.Version}\nType: {service.ServiceType}\nPort: {service.Port}\nPriority: {service.Priority}",
                    "Service Information");
            }
        }
    }

    private void LAMPTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyLAMPTemplateCommand.Execute(null);
    }

    private void LEMPTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyLEMPTemplateCommand.Execute(null);
    }

    private void DatabaseTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyDatabaseTemplateCommand.Execute(null);
    }

    private void CustomTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyCustomTemplateCommand.Execute(null);
    }

    #endregion

    #region Service Configuration Dialog

    private async void ConfigureService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ServiceSelectionItem service)
        {
            try
            {
                var stackManager = ServiceLocator.GetService<IStackManager>();
                var serviceRepository = ServiceLocator.GetService<IServiceRepository>();
                
                ServiceDefinition? serviceFromDb = null;
                
                if (service.DatabaseId > 0)
                {
                    serviceFromDb = await serviceRepository.GetByIdAsync(service.DatabaseId);
                }
                
                serviceFromDb ??= new()
                {
                    Id = service.DatabaseId,
                    Name = service.Name,
                    DisplayName = service.Name.ToUpperInvariant(),
                    ServiceType = System.Enum.TryParse<ModStack.Common.ServiceType>(service.ServiceType, out var serviceType) 
                        ? serviceType 
                        : ModStack.Common.ServiceType.Apache,
                    Version = service.Version,
                        DefaultPort = service.Port,
                        BinaryPath = "",
                        IsInstalled = service.IsInstalled,
                        IsEnabled = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                };

                var stackService = new StackService
                {
                    ServiceId = serviceFromDb.Id,
                    Service = serviceFromDb,
                    Port = service.Port,
                    Priority = service.Priority,
                    Configuration = []
                };

                var serviceProvider = ModStack.GUI.Services.ServiceLocator.ServiceProvider;
                var dialog = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<StackServiceConfigurationDialog>(
                    serviceProvider, stackService);
                dialog.Owner = Application.Current.MainWindow;
                Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
            }
            catch (Exception ex)
            {
                ModernDialogService.ShowError($"Error opening service configuration: {ex.Message}", "Configuration Error");
            }
        }
    }

    #endregion

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