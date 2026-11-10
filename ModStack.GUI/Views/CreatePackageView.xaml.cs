using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ModStack.GUI.ViewModels;
using ModStack.Logging;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views;

/// <summary>
/// Interaction logic for CreatePackageView.xaml
/// </summary>
public partial class CreatePackageView : UserControl
{
    private readonly CreatePackageViewModel _viewModel;

    public CreatePackageView(CreatePackageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        
        // Subscribe to collection changes to detect newly added versions
        _viewModel.Versions.CollectionChanged += Versions_CollectionChanged;
    }

    private void Versions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Only handle additions
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is PackageVersionViewModel newVersion && newVersion.IsNewlyAdded)
                {
                    // Scroll to and highlight the new version
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ScrollToNewVersion(newVersion);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }
    }

    private void ScrollToNewVersion(PackageVersionViewModel newVersion)
    {
        try
        {
            // Find the ItemsControl
            if (VersionsItemsControl?.ItemContainerGenerator != null)
            {
                // Wait for the item to be generated
                VersionsItemsControl.UpdateLayout();
                
                // Find the container for the new version
                var container = VersionsItemsControl.ItemContainerGenerator.ContainerFromItem(newVersion);
                if (container is FrameworkElement element)
                {
                    // Scroll the container into view
                    element.BringIntoView();
                    
                    // Add a subtle highlight effect
                    UIAnimationHelper.HighlightElement(element);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Error scrolling to new version: {ex.Message}");
        }
    }

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

    #if DEBUG
        public CreatePackageView()
        {
            InitializeComponent();
            // Designer-only constructor
        }
    #endif
}

