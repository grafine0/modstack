using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ModStack.Common.Models;
using ModStack.GUI.Services;
using ModStack.Services;

namespace ModStack.GUI.Views;

/// <summary>
/// Progress window for PHP extension installation operations
/// </summary>
public partial class ExtensionInstallProgressWindow : Window, INotifyPropertyChanged
{
    private readonly PHPExtensionManager _extensionManager;
    private readonly ServiceDefinition _phpService;
    private readonly PHPExtension _extension;
    private readonly string _phpVersion;
    private CancellationTokenSource? _cancellationTokenSource;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Properties

    private string _title = "Installing PHP Extension";
    public new string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string ExtensionName => _extension.Name;
    public string ExtensionDisplayName => _extension.DisplayName;
    public string? ExtensionDescription => _extension.Description;

    private double _progressPercentage = 0;
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            _progressPercentage = value;
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(ProgressText));
        }
    }

    public string ProgressText => $"{ProgressPercentage:F0}%";

    private string _statusText = "Preparing installation...";
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private string _downloadInfo = "";
    public string DownloadInfo
    {
        get => _downloadInfo;
        set
        {
            _downloadInfo = value;
            OnPropertyChanged(nameof(DownloadInfo));
        }
    }

    private bool _canCancel = true;
    public bool CanCancel
    {
        get => _canCancel;
        set
        {
            _canCancel = value;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    private bool _canClose = false;
    public bool CanClose
    {
        get => _canClose;
        set
        {
            _canClose = value;
            OnPropertyChanged(nameof(CanClose));
        }
    }

    private bool _installationCompleted = false;
    public bool InstallationCompleted => _installationCompleted;

    private bool _installationSuccessful = false;
    public bool InstallationSuccessful => _installationSuccessful;

    #endregion

    public ExtensionInstallProgressWindow(ServiceDefinition phpService, PHPExtension extension, string phpVersion)
    {
        InitializeComponent();
        DataContext = this;

        _phpService = phpService;
        _extension = extension;
        _phpVersion = phpVersion;
        _extensionManager = ServiceLocator.GetService<PHPExtensionManager>();

        Title = $"Installing {extension.DisplayName}";
        
        Loaded += ExtensionInstallProgressWindow_Loaded;
        Closing += ExtensionInstallProgressWindow_Closing;
    }

    private async void ExtensionInstallProgressWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await StartInstallation();
    }

    private void ExtensionInstallProgressWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_installationCompleted && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    private async Task StartInstallation()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        
        try
        {
            await InstallExtensionWithProgress(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Installation cancelled by user";
            _installationCompleted = true;
            CanCancel = false;
            CanClose = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Installation failed: {ex.Message}";
            _installationCompleted = true;
            _installationSuccessful = false;
            CanCancel = false;
            CanClose = true;
        }
    }

    private async Task InstallExtensionWithProgress(CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validation (10%)
            StatusText = "Validating extension and service...";
            ProgressPercentage = 10;
            await Task.Delay(500, cancellationToken);

            // Step 2: Check if built-in or needs download (20%)
            StatusText = "Checking extension requirements...";
            ProgressPercentage = 20;
            await Task.Delay(300, cancellationToken);

            // Step 3: Download files if needed (30-70%)
            if (!_extension.IsBuiltIn && !string.IsNullOrEmpty(_extension.DownloadUrl))
            {
                StatusText = "Downloading extension files...";
                DownloadInfo = $"Downloading {_extension.DllName ?? _extension.Name}";
                
                // Simulate download progress
                for (int i = 30; i <= 70; i += 5)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProgressPercentage = i;
                    await Task.Delay(200, cancellationToken);
                }
                
                DownloadInfo = "";
            }
            else
            {
                StatusText = "Extension files already available...";
                ProgressPercentage = 70;
                await Task.Delay(300, cancellationToken);
            }

            // Step 4: Update PHP configuration (80%)
            StatusText = "Updating PHP configuration...";
            ProgressPercentage = 80;
            await Task.Delay(500, cancellationToken);

            // Step 5: Install extension (90%)
            StatusText = "Installing extension...";
            ProgressPercentage = 90;
            
            var request = new InstallPHPExtensionRequest
            {
                ServiceId = _phpService.Id,
                ExtensionId = _extension.Id,
                PHPVersion = _phpVersion,
                EnableAfterInstall = true
            };

            var result = await _extensionManager.InstallExtensionAsync(request, cancellationToken);
            
            // Step 6: Complete (100%)
            if (result.Success)
            {
                StatusText = result.Message;
                ProgressPercentage = 100;
                _installationSuccessful = true;
            }
            else
            {
                StatusText = $"Installation failed: {result.Message}";
                _installationSuccessful = false;
            }

            _installationCompleted = true;
            CanCancel = false;
            CanClose = true;

            // Auto-close after success
            if (_installationSuccessful)
            {
                await Task.Delay(1500, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    Dispatcher.Invoke(() =>
                    {
                        DialogResult = true;
                        Close();
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw to be handled by caller
        }
        catch (Exception ex)
        {
            StatusText = $"Installation error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Extension installation error: {ex}");
            _installationCompleted = true;
            _installationSuccessful = false;
            CanCancel = false;
            CanClose = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            StatusText = "Cancelling installation...";
            CanCancel = false;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installationCompleted)
        {
            DialogResult = _installationSuccessful;
            Close();
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
