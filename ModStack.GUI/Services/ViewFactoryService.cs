using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ModStack.Common.Models;
using ModStack.GUI.Views;
using ModStack.GUI.ViewModels;

namespace ModStack.GUI.Services;

/// <summary>
/// View factory service that uses proper DI instead of ServiceLocator
/// </summary>
public class ViewFactoryService : IViewFactoryService
{
    private readonly IServiceProvider _serviceProvider;

    public ViewFactoryService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public UserControl GetCreateProjectView()
    {
        // The DI container will automatically inject the CreateProjectViewModel
        return _serviceProvider.GetRequiredService<CreateProjectView>();
    }

    public UserControl GetCreateProjectViewForEdit(Project project)
    {
        // Get ViewModel and configure it for editing, then create view via DI
        var viewModel = _serviceProvider.GetRequiredService<CreateProjectViewModel>();
        
        // Configure ViewModel for editing (async operation will be handled by InitializeCommand)
        _ = viewModel.InitializeForEditAsync(project);
        
        // Get the view via DI with the pre-configured ViewModel
        return new CreateProjectView(viewModel);
    }

    public UserControl GetSettingsView()
    {
        return _serviceProvider.GetRequiredService<SettingsView>();
    }

    public T GetView<T>() where T : UserControl
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public TControl CreateView<TControl>(params object[] parameters) where TControl : UserControl
    {
        return ActivatorUtilities.CreateInstance<TControl>(_serviceProvider, parameters);
    }

    public TWindow CreateDialog<TWindow>(params object[] parameters) where TWindow : System.Windows.Window
    {
        return ActivatorUtilities.CreateInstance<TWindow>(_serviceProvider, parameters);
    }
}
