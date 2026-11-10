using System.Windows.Controls;
using ModStack.Common.Models;

namespace ModStack.GUI.Services;

/// <summary>
/// Service for handling navigation between views using proper DI
/// </summary>
public interface IViewFactoryService
{
    /// <summary>
    /// Navigates to the create project view
    /// </summary>
    UserControl GetCreateProjectView();

    /// <summary>
    /// Navigates to the create project view for editing an existing project
    /// </summary>
    UserControl GetCreateProjectViewForEdit(Project project);

    /// <summary>
    /// Gets the settings view
    /// </summary>
    UserControl GetSettingsView();

    /// <summary>
    /// Gets other views through the DI container
    /// </summary>
    T GetView<T>() where T : UserControl;

    /// <summary>
    /// Creates a view of type TControl, dynamically injecting dependencies alongside any provided runtime parameters.
    /// </summary>
    TControl CreateView<TControl>(params object[] parameters) where TControl : UserControl;

    /// <summary>
    /// Creates a Window/Dialog of type TWindow, dynamically injecting dependencies alongside any provided runtime parameters.
    /// </summary>
    TWindow CreateDialog<TWindow>(params object[] parameters) where TWindow : System.Windows.Window;
}
