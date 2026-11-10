using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using ModStack.Data.Interfaces;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;
using ModStack.Services;

namespace ModStack.GUI.Views.Dialogs
{
    /// <summary>
    /// Apache Module Manager Dialog for enabling/disabling Apache modules
    /// Now follows proper MVVM pattern with ViewModel
    /// </summary>
    public partial class ApacheModuleManagerDialog : Window
    {
        private readonly ApacheModuleManagerViewModel _viewModel;

        public ApacheModuleManagerDialog()
        {
            InitializeComponent();

            // Get services from service locator and create ViewModel
            var moduleRepository = ServiceLocator.GetService<IApacheModuleRepository>();
            var moduleManager = ServiceLocator.GetService<ApacheModuleManager>();
            var apacheConfigService = ServiceLocator.GetService<ApacheConfigurationService>();
            var logger = ServiceLocator.GetService<ILogger<ApacheModuleManagerViewModel>>();
            
            _viewModel = new ApacheModuleManagerViewModel(moduleRepository, moduleManager, apacheConfigService, logger);
            DataContext = _viewModel;

            // Subscribe to ViewModel events
            _viewModel.RequestClose += (s, e) => 
            {
                DialogResult = true;
                Close();
            };

            // Initialize ViewModel when loaded
            Loaded += async (s, e) => 
            {
                System.Diagnostics.Debug.WriteLine("🔄 ApacheModuleManagerDialog Loaded event fired");
                await _viewModel.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("✅ ApacheModuleManagerDialog initialization completed");
            };
        }

        // All logic has been moved to ApacheModuleManagerViewModel
        // This code-behind now only handles view-specific concerns
    }
}
