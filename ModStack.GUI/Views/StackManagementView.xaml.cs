using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;

namespace ModStack.GUI.Views;

/// <summary>
/// Stack Management View - MVVM implementation
/// </summary>
public partial class StackManagementView : UserControl
{
    private readonly StackManagementViewModel _viewModel;

    public StackManagementView(StackManagementViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = _viewModel;
        
        // Handle visibility changes to refresh data when view becomes visible
        this.IsVisibleChanged += StackManagementView_IsVisibleChanged;
        
        Debug.WriteLine("📦 StackManagementView initialized with clean MVVM pattern");
    }

    private async void StackManagementView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && _viewModel != null)
        {
            // Refresh data when view becomes visible
            Debug.WriteLine("📦 StackManagementView became visible, refreshing data");
            await _viewModel.LoadStacksAsync();
        }
    }
}
