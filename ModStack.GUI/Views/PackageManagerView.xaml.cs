using System.Windows.Controls;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using ModStack.Services;
using ModStack.Data.Interfaces;

namespace ModStack.GUI.Views
{
    /// <summary>
    /// Interaction logic for PackageManagerView.xaml
    /// Clean MVVM implementation with minimal code-behind
    /// </summary>
    public partial class PackageManagerView : UserControl
    {
        public PackageManagerView(PackageManagerViewModel viewModel)
        {
            InitializeComponent();
            
            DataContext = viewModel;
            
            // Initialize the ViewModel asynchronously when the view is loaded
            Loaded += async (_, _) => await viewModel.InitializeAsync();
        }
    }
}
