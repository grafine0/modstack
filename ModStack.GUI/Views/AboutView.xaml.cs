using System.Windows.Controls;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ModStack.GUI.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        
        // Use dependency injection to resolve the ViewModel
        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var viewModel = ServiceLocator.ServiceProvider?.GetService<AboutViewModel>();
            if (viewModel != null)
            {
                this.DataContext = viewModel;
            }
        }
    }
}
