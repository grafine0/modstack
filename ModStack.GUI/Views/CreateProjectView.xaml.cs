using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views;

/// <summary>
/// Interaction logic for CreateProjectView.xaml - MVVM pattern implementation
/// </summary>
public partial class CreateProjectView : UserControl
{
    public CreateProjectView(CreateProjectViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    /// <summary>
    /// Gets the strongly-typed ViewModel for design-time binding and code-behind access
    /// </summary>
    public CreateProjectViewModel? ViewModel => DataContext as CreateProjectViewModel;

    #region Button Animation

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Trigger highlight animation on the save/update button
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
