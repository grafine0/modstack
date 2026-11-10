using System.Windows.Controls;

namespace ModStack.GUI.Models;

/// <summary>
/// Message sent when a navigation request is processed and the view should be changed.
/// </summary>
/// <param name="View">The new UserControl to display.</param>
public record NavigationMessage(UserControl View);

/// <summary>
/// Message sent to update the application status bar text.
/// </summary>
/// <param name="Message">The text to display.</param>
public record StatusBarMessage(string Message);

/// <summary>
/// Message sent when a new service is installed.
/// </summary>
public record ServiceInstalledMessage();

/// <summary>
/// Message sent when a stack configuration is updated.
/// </summary>
public record StackUpdatedMessage();
