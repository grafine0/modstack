using System.Windows.Controls;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ModStack.GUI.Views;

public partial class UpdateView : UserControl
{
    public UpdateView()
    {
        InitializeComponent();
        
        if (!System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            var viewModel = ServiceLocator.ServiceProvider?.GetService<UpdateViewModel>();
            if (viewModel != null)
            {
                this.DataContext = viewModel;
            }
        }
    }
}
