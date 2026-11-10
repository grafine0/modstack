using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace ModStack.GUI.Views;

public partial class ServiceInstallProgressWindow : Window, INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _statusText = "Preparing installation...";
    private double _progress = 0;
    private bool _isCancelled = false;
    private string _downloadSpeed = string.Empty;
    private string _eta = string.Empty;
    private string _operationIcon = "📥"; // Default to install icon
    private string _downloadInfo = string.Empty;
    private DateTime _startTime = DateTime.Now;
    private long _totalBytes = 0;
    private long _downloadedBytes = 0;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly Queue<double> _speedHistory = new();
    private readonly int _speedHistorySize = 10; // Keep last 10 speed measurements for smoothing
    private Border? _modalOverlay; // Reference to the modal overlay

    public event EventHandler? InstallCancelled;

    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set
        {
            if (_downloadSpeed != value)
            {
                _downloadSpeed = value;
                OnPropertyChanged(nameof(DownloadSpeed));
            }
        }
    }

    public string ETA
    {
        get => _eta;
        set
        {
            if (_eta != value)
            {
                _eta = value;
                OnPropertyChanged(nameof(ETA));
            }
        }
    }

    public string OperationIcon
    {
        get => _operationIcon;
        set
        {
            if (_operationIcon != value)
            {
                _operationIcon = value;
                OnPropertyChanged(nameof(OperationIcon));
            }
        }
    }

    public string DownloadInfo
    {
        get => _downloadInfo;
        set
        {
            if (_downloadInfo != value)
            {
                _downloadInfo = value;
                OnPropertyChanged(nameof(DownloadInfo));
            }
        }
    }

    public new string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            RunOnUIThread(() =>
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            });
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                RunOnUIThread(() =>
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                    
                    // Trigger TargetUpdated for MultiBinding animation
                    var bindingExpression = ProgressIndicator?.GetBindingExpression(FrameworkElement.WidthProperty);
                    bindingExpression?.UpdateTarget();
                });
            }
        }
    }

    public bool IsCancelled => _isCancelled;
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public ServiceInstallProgressWindow(string title, bool isUninstall = false)
    {
        InitializeComponent();
        
        // Set DataContext first
        DataContext = this;
        
        // Set title (this will trigger property changed notification)
        _title = title;
        base.Title = title; // Also set the Window title
        OnPropertyChanged(nameof(Title));
        
        _startTime = DateTime.Now;
        
        // Set appropriate icon and initial status based on operation type
        if (isUninstall)
        {
            _operationIcon = "🗑️";
            _statusText = "Preparing uninstallation...";
        }
        else
        {
            _operationIcon = "📥";
            _statusText = "Preparing installation...";
        }
        
        // Initialize with empty download statistics
        DownloadSpeed = "";
        ETA = "";
        
        // Create modal overlay effect on parent window
        CreateModalOverlay();
    }

    /// <summary>
    /// Helper method to run actions on the UI thread, reducing dispatcher boilerplate
    /// </summary>
    private void RunOnUIThread(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.Invoke(action);
    }

    /// <summary>
    /// Conditional debug logging helper
    /// </summary>
    private void LogDebug(string message)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(message);
#endif
    }

    /// <summary>
    /// Updates progress with optional download statistics
    /// </summary>
    /// <param name="status">Status message to display</param>
    /// <param name="progress">Progress percentage (0-100)</param>
    /// <param name="downloadedBytes">Optional: bytes downloaded so far</param>
    /// <param name="totalBytes">Optional: total bytes to download</param>
    public void UpdateProgress(string status, double progress, long downloadedBytes = 0, long totalBytes = 0)
    {
        RunOnUIThread(() =>
        {
            // Filter out download info from status text since it's now in the title bar
            string displayStatus = status;
            if (status.StartsWith("Downloaded:"))
            {
                // Show more informative status during download
                if (progress < 50)
                    displayStatus = "Downloading package files...";
                else if (progress < 80)
                    displayStatus = "Download nearly complete...";
                else
                    displayStatus = "Finalizing download...";
            }
            else if (status.StartsWith("Connecting to:"))
            {
                displayStatus = "Connecting to download server...";
            }
            else if (status.StartsWith("Downloading"))
            {
                displayStatus = "Downloading package files...";
            }
            
            StatusText = displayStatus;
            Progress = progress;
            
            // Update download statistics if provided
            if (downloadedBytes > 0 && totalBytes > 0)
            {
                _downloadedBytes = downloadedBytes;
                _totalBytes = totalBytes;
            }
            
            UpdateDownloadStatistics(progress);
        });
    }

    /// <summary>
    /// Legacy method for backward compatibility - delegates to unified UpdateProgress
    /// </summary>
    [Obsolete("Use UpdateProgress with optional parameters instead")]
    public void UpdateDownloadProgress(string status, double progress, long downloadedBytes, long totalBytes)
    {
        UpdateProgress(status, progress, downloadedBytes, totalBytes);
    }

    private void UpdateDownloadStatistics(double progress)
    {
        try
        {
            var elapsed = DateTime.Now - _startTime;
            
            if (_totalBytes > 0 && _downloadedBytes > 0 && elapsed.TotalSeconds > 1)
            {
                // Calculate instantaneous speed
                var instantSpeed = _downloadedBytes / elapsed.TotalSeconds;
                
                // Add to speed history for smoothing
                _speedHistory.Enqueue(instantSpeed);
                if (_speedHistory.Count > _speedHistorySize)
                {
                    _speedHistory.Dequeue();
                }
                
                // Calculate smoothed speed (rolling average)
                var smoothedSpeed = _speedHistory.Average();
                var speedText = FormatSpeed(smoothedSpeed);
                
                // Calculate ETA with divide-by-zero protection
                var remainingBytes = _totalBytes - _downloadedBytes;
                string etaText;
                
                if (smoothedSpeed > 0 && remainingBytes > 0)
                {
                    var etaSeconds = remainingBytes / smoothedSpeed;
                    etaText = FormatETA(etaSeconds);
                }
                else
                {
                    etaText = "Calculating...";
                }
                
                // Update bindable properties (automatically updates UI via bindings)
                DownloadSpeed = speedText;
                ETA = etaText;
                
                // Update download info for title bar
                DownloadInfo = FormatDownloadInfo(_downloadedBytes, _totalBytes);
            }
            else if (progress > 0)
            {
                // Show generic progress info using bindable properties
                DownloadSpeed = "Processing...";
                ETA = "";
                DownloadInfo = "";
            }
        }
        catch (Exception ex)
        {
            LogDebug($"Error updating download statistics: {ex.Message}");
        }
    }

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return $"{bytesPerSecond:F0} B/s";
        if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024:F1} KB/s";
        return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
    }

    private string FormatETA(double seconds)
    {
        if (seconds <= 0 || double.IsInfinity(seconds) || double.IsNaN(seconds))
            return "Calculating...";
            
        var timeSpan = TimeSpan.FromSeconds(seconds);
        
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m remaining";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s remaining";
        
        return $"{timeSpan.Seconds}s remaining";
    }

    private string FormatDownloadInfo(long downloadedBytes, long totalBytes)
    {
        if (totalBytes <= 0 || downloadedBytes <= 0)
            return "";

        var downloadedSize = FormatFileSize(downloadedBytes);
        var totalSize = FormatFileSize(totalBytes);
        
        return $"Downloaded: {downloadedSize} / {totalSize}";
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelInstallation();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CancelInstallation();
    }

    /// <summary>
    /// Cancels the installation and notifies listeners
    /// </summary>
    private void CancelInstallation()
    {
        if (!_isCancelled)
        {
            _isCancelled = true;
            _cancellationTokenSource.Cancel();
            
            // Notify listeners
            InstallCancelled?.Invoke(this, EventArgs.Empty);
            
            LogDebug("Installation cancelled by user");
        }
        
        Close();
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a modal overlay effect on the parent window
    /// </summary>
    private void CreateModalOverlay()
    {
        if (Owner != null)
        {
            // Disable the parent window to prevent interaction
            Owner.IsEnabled = false;
            
            // Create a semi-transparent overlay
            var overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), // Semi-transparent black
                Opacity = 0.5
            };
            
            // Add overlay to parent window's content
            if (Owner.Content is Panel parentPanel)
            {
                parentPanel.Children.Add(overlay);
                _modalOverlay = overlay;
            }
        }
    }
    
    /// <summary>
    /// Removes the modal overlay from the parent window
    /// </summary>
    private void RemoveModalOverlay()
    {
        if (_modalOverlay != null && Owner?.Content is Panel parentPanel)
        {
            parentPanel.Children.Remove(_modalOverlay);
            _modalOverlay = null;
        }
        
        // Re-enable the parent window
        if (Owner != null)
        {
            Owner.IsEnabled = true;
        }
    }

    /// <summary>
    /// Handle window activation to ensure parent windows are also brought to front
    /// </summary>
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        
        // Bring parent windows to front when this window is activated
        BringParentWindowsToFront();
    }
    
    /// <summary>
    /// Brings all parent windows to the front
    /// </summary>
    private void BringParentWindowsToFront()
    {
        if (Owner != null)
        {
            // Bring the immediate parent to front
            Owner.Activate();
            Owner.BringIntoView();
            
            // If the parent has an owner, bring that to front too (recursive)
            var currentParent = Owner;
            while (currentParent != null)
            {
                currentParent.Activate();
                currentParent.BringIntoView();
                currentParent = currentParent.Owner;
            }
        }
    }

    /// <summary>
    /// Clean up resources when the window is closed
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        RemoveModalOverlay();
        _cancellationTokenSource?.Dispose();
        base.OnClosed(e);
    }
}
