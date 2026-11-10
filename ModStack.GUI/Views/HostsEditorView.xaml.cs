using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ModStack.GUI.Services;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views;

public partial class HostsEditorView : UserControl, INotifyPropertyChanged
{
    private static readonly char[] HostsFileSeparators = [' ', '\t'];
    
    public ObservableCollection<HostsEntry> HostsEntries { get; set; }
    public ObservableCollection<HostsEntry> FilteredHostsEntries { get; set; }
    private readonly string _hostsFilePath;
    
    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }
    
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                FilterHostsEntries();
            }
        }
    }
    
    private string _entryCountText = "0 entries";
    public string EntryCountText
    {
        get => _entryCountText;
        set
        {
            if (_entryCountText != value)
            {
                _entryCountText = value;
                OnPropertyChanged();
            }
        }
    }

    public HostsEditorView()
    {
        InitializeComponent();
        HostsEntries = new();
        FilteredHostsEntries = new();
        DataContext = this;
        
        // Windows hosts file path
        _hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
        
        LoadHostsFile();
    }

    private void LoadHostsFile()
    {
        try
        {
            HostsEntries.Clear();
            StatusTextBlock.Text = "Loading hosts file...";
            
            if (File.Exists(_hostsFilePath))
            {
                var lines = File.ReadAllLines(_hostsFilePath);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                        continue;
                    
                    var parts = trimmedLine.Split(HostsFileSeparators, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var entry = new HostsEntry
                        {
                            IpAddress = parts[0],
                            Hostname = string.Join(" ", parts.Skip(1))
                        };
                        HostsEntries.Add(entry);
                    }
                }
            }
            
            FilterHostsEntries(); // Refresh filtered view after loading
            UpdateStatus();
            StatusTextBlock.Text = "Hosts file loaded successfully";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error loading hosts file: {ex.Message}";
            LogService.Error($"Failed to load hosts file: {ex}");
        }
    }

    private void UpdateStatus()
    {
        UpdateEntryCountText();
    }
    
    private void UpdateEntryCountText()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            EntryCountText = $"{HostsEntries.Count} entries";
            SearchStatusTextBlock.Text = "";
        }
        else
        {
            EntryCountText = $"{FilteredHostsEntries.Count} of {HostsEntries.Count} entries";
            SearchStatusTextBlock.Text = $"Showing {FilteredHostsEntries.Count} results";
        }
    }
    
    private void FilterHostsEntries()
    {
        FilteredHostsEntries.Clear();
        
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // Show all entries
            foreach (var entry in HostsEntries)
            {
                FilteredHostsEntries.Add(entry);
            }
        }
        else
        {
            // Filter entries based on search text
            var filteredEntries = HostsEntries.Where(entry =>
                entry.IpAddress.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                entry.Hostname.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            );
            
            foreach (var entry in filteredEntries)
            {
                FilteredHostsEntries.Add(entry);
            }
        }
        
        UpdateEntryCountText();
    }

    private void AddEntryButton_Click(object sender, RoutedEventArgs e)
    {
        var newEntry = new HostsEntry
        {
            IpAddress = "127.0.0.1",
            Hostname = "example.local",
            IsNewlyAdded = true // Mark as newly added
        };
        
        HostsEntries.Add(newEntry);
        FilterHostsEntries(); // Refresh filtered view
        UpdateStatus();
        StatusTextBlock.Text = "New entry added";
        
        // Scroll to the newly added entry
        ScrollToNewEntry(newEntry);
        
        // Clear the "newly added" flag after a delay
        ClearNewlyAddedFlag(newEntry);
    }
    
    private async void ClearNewlyAddedFlag(HostsEntry entry)
    {
        // Wait 3 seconds, then clear the flag
        await Task.Delay(3000);
        entry.IsNewlyAdded = false;
        
        // Trigger UI update
        OnPropertyChanged(nameof(FilteredHostsEntries));
    }
    
    private void ScrollToNewEntry(HostsEntry newEntry)
    {
        try
        {
            // Find the item in the ItemsControl
            var itemsControl = HostsEntriesItemsControl;
            if (itemsControl?.ItemContainerGenerator != null)
            {
                // Wait for the item to be generated
                itemsControl.UpdateLayout();
                
                // Find the container for the new entry
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(newEntry);
                if (container is FrameworkElement element)
                {
                    // Scroll the container into view
                    element.BringIntoView();
                    
                    // Add a subtle highlight effect
                    UIAnimationHelper.HighlightElement(element);
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Error scrolling to new entry: {ex.Message}");
        }
    }
    

    private void DeleteEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is HostsEntry entry)
        {
            HostsEntries.Remove(entry);
            FilterHostsEntries(); // Refresh filtered view
            UpdateStatus();
            StatusTextBlock.Text = "Entry deleted";
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Trigger highlight animation on the save button
        // Use Dispatcher to ensure animation starts before command execution blocks UI
        if (sender is Button button)
        {
            _ = Dispatcher.BeginInvoke(() =>
            {
                UIAnimationHelper.HighlightButton(button);
            }, DispatcherPriority.Loaded);
        }
        
        // Set loading state
        IsLoading = true;
        
        // Give UI time to render animation before starting save operation
        await Task.Delay(50);
        
        try
        {
            StatusTextBlock.Text = "Saving hosts file...";
            
            var lines = new List<string>(); // IDE0028: List needed for Add operations, cannot use []
            
            // Add header comment
            lines.Add("# ModStack Hosts File Editor");
            lines.Add("# This file contains mappings of IP addresses to host names.");
            lines.Add("# Each entry should be kept on an individual line.");
            lines.Add("# The IP address should be placed in the first column followed by the corresponding host name.");
            lines.Add("# The IP address and the host name should be separated by at least one space.");
            lines.Add("#");
            lines.Add("# Additionally, comments (such as these) may be inserted on individual");
            lines.Add("# lines or following the machine name denoted by a '#' symbol.");
            lines.Add("#");
            lines.Add("# For example:");
            lines.Add("#");
            lines.Add("#      102.54.94.97     rhino.acme.com          # source server");
            lines.Add("#       38.25.63.10     x.acme.com              # x client host");
            lines.Add("");
            
            // Add entries
            foreach (var entry in HostsEntries)
            {
                if (!string.IsNullOrWhiteSpace(entry.IpAddress) && !string.IsNullOrWhiteSpace(entry.Hostname))
                {
                    lines.Add($"{entry.IpAddress}\t{entry.Hostname}");
                }
            }
            
            // Write to file
            File.WriteAllLines(_hostsFilePath, lines);
            
            // Show success toast notification
            Services.NotificationService.Instance.ShowSuccess(
                "Hosts file has been saved successfully",
                "Hosts File Saved");
            
            UpdateStatus();
            StatusTextBlock.Text = "Hosts file saved successfully";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error saving hosts file: {ex.Message}";
            LogService.Error($"Failed to save hosts file: {ex}");
            var dialogService = ModStack.GUI.Services.ServiceLocator.ServiceProvider.GetService(typeof(ModStack.GUI.Services.Interfaces.IDialogService)) as ModStack.GUI.Services.Interfaces.IDialogService;
            if (dialogService != null)
            {
                _ = dialogService.ShowErrorAsync($"Failed to save hosts file:\n\n{ex.Message}", "Save Error");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadHostsFile();
    }
    
    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchText = "";
        SearchTextBox.Clear();
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class HostsEntry
{
    public string IpAddress { get; set; } = "";
    public string Hostname { get; set; } = "";
    public bool IsNewlyAdded { get; set; } = false;
}
