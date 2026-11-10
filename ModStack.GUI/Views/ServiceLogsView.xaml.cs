using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ModStack.GUI.Services;
using ModStack.GUI.Views.Dialogs;

namespace ModStack.GUI.Views
{

public partial class ServiceLogsView : UserControl, IDisposable
{
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly object _logLock = new object();
    private string _searchText = string.Empty;
    private LogLevel? _filterLevel = null;
    private System.Windows.Threading.DispatcherTimer? _refreshTimer;
    private System.Windows.Threading.DispatcherTimer? _updateDisplayTimer;
    private bool _disposed = false;
    private bool _pendingDisplayUpdate = false;
    
    // Cache styles to avoid creating new ones every refresh
    private readonly Dictionary<LogLevel, Style> _cachedStyles = new();

    public ServiceLogsView()
    {
        InitializeComponent();
        LogService.Info("ServiceLogsView initialized");
        
        // Register this view with LogService
        LogService.RegisterServiceLogsView(this);
        
        // Initialize cached styles
        InitializeCachedStyles();
        
        // Set up throttled display update timer (updates UI max once per second)
        _updateDisplayTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update every 500ms max
        };
        _updateDisplayTimer.Tick += UpdateDisplayTimer_Tick;
        _updateDisplayTimer.Start();
        
        LoadLogs();
        
        // Set up auto-refresh timer (only when view is loaded)
        this.Loaded += ServiceLogsView_Loaded;
        this.Unloaded += ServiceLogsView_Unloaded;
    }

    private void UpdateDisplayTimer_Tick(object? sender, EventArgs e)
    {
        if (_pendingDisplayUpdate && !_disposed)
        {
            _pendingDisplayUpdate = false;
            UpdateLogDisplayInternal();
        }
    }

    private void ServiceLogsView_Loaded(object sender, RoutedEventArgs e)
    {
        // Start timer only when view is visible
        if (_refreshTimer == null)
        {
            _refreshTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }
    }

    private void ServiceLogsView_Unloaded(object sender, RoutedEventArgs e)
    {
        // Stop timer when view is unloaded
        StopTimer();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            RefreshLogs();
        }
    }

    private void StopTimer()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
        
        if (_updateDisplayTimer != null)
        {
            _updateDisplayTimer.Tick -= UpdateDisplayTimer_Tick;
            _updateDisplayTimer.Stop();
            _updateDisplayTimer = null;
        }
    }

    private void InitializeCachedStyles()
    {
        _cachedStyles[LogLevel.Error] = CreateLogEntryStyle(LogLevel.Error);
        _cachedStyles[LogLevel.Warning] = CreateLogEntryStyle(LogLevel.Warning);
        _cachedStyles[LogLevel.Info] = CreateLogEntryStyle(LogLevel.Info);
    }

    private Style CreateLogEntryStyle(LogLevel level)
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily("Consolas")));
        style.Setters.Add(new Setter(TextBox.FontSizeProperty, 12.0));
        style.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(TextBox.MarginProperty, new Thickness(0, 2, 0, 2)));
        style.Setters.Add(new Setter(TextBox.IsReadOnlyProperty, true));
        style.Setters.Add(new Setter(TextBox.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(4, 2, 4, 2)));
        
        switch (level)
        {
            case LogLevel.Error:
                style.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(239, 68, 68))));
                break;
            case LogLevel.Warning:
                style.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(251, 146, 60))));
                break;
            case LogLevel.Info:
                style.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(163, 163, 163))));
                break;
            default:
                style.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(156, 163, 175))));
                break;
        }
        
        return style;
    }

    private async void LoadLogs()
    {
        try
        {
            // Load logs from the application's log file if it exists
            var logFilePath = LogService.GetLogFilePath();
            
            if (File.Exists(logFilePath))
            {
                // Read file asynchronously to avoid blocking UI
                var logContent = await File.ReadAllTextAsync(logFilePath);
                var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                lock (_logLock)
                {
                    _logEntries.Clear();
                    foreach (var line in lines.TakeLast(100)) // Show last 100 entries
                {
                    var level = ParseLogLevel(line);
                        AddLogEntryInternal(line, level);
                    }
                }
            }
            else
            {
                var localizationService = Services.ServiceLocator.GetServiceOptional<Services.Interfaces.ILocalizationService>();
                var message = localizationService?.GetString("NoLogFileFound") ?? "No log file found. Debug logs will appear here when generated.";
                AddLogEntry(message);
            }
            
            UpdateStatus();
        }
        catch (Exception ex)
        {
            AddLogEntry($"Error loading logs: {ex.Message}", LogLevel.Error);
        }
    }

    private void RefreshLogs()
    {
        if (_disposed)
            return;

        try
        {
            var logFilePath = LogService.GetLogFilePath();
            
            if (File.Exists(logFilePath))
            {
                // Read file on background thread to avoid blocking UI
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var logContent = await File.ReadAllTextAsync(logFilePath);
                        var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        
                        // Update UI on dispatcher thread
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (_disposed) return;
                            
                            lock (_logLock)
                    {
                        _logEntries.Clear();
                        foreach (var line in lines.TakeLast(100))
                        {
                                    var entry = new LogEntry
                                    {
                                        Timestamp = DateTime.Now,
                                        Message = line,
                                        Level = ParseLogLevel(line)
                                    };
                                    _logEntries.Add(entry);
                                }
                                
                                // Keep only last 500 entries
                                while (_logEntries.Count > 500)
                                {
                                    _logEntries.RemoveAt(0);
                                }
                            }
                            
                            // Update display outside of lock
                            UpdateLogDisplayInternal();
                        UpdateStatus();
                    });
                }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (!_disposed)
                                AddLogEntry($"Error refreshing logs: {ex.Message}", LogLevel.Error);
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            if (!_disposed)
            AddLogEntry($"Error refreshing logs: {ex.Message}", LogLevel.Error);
        }
    }

    private LogLevel ParseLogLevel(string message)
    {
        if (message.Contains("[Error]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Error;
        if (message.Contains("[Warning]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Warning;
        if (message.Contains("[Info]", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Info;
        
        return LogLevel.Info; // Default
    }

    private void AddLogEntry(string message, LogLevel level = LogLevel.Info)
    {
        if (_disposed) return;
        AddLogEntryInternal(message, level);
    }

    private void AddLogEntryInternal(string message, LogLevel level = LogLevel.Info)
    {
        if (_disposed) return;
        
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Message = message,
                Level = level
            };

        if (Dispatcher.CheckAccess())
        {
            AddLogEntryToCollection(entry);
        }
        else
        {
            // Use BeginInvoke for better responsiveness instead of Invoke
            Dispatcher.BeginInvoke(() => AddLogEntryToCollection(entry));
        }
    }

    private void AddLogEntryToCollection(LogEntry entry)
    {
        if (_disposed) return;
        
        lock (_logLock)
        {
            _logEntries.Add(entry);
            
            // Keep only last 500 entries to prevent memory issues (reduced from 1000)
            if (_logEntries.Count > 500)
            {
                _logEntries.RemoveAt(0);
            }
        }
        
        // Mark display update as pending (will be batched by timer)
        _pendingDisplayUpdate = true;
        UpdateStatus();
    }

    private void UpdateLogDisplay()
    {
        if (_disposed || LogPanel == null)
            return;

        // Mark as pending - timer will handle the actual update
        _pendingDisplayUpdate = true;
    }

    private void UpdateLogDisplayInternal()
    {
        if (_disposed || LogPanel == null)
            return;
            
        try
        {
            // Clear existing children
        LogPanel.Children.Clear();
        
        // Filter logs based on search text and log level
            List<LogEntry> entriesToDisplay;
            lock (_logLock)
            {
        var filteredEntries = _logEntries.AsEnumerable();
        
        // Apply log level filter
        if (_filterLevel.HasValue)
        {
            filteredEntries = filteredEntries.Where(e => e.Level == _filterLevel.Value);
        }
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            filteredEntries = filteredEntries.Where(e => 
                e.Message.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
        
                // Limit displayed entries to prevent UI memory issues
                entriesToDisplay = filteredEntries.TakeLast(200).ToList();
            }
            
            // Create UI elements on UI thread
            foreach (var entry in entriesToDisplay)
        {
            var textBox = new TextBox
            {
                Text = $"[{entry.Timestamp:HH:mm:ss}] {entry.Message}",
                    Style = _cachedStyles.TryGetValue(entry.Level, out var style) ? style : _cachedStyles[LogLevel.Info],
                    IsReadOnly = true
            };
            
            LogPanel.Children.Add(textBox);
        }
        
        if (AutoScrollCheckBox?.IsChecked == true)
        {
            // Scroll to bottom
            var scrollViewer = FindScrollViewer(LogPanel);
            scrollViewer?.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Error updating log display: {ex.Message}");
        }
    }

    private ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer scrollViewer)
                return scrollViewer;
            
            var result = FindScrollViewer(child);
            if (result != null)
                return result;
        }
        return null;
    }


    private void UpdateStatus()
    {
        var localizationService = Services.ServiceLocator.GetServiceOptional<Services.Interfaces.ILocalizationService>();
        if (localizationService != null)
        {
            LogCountTextBlock.Text = localizationService.GetString("LoadedLogEntries", _logEntries.Count);
        }
        else
        {
            LogCountTextBlock.Text = $"Loaded {_logEntries.Count} log entries";
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshLogs();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Clear the log file
            LogService.ClearLogs();
            
            lock (_logLock)
            {
                Dispatcher.Invoke(() =>
                {
                    _logEntries.Clear();
                    UpdateLogDisplay();
                    UpdateStatus();
                });
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to clear logs: {ex}");
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Logs",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"ModStack_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var logContent = string.Join("\n", _logEntries.Select(e => $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] {e.Message}"));
                File.WriteAllText(saveDialog.FileName, logContent);
                
                ModernConfirmationDialog.Show($"Logs saved to: {saveDialog.FileName}", "Save Complete", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ModernConfirmationDialog.Show($"Error saving logs: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_logEntries.Count == 0)
            {
                ModernConfirmationDialog.Show("No logs to copy.", "Information", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Build the complete log text
            var logText = new StringBuilder();
            foreach (var entry in _logEntries)
            {
                logText.AppendLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}");
            }

            // Copy to clipboard
            Clipboard.SetText(logText.ToString());
            
            // Show success message
            ModernConfirmationDialog.Show($"Copied {_logEntries.Count} log entries to clipboard.", "Success", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to copy logs: {ex}");
            ModernConfirmationDialog.Show($"Error copying logs: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        AddLogEntry(message, level);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchTextBox == null)
            return;
            
        _searchText = SearchTextBox.Text;
        UpdateLogDisplay();
    }

    private void LogLevelFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogLevelFilterComboBox == null)
            return;
            
        if (LogLevelFilterComboBox.SelectedIndex == 0)
        {
            _filterLevel = null; // All Levels
        }
        else if (LogLevelFilterComboBox.SelectedIndex == 1)
        {
            _filterLevel = LogLevel.Info;
        }
        else if (LogLevelFilterComboBox.SelectedIndex == 2)
        {
            _filterLevel = LogLevel.Warning;
        }
        else if (LogLevelFilterComboBox.SelectedIndex == 3)
        {
            _filterLevel = LogLevel.Error;
        }
        
        UpdateLogDisplay();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop and dispose timer
        StopTimer();

        // Unregister from LogService
        LogService.UnregisterServiceLogsView();

        // Clean up event handlers
        this.Loaded -= ServiceLogsView_Loaded;
        this.Unloaded -= ServiceLogsView_Unloaded;

        // Clear collections
        lock (_logLock)
        {
            Dispatcher.Invoke(() =>
            {
                _logEntries.Clear();
                if (LogPanel != null)
                {
                    LogPanel.Children.Clear();
                }
            });
        }

        // Clear cached styles
        _cachedStyles.Clear();

        LogService.Info("ServiceLogsView disposed and cleaned up");
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogLevel Level { get; set; }
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}
}