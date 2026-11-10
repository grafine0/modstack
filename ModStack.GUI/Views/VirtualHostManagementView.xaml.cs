using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ModStack.GUI.Services;
using ModStack.Data.Interfaces;
using ModStack.Common.Models;
using ModStack.Core.Services;
using ModStack.Services;
using ModStack.Common;

namespace ModStack.GUI.Views;

/// <summary>
/// Virtual Host Management View - Professional interface for managing virtual hosts
/// </summary>
public partial class VirtualHostManagementView : UserControl, INotifyPropertyChanged, IDisposable
{
    private readonly VirtualHostManager _virtualHostManager;
    private readonly IServiceService _serviceService;
    private readonly IProjectRepository _projectRepository;
    private readonly ModStack.GUI.Services.Interfaces.IDialogService _dialogService;
    private readonly ModStack.GUI.Services.IViewFactoryService _viewFactoryService;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    private ObservableCollection<VirtualHostDisplayItem> _virtualHosts = [];
    public ObservableCollection<VirtualHostDisplayItem> VirtualHosts
    {
        get => _virtualHosts;
        set
        {
            _virtualHosts = value;
            OnPropertyChanged(nameof(VirtualHosts));
            OnPropertyChanged(nameof(FilteredVirtualHosts));
            OnPropertyChanged(nameof(TotalVirtualHosts));
            OnPropertyChanged(nameof(ActiveVirtualHosts));
            OnPropertyChanged(nameof(SSLEnabledHosts));
            OnPropertyChanged(nameof(HasVirtualHosts));
        }
    }

    private ObservableCollection<ServiceDefinition> _availableServices = [];
    public ObservableCollection<ServiceDefinition> AvailableServices
    {
        get => _availableServices;
        set
        {
            _availableServices = value;
            OnPropertyChanged(nameof(AvailableServices));
            OnPropertyChanged(nameof(TotalServices));
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(FilteredVirtualHosts));
            OnPropertyChanged(nameof(HasVirtualHosts));
        }
    }

    private ServiceDefinition? _selectedService;
    public ServiceDefinition? SelectedService
    {
        get => _selectedService;
        set
        {
            _selectedService = value;
            OnPropertyChanged(nameof(SelectedService));
            OnPropertyChanged(nameof(FilteredVirtualHosts));
            OnPropertyChanged(nameof(HasVirtualHosts));
        }
    }

    private string _selectedStatus = "All Status";
    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            _selectedStatus = value;
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(FilteredVirtualHosts));
            OnPropertyChanged(nameof(HasVirtualHosts));
        }
    }

    public ICollectionView FilteredVirtualHosts
    {
        get
        {
            var view = CollectionViewSource.GetDefaultView(VirtualHosts);
            view.Filter = FilterVirtualHost;
            return view;
        }
    }

    public int TotalVirtualHosts => VirtualHosts.Count;
    public int ActiveVirtualHosts => VirtualHosts.Count(vh => vh.IsEnabled);
    public int SSLEnabledHosts => VirtualHosts.Count(vh => vh.IsSSL);
    public int TotalServices => AvailableServices.Count;
    public bool HasVirtualHosts => VirtualHosts.Count > 0;

    #endregion

    public VirtualHostManagementView(
        VirtualHostManager virtualHostManager,
        IServiceService serviceService,
        IProjectRepository projectRepository,
        ModStack.GUI.Services.Interfaces.IDialogService dialogService,
        ModStack.GUI.Services.IViewFactoryService viewFactoryService)
    {
        try
        {
            LogService.Info("=== VirtualHostManagementView Constructor START ===");
            InitializeComponent();
            DataContext = this;

            _virtualHostManager = virtualHostManager;
            _serviceService = serviceService;
            _projectRepository = projectRepository;
            _dialogService = dialogService;
            _viewFactoryService = viewFactoryService;

            Loaded += VirtualHostManagementView_Loaded;
            LogService.Info("=== VirtualHostManagementView Constructor END ===");
        }
        catch (Exception ex)
        {
            LogService.Error($"=== VirtualHostManagementView Constructor ERROR: {ex.Message} ===", ex);
            throw;
        }
    }
    
    private async void VirtualHostManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            LogService.Info("=== VirtualHostManagementView.LoadDataAsync START ===");
            
            if (_virtualHostManager == null)
            {
                LogService.Warning("VirtualHostManager is not initialized yet");
                return;
            }
            
            LogService.Info("About to call _virtualHostManager.GetAllVirtualHostsAsync()");
            
            // Load virtual hosts
            var virtualHosts = await _virtualHostManager.GetAllVirtualHostsAsync();
            LogService.Info($"Retrieved {virtualHosts.Count} virtual hosts from VirtualHostManager");
            
            VirtualHosts.Clear();
            
            foreach (var vh in virtualHosts)
            {
                LogService.Info($"Adding virtual host to UI: {vh.ServerName}:{vh.Port} (ID: {vh.Id})");
                VirtualHosts.Add(new VirtualHostDisplayItem(vh));
            }
            
            LogService.Info($"VirtualHosts collection now has {VirtualHosts.Count} items");
            LogService.Info($"HasVirtualHosts property: {HasVirtualHosts}");

            // Load available services (Apache/Nginx only)
            if (_serviceService == null)
            {
                LogService.Warning("ServiceService is not initialized yet");
                return;
            }
            
            var apacheServices = await _serviceService.GetServicesByTypeAsync(ServiceType.Apache);
            var nginxServices = await _serviceService.GetServicesByTypeAsync(ServiceType.Nginx);
            var webServices = apacheServices.Concat(nginxServices).ToList();
            LogService.Info($"Retrieved {webServices.Count} web services (Apache/Nginx) from ServiceService");
            
            AvailableServices.Clear();
            
            foreach (var service in webServices)
            {
                LogService.Info($"Adding web service: {service.DisplayName} ({service.ServiceType})");
                AvailableServices.Add(service);
            }
            
            LogService.Info($"AvailableServices collection now has {AvailableServices.Count} items");
            LogService.Info($"TotalServices property: {TotalServices}");

            // Populate the service filter combo box with "All Services" option
            var serviceFilterItems = new List<ServiceFilterItem> 
            { 
                new() { DisplayName = "All Services", ServiceDefinition = null }
            };
            serviceFilterItems.AddRange(AvailableServices.Select(s => new ServiceFilterItem 
            { 
                DisplayName = s.DisplayName, 
                ServiceDefinition = s 
            }));
            ServiceFilterComboBox.ItemsSource = serviceFilterItems;
            ServiceFilterComboBox.DisplayMemberPath = "DisplayName";
            ServiceFilterComboBox.SelectedIndex = 0; // "All Services"

            // Refresh the filtered view and notify UI of changes
            OnPropertyChanged(nameof(FilteredVirtualHosts));
            OnPropertyChanged(nameof(HasVirtualHosts));
            OnPropertyChanged(nameof(TotalVirtualHosts));
            OnPropertyChanged(nameof(ActiveVirtualHosts));
            OnPropertyChanged(nameof(SSLEnabledHosts));
            OnPropertyChanged(nameof(TotalServices));
            
            LogService.Info("UI property change notifications sent");
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to load virtual hosts: {ex.Message}", ex);
            _ = _dialogService?.ShowErrorAsync($"Failed to load virtual hosts: {ex.Message}", "Error");
        }
    }

    private bool FilterVirtualHost(object obj)
    {
        if (obj is not VirtualHostDisplayItem vh) return false;

        // Search filter
        if (!string.IsNullOrEmpty(SearchText))
        {
            if (!vh.ServerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                !vh.ServerAlias?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true &&
                !vh.DocumentRoot.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Service filter
        if (SelectedService != null && vh.Service?.Id != SelectedService.Id)
        {
            return false;
        }

        // Status filter
        if (SelectedStatus != "All Status")
        {
            var isActive = vh.IsEnabled;
            if (SelectedStatus == "Active" && !isActive)
            {
                return false;
            }
            if (SelectedStatus == "Inactive" && isActive)
            {
                return false;
            }
        }

        return true;
    }

    private async void CreateVirtualHostButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = _viewFactoryService.CreateDialog<VirtualHostDialog>();
            var result = Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog);
            
            // Always refresh the data after dialog closes, regardless of result
            // This ensures we show any virtual hosts that were created successfully
            await LoadDataAsync();
            
            // Show error message if dialog was closed due to an error
            if (result != true)
            {
                // The error message was already shown in the dialog
                Console.WriteLine("Virtual host creation dialog was closed without success");
            }
        }
        catch (Exception ex)
        {
            _ = _dialogService?.ShowErrorAsync($"Failed to create virtual host: {ex.Message}", "Error");
            
            // Still refresh to show any existing virtual hosts
            try
            {
                await LoadDataAsync();
            }
            catch (Exception refreshEx)
            {
                Console.WriteLine($"Failed to refresh data: {refreshEx.Message}");
            }
        }
    }

    private async void EditVirtualHostButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is VirtualHostDisplayItem vh)
            {
                var dialog = _viewFactoryService.CreateDialog<VirtualHostDialog>(vh.VirtualHost);
                if (Helpers.DialogBlurHelper.ShowDialogWithBlur(dialog) == true)
                {
                    await LoadDataAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _ = _dialogService?.ShowErrorAsync($"Failed to edit virtual host: {ex.Message}", "Error");
        }
    }

    private async void DeleteVirtualHostButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is VirtualHostDisplayItem vh)
            {
                if (_dialogService != null)
                {
                    var result = await _dialogService.ShowConfirmationAsync(
                        $"Are you sure you want to delete the virtual host '{vh.ServerName}'?\n\nThis will remove the virtual host configuration and cannot be undone.",
                        "Confirm Delete");

                    if (result)
                    {
                    if (_virtualHostManager != null)
                    {
                        await _virtualHostManager.DeleteVirtualHostAsync(vh.VirtualHost.Id);
                        await LoadDataAsync();
                    }
                    else
                    {
                        _ = _dialogService?.ShowErrorAsync("VirtualHostManager is not initialized", "Error");
                    }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _ = _dialogService?.ShowErrorAsync($"Failed to delete virtual host: {ex.Message}", "Error");
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        OnPropertyChanged(nameof(FilteredVirtualHosts));
        OnPropertyChanged(nameof(HasVirtualHosts));
        UpdateEmptyStateVisibility();
    }

    private void ServiceFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ServiceFilterItem selectedItem)
        {
            SelectedService = selectedItem.ServiceDefinition; // null for "All Services"
        }
        OnPropertyChanged(nameof(FilteredVirtualHosts));
        OnPropertyChanged(nameof(HasVirtualHosts));
        UpdateEmptyStateVisibility();
    }

    private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            SelectedStatus = selectedItem.Content.ToString() ?? "All Status";
        }
        OnPropertyChanged(nameof(FilteredVirtualHosts));
        OnPropertyChanged(nameof(HasVirtualHosts));
        UpdateEmptyStateVisibility();
    }

    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset all filter controls
        SearchText = string.Empty;
        ServiceFilterComboBox.SelectedIndex = 0; // "All Services"
        StatusFilterComboBox.SelectedIndex = 0; // "All Status"
        
        // Clear the properties
        SelectedService = null;
        SelectedStatus = "All Status";
        
        // Refresh the filtered view
        OnPropertyChanged(nameof(FilteredVirtualHosts));
        OnPropertyChanged(nameof(HasVirtualHosts));
        UpdateEmptyStateVisibility();
    }

    private void UpdateEmptyStateVisibility()
    {
        try
        {
            // Check if controls are initialized
            if (EmptyStatePanel == null || VirtualHostsItemsControl == null)
            {
                System.Diagnostics.Debug.WriteLine("Empty state panel or virtual hosts items control not yet initialized");
                return;
            }

            // Show empty state when there are no filtered virtual hosts
            var hasVirtualHosts = FilteredVirtualHosts?.Cast<object>().Any() == true;
            EmptyStatePanel.Visibility = hasVirtualHosts ? Visibility.Collapsed : Visibility.Visible;
            VirtualHostsItemsControl.Visibility = hasVirtualHosts ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating empty state visibility: {ex.Message}");
        }
    }

    private void CreateFirstVirtualHostButton_Click(object sender, RoutedEventArgs e)
    {
        CreateVirtualHostButton_Click(sender, e);
    }

    private void VirtualHostsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle selection changes if needed
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region IDisposable Implementation

    private bool _disposed = false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Clean up managed resources
                Loaded -= VirtualHostManagementView_Loaded;
                
                // Clear collections to help with garbage collection
                VirtualHosts?.Clear();
                AvailableServices?.Clear();
                
                // Clear UI bindings
                DataContext = null;
                
                LogService.Info("VirtualHostManagementView disposed");
            }
            
            _disposed = true;
        }
    }

    #endregion
}

/// <summary>
/// Display item for virtual hosts with additional UI properties
/// </summary>
public class VirtualHostDisplayItem(VirtualHost virtualHost)
{
    public VirtualHost VirtualHost { get; } = virtualHost;
    public string ServerName => VirtualHost.ServerName;
    public string? ServerAlias => VirtualHost.ServerAlias;
    public string DocumentRoot => VirtualHost.DocumentRoot;
    public int Port => VirtualHost.Port;
    public bool IsSSL => VirtualHost.IsSSL;
    public bool IsEnabled => VirtualHost.IsEnabled;
    public DateTime CreatedAt => VirtualHost.CreatedAt;
    public ServiceDefinition? Service => VirtualHost.Service;
}

public class ServiceFilterItem
{
    public string DisplayName { get; set; } = string.Empty;
    public ServiceDefinition? ServiceDefinition { get; set; }
}
