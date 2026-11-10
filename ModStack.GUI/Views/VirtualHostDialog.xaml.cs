using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using ModStack.GUI.Services;
using ModStack.Data.Interfaces;
using ModStack.Common.Models;
using ModStack.Services;
using ModStack.Common;
using Microsoft.Win32;

namespace ModStack.GUI.Views;

/// <summary>
/// Dialog for creating and editing virtual hosts
/// </summary>
public partial class VirtualHostDialog : Window, INotifyPropertyChanged
{
    private readonly VirtualHostManager _virtualHostManager;
    private readonly IServiceRepository _serviceRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly VirtualHost? _existingVirtualHost;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    public string DialogTitle => _existingVirtualHost == null ? "Create Virtual Host" : "Edit Virtual Host";
    public string DialogDescription => _existingVirtualHost == null 
        ? "Configure a new virtual host for your web server" 
        : "Modify the virtual host configuration";

    public string SaveButtonText => _existingVirtualHost == null ? "Create" : "Save";

    private string _serverName = string.Empty;
    public string ServerName
    {
        get => _serverName;
        set
        {
            _serverName = value;
            OnPropertyChanged(nameof(ServerName));
        }
    }

    private string? _serverAlias;
    public string? ServerAlias
    {
        get => _serverAlias;
        set
        {
            _serverAlias = value;
            OnPropertyChanged(nameof(ServerAlias));
        }
    }

    private string _documentRoot = string.Empty;
    public string DocumentRoot
    {
        get => _documentRoot;
        set
        {
            _documentRoot = value;
            OnPropertyChanged(nameof(DocumentRoot));
        }
    }

    private int _port = 80;
    public int Port
    {
        get => _port;
        set
        {
            _port = value;
            OnPropertyChanged(nameof(Port));
        }
    }

    private bool _isSSL;
    public bool IsSSL
    {
        get => _isSSL;
        set
        {
            _isSSL = value;
            OnPropertyChanged(nameof(IsSSL));
            if (_isSSL && Port == 80)
            {
                Port = 443;
            }
            else if (!_isSSL && Port == 443)
            {
                Port = 80;
            }
        }
    }

    private string? _sslCertPath;
    public string? SSLCertPath
    {
        get => _sslCertPath;
        set
        {
            _sslCertPath = value;
            OnPropertyChanged(nameof(SSLCertPath));
        }
    }

    private string? _sslKeyPath;
    public string? SSLKeyPath
    {
        get => _sslKeyPath;
        set
        {
            _sslKeyPath = value;
            OnPropertyChanged(nameof(SSLKeyPath));
        }
    }

    private bool _isVirtualHostEnabled = true;
    public bool IsVirtualHostEnabled
    {
        get => _isVirtualHostEnabled;
        set
        {
            _isVirtualHostEnabled = value;
            OnPropertyChanged(nameof(IsVirtualHostEnabled));
        }
    }

    private string? _additionalConfiguration;
    public string? AdditionalConfiguration
    {
        get => _additionalConfiguration;
        set
        {
            _additionalConfiguration = value;
            OnPropertyChanged(nameof(AdditionalConfiguration));
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
        }
    }

    private ObservableCollection<Project> _availableProjects = [];
    public ObservableCollection<Project> AvailableProjects
    {
        get => _availableProjects;
        set
        {
            _availableProjects = value;
            OnPropertyChanged(nameof(AvailableProjects));
        }
    }

    private Project? _selectedProject;
    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            _selectedProject = value;
            OnPropertyChanged(nameof(SelectedProject));
        }
    }

    #endregion

    private readonly ModStack.GUI.Services.Interfaces.IDialogService _dialogService;

    public VirtualHostDialog(
        VirtualHostManager virtualHostManager, 
        IServiceRepository serviceRepository, 
        IProjectRepository projectRepository,
        ModStack.GUI.Services.Interfaces.IDialogService dialogService,
        VirtualHost? existingVirtualHost = null)
    {
        InitializeComponent();
        DataContext = this;

        _existingVirtualHost = existingVirtualHost;

        _virtualHostManager = virtualHostManager;
        _serviceRepository = serviceRepository;
        _projectRepository = projectRepository;
        _dialogService = dialogService;

        Loaded += VirtualHostDialog_Loaded;
    }

    private async void VirtualHostDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
        LoadExistingData();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load available services (Apache/Nginx only)
            var services = await _serviceRepository.GetAllAsync();
            AvailableServices.Clear();
            
            foreach (var service in services.Where(s => s.ServiceType == ServiceType.Apache || s.ServiceType == ServiceType.Nginx))
            {
                AvailableServices.Add(service);
            }

            // Load available projects
            var projects = await _projectRepository.GetAllAsync();
            AvailableProjects.Clear();
            
            foreach (var project in projects)
            {
                AvailableProjects.Add(project);
            }

            // Set default service if only one available
            if (AvailableServices.Count == 1)
            {
                SelectedService = AvailableServices[0];
            }
        }
        catch (Exception ex)
        {
            _ = _dialogService.ShowErrorAsync($"Failed to load data: {ex.Message}", "Error");
        }
    }

    private async Task<bool> IsPortAvailableAsync(int port, long serviceId)
    {
        try
        {
            var virtualHosts = await _virtualHostManager.GetVirtualHostsForServiceAsync(serviceId);
            return !virtualHosts.Any(vh => vh.Port == port && vh.IsEnabled);
        }
        catch
        {
            return true; // If we can't check, assume it's available
        }
    }

    private void LoadExistingData()
    {
        if (_existingVirtualHost != null)
        {
            ServerName = _existingVirtualHost.ServerName;
            ServerAlias = _existingVirtualHost.ServerAlias;
            DocumentRoot = _existingVirtualHost.DocumentRoot;
            Port = _existingVirtualHost.Port;
            IsSSL = _existingVirtualHost.IsSSL;
            SSLCertPath = _existingVirtualHost.SSLCertPath;
            SSLKeyPath = _existingVirtualHost.SSLKeyPath;
            IsVirtualHostEnabled = _existingVirtualHost.IsEnabled;

            // Find and select the service
            if (_existingVirtualHost.Service != null)
            {
                SelectedService = AvailableServices.FirstOrDefault(s => s.Id == _existingVirtualHost.Service.Id);
            }

            // Find and select the project
            if (_existingVirtualHost.Project != null)
            {
                SelectedProject = AvailableProjects.FirstOrDefault(p => p.Id == _existingVirtualHost.Project.Id);
            }
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialogService = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.IDialogService>();
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(ServerName))
            {
                if (dialogService != null) await dialogService.ShowInformationAsync("Server name is required.", "Validation Error");
                return;
            }

            if (string.IsNullOrWhiteSpace(DocumentRoot))
            {
                if (dialogService != null) await dialogService.ShowInformationAsync("Document root is required.", "Validation Error");
                return;
            }

            if (SelectedService == null)
            {
                if (dialogService != null) await dialogService.ShowInformationAsync("Please select a web service.", "Validation Error");
                return;
            }

            // Check if port is available (only for new virtual hosts)
            if (_existingVirtualHost == null)
            {
                var isPortAvailable = await IsPortAvailableAsync(Port, SelectedService.Id);
                if (!isPortAvailable)
                {
                    if (dialogService != null) await dialogService.ShowInformationAsync($"Port {Port} is already in use by another virtual host. Please choose a different port.", "Port Conflict");
                    return;
                }
            }

            if (IsSSL && (string.IsNullOrWhiteSpace(SSLCertPath) || string.IsNullOrWhiteSpace(SSLKeyPath)))
            {
                if (dialogService != null) await dialogService.ShowInformationAsync("SSL certificate and key paths are required when SSL is enabled.", "Validation Error");
                return;
            }

            // Create or update virtual host
            if (_existingVirtualHost == null)
            {
                // Create new virtual host
                var request = new CreateVirtualHostRequest
                {
                    ProjectId = SelectedProject?.Id,
                    ServiceId = SelectedService.Id,
                    ServerName = ServerName,
                    ServerAlias = ServerAlias,
                    DocumentRoot = DocumentRoot,
                    Port = Port,
                    IsSSL = IsSSL,
                    SSLCertPath = SSLCertPath,
                    SSLKeyPath = SSLKeyPath,
                    IsEnabled = IsVirtualHostEnabled,
                    AdditionalConfiguration = AdditionalConfiguration
                };

                await _virtualHostManager.CreateVirtualHostAsync(request);
                
                // Show success toast notification
                ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(
                    $"Virtual host '{request.ServerName}' created successfully",
                    "Virtual Host Created");
            }
            else
            {
                // Update existing virtual host
                var request = new UpdateVirtualHostRequest
                {
                    ServerName = ServerName,
                    ServerAlias = ServerAlias,
                    DocumentRoot = DocumentRoot,
                    Port = Port,
                    IsSSL = IsSSL,
                    SSLCertPath = SSLCertPath,
                    SSLKeyPath = SSLKeyPath,
                    IsEnabled = IsVirtualHostEnabled,
                    AdditionalConfiguration = AdditionalConfiguration
                };

                await _virtualHostManager.UpdateVirtualHostAsync(_existingVirtualHost.Id, request);
                
                // Show success toast notification
                ModStack.GUI.Services.NotificationService.Instance.ShowSuccess(
                    $"Virtual host '{request.ServerName}' updated successfully",
                    "Virtual Host Updated");
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            if (dialogService != null) await dialogService.ShowErrorAsync($"Failed to save virtual host: {ex.Message}", "Error");
        }
    }

    private async void BrowseDocumentRoot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Document Root Folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            // Set initial directory if DocumentRoot is already set
            if (!string.IsNullOrEmpty(DocumentRoot) && Directory.Exists(DocumentRoot))
            {
                folderDialog.SelectedPath = DocumentRoot;
            }

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DocumentRoot = folderDialog.SelectedPath;
            }
        }
        catch (Exception ex)
        {
            var dialogService = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.IDialogService>();
            if (dialogService != null) await dialogService.ShowErrorAsync($"Error browsing for folder: {ex.Message}", "Error");
        }
    }

    private async void BrowseSSLCertPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select SSL Certificate File",
                Filter = "Certificate Files (*.crt;*.pem;*.cer)|*.crt;*.pem;*.cer|All Files (*.*)|*.*",
                DefaultExt = "crt"
            };

            // Set initial directory if SSLCertPath is already set
            if (!string.IsNullOrEmpty(SSLCertPath) && File.Exists(SSLCertPath))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(SSLCertPath);
                openFileDialog.FileName = Path.GetFileName(SSLCertPath);
            }

            if (openFileDialog.ShowDialog() == true)
            {
                SSLCertPath = openFileDialog.FileName;
            }
        }
        catch (Exception ex)
        {
            var dialogService = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.IDialogService>();
            if (dialogService != null) await dialogService.ShowErrorAsync($"Error browsing for certificate file: {ex.Message}", "Error");
        }
    }

    private async void BrowseSSLKeyPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select SSL Private Key File",
                Filter = "Key Files (*.key;*.pem)|*.key;*.pem|All Files (*.*)|*.*",
                DefaultExt = "key"
            };

            // Set initial directory if SSLKeyPath is already set
            if (!string.IsNullOrEmpty(SSLKeyPath) && File.Exists(SSLKeyPath))
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(SSLKeyPath);
                openFileDialog.FileName = Path.GetFileName(SSLKeyPath);
            }

            if (openFileDialog.ShowDialog() == true)
            {
                SSLKeyPath = openFileDialog.FileName;
            }
        }
        catch (Exception ex)
        {
            var dialogService = ServiceLocator.GetServiceOptional<ModStack.GUI.Services.Interfaces.IDialogService>();
            if (dialogService != null) await dialogService.ShowErrorAsync($"Error browsing for private key file: {ex.Message}", "Error");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
