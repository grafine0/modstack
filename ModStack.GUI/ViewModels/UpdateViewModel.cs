using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ModStack.Core.Interfaces;

namespace ModStack.GUI.ViewModels;

public class UpdateViewModel : INotifyPropertyChanged
{
    private readonly IUpdateService _updateService;
    private string _statusText = "Ready to check for updates.";
    private bool _isChecking;
    private bool _isDownloading;
    private double _downloadProgress;
    private bool _updateAvailable;
    private string _downloadUrl = string.Empty;
    private CancellationTokenSource? _cancellationTokenSource;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsChecking
    {
        get => _isChecking;
        set
        {
            if (_isChecking != value)
            {
                _isChecking = value;
                OnPropertyChanged();
                (CheckForUpdatesCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (_isDownloading != value)
            {
                _isDownloading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowCheckButton));
                OnPropertyChanged(nameof(ShowDownloadButton));
                (DownloadAndInstallCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (Math.Abs(_downloadProgress - value) > 0.01)
            {
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set
        {
            if (_updateAvailable != value)
            {
                _updateAvailable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowCheckButton));
                OnPropertyChanged(nameof(ShowDownloadButton));
            }
        }
    }

    public bool ShowCheckButton => !UpdateAvailable && !IsDownloading;
    public bool ShowDownloadButton => UpdateAvailable && !IsDownloading;

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }
    public IAsyncRelayCommand DownloadAndInstallCommand { get; }

    public UpdateViewModel(IUpdateService updateService)
    {
        _updateService = updateService;

        CheckForUpdatesCommand = new AsyncRelayCommand(
            CheckForUpdatesAsync,
            () => !IsChecking && !IsDownloading
        );

        DownloadAndInstallCommand = new AsyncRelayCommand(
            DownloadAndInstallAsync,
            () => UpdateAvailable && !IsDownloading
        );
    }

    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        StatusText = "Checking for updates...";
        UpdateAvailable = false;

        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo.IsUpdateAvailable)
            {
                StatusText = $"Update available: {updateInfo.LatestVersion}!";
                _downloadUrl = updateInfo.DownloadUrl;
                UpdateAvailable = true;
            }
            else
            {
                StatusText = "You are up to date.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error checking updates: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
            DownloadAndInstallCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task DownloadAndInstallAsync()
    {
        IsDownloading = true;
        StatusText = "Downloading update...";
        DownloadProgress = 0;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);

            // MOCK INSTALLER PATH
            string tempInstallerPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ModStack-Installer.exe");

            await _updateService.DownloadUpdateAsync(_downloadUrl, progress, _cancellationTokenSource.Token);

            StatusText = "Download complete. Starting installer...";
            _updateService.InstallUpdate(tempInstallerPath);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Download cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error downloading update: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
