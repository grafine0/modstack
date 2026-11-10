using System;
using System.Windows;
using ModStack.Common.Models;
using ModStack.Core.Interfaces;
using ModStack.Data.Interfaces;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;
using ModStack.Services;
using Microsoft.Extensions.Logging;

namespace ModStack.GUI.Views;

/// <summary>
/// UserControl for updating service configuration, PHP extensions, and Apache modules
/// </summary>
public partial class UpdateServiceView
{
    public UpdateServiceViewModel ViewModel { get; private set; }

    public UpdateServiceView(
        IServiceInstallationChecker serviceInstallationChecker,
        IServiceRepository serviceRepository,
        IApacheModuleRepository apacheModuleRepository,
        ApacheModuleManager apacheModuleManager,
        ApacheConfigurationService apacheConfigService,
        IPHPExtensionRepository phpExtensionRepository,
        PHPExtensionManager phpExtensionManager,
        ModStack.Core.Services.IServiceService serviceService,
        ModStack.Core.Services.ServiceControlManager serviceControlManager,
        IVirtualHostService virtualHostService,
        ILogger<UpdateServiceViewModel> logger,
        ServiceDisplayItem service)
    {
        InitializeComponent();

        // Get Apache-specific services if this is an Apache service
        IApacheModuleRepository? resolvedApacheModuleRepository = null;
        ApacheModuleManager? resolvedApacheModuleManager = null;
        ApacheConfigurationService? resolvedApacheConfigService = null;

        // Get PHP-specific services if this is a PHP service
        IPHPExtensionRepository? resolvedPhpExtensionRepository = null;
        PHPExtensionManager? resolvedPhpExtensionManager = null;
        ModStack.Core.Services.IServiceService? resolvedServiceService = null;

        if (service.Type == ModStack.Common.ServiceType.Apache)
        {
            resolvedApacheModuleRepository = apacheModuleRepository;
            resolvedApacheModuleManager = apacheModuleManager;
            resolvedApacheConfigService = apacheConfigService;
        }
        else if (service.Type == ModStack.Common.ServiceType.PHP)
        {
            resolvedPhpExtensionRepository = phpExtensionRepository;
            resolvedPhpExtensionManager = phpExtensionManager;
            resolvedServiceService = serviceService;
        }

        // Create ViewModel
        ViewModel = new UpdateServiceViewModel(
            service,
            serviceInstallationChecker,
            serviceRepository,
            resolvedApacheModuleRepository,
            resolvedApacheModuleManager,
            resolvedApacheConfigService,
            resolvedPhpExtensionRepository,
            resolvedPhpExtensionManager,
            resolvedServiceService,
            serviceControlManager,
            virtualHostService,
            logger);

        DataContext = ViewModel;
        
        // Handle tab selection changed event
        this.Loaded += (s, e) =>
        {
            if (this.FindName("TabControl") is System.Windows.Controls.TabControl tabControl)
            {
                tabControl.SelectionChanged += TabControl_SelectionChanged;
            }
        };
    }
    
    private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Force trigger tab change handler in ViewModel
        if (ViewModel != null && sender is System.Windows.Controls.TabControl tabControl)
        {
            var newIndex = tabControl.SelectedIndex;
            System.Diagnostics.Debug.WriteLine($"🔍 TabControl_SelectionChanged: SelectedIndex changed to {newIndex}");
            
            // Update ViewModel's SelectedTabIndex which will trigger OnTabChanged
            ViewModel.SelectedTabIndex = newIndex;
        }
    }
}

