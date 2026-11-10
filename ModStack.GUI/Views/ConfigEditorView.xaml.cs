using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views;

public partial class ConfigEditorView : UserControl, INotifyPropertyChanged
{
    private string _currentFilePath = "";
    private bool _isModified = false;
    private bool _isLoading = false;

    public ConfigEditorView()
    {
        InitializeComponent();
        
        // Set default syntax to Plain Text
        SyntaxComboBox.SelectedIndex = 0;
        
        UpdateStatus();
        DataContext = this; // Set DataContext to self for binding
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateStatus()
    {
        var localizationService = ServiceLocator.GetServiceOptional<ILocalizationService>();
        
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            StatusTextBlock.Text = localizationService?.GetString("ConfigEditorNoFileLoaded") ?? "No file loaded";
            LineCountTextBlock.Text = localizationService?.GetString("ConfigEditorLines", 0) ?? "Lines: 0";
            CharCountTextBlock.Text = localizationService?.GetString("ConfigEditorChars", 0) ?? "Chars: 0";
        }
        else
        {
            var fileName = Path.GetFileName(_currentFilePath);
            if (_isModified)
            {
                StatusTextBlock.Text = localizationService?.GetString("ConfigEditorModified", fileName) ?? $"Modified - {fileName}";
            }
            else
            {
                StatusTextBlock.Text = fileName;
            }
            
            var lines = ConfigTextBox.Text.Split('\n').Length;
            var chars = ConfigTextBox.Text.Length;
            
            LineCountTextBlock.Text = localizationService?.GetString("ConfigEditorLines", lines) ?? $"Lines: {lines}";
            CharCountTextBlock.Text = localizationService?.GetString("ConfigEditorChars", chars) ?? $"Chars: {chars}";
        }
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var localizationService = ServiceLocator.GetServiceOptional<ILocalizationService>();
        var openFileDialog = new OpenFileDialog
        {
            Title = localizationService?.GetString("ConfigEditorDialogTitle") ?? "Open Configuration File",
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml|INI Files (*.ini)|*.ini|YAML Files (*.yml;*.yaml)|*.yml;*.yaml",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadFile(openFileDialog.FileName);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileButton_Click(sender, e);
    }

    private void LoadFile(string filePath)
    {
        var localizationService = ServiceLocator.GetServiceOptional<ILocalizationService>();
        
        try
        {
            StatusTextBlock.Text = localizationService?.GetString("ConfigEditorLoadingFile") ?? "Loading file...";
            
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                ConfigTextBox.Text = content;
                _currentFilePath = filePath;
                _isModified = false;
                
                CurrentFilePathTextBlock.Text = filePath;
                
                // Auto-detect syntax based on file extension
                var extension = Path.GetExtension(filePath).ToLower();
                SyntaxComboBox.SelectedIndex = extension switch
                {
                    ".json" => 1, // JSON
                    ".xml" => 2, // XML
                    ".ini" => 3, // INI
                    ".yml" or ".yaml" => 4, // YAML
                    _ => 0 // Plain Text
                };
                
                UpdateStatus();
                StatusTextBlock.Text = localizationService?.GetString("ConfigEditorFileLoadedSuccessfully") ?? "File loaded successfully";
            }
            else
            {
                StatusTextBlock.Text = localizationService?.GetString("ConfigEditorFileNotFound") ?? "File not found";
            }
        }
        catch (Exception ex)
        {
            var errorMsg = localizationService?.GetString("ConfigEditorErrorLoadingFile", ex.Message) ?? $"Error loading file: {ex.Message}";
            StatusTextBlock.Text = errorMsg;
            LogService.Error($"Failed to load file {filePath}: {ex}");
            var loadErrorTitle = localizationService?.GetString("ConfigEditorLoadError") ?? "Load Error";
            var dialogService = ServiceLocator.GetServiceOptional<IDialogService>();
            if (dialogService != null)
            {
                _ = dialogService.ShowErrorAsync($"Failed to load file:\n\n{ex.Message}", loadErrorTitle);
            }
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
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsFile();
            }
            else
            {
                SaveCurrentFile();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SaveAsFile()
    {
        var localizationService = ServiceLocator.GetServiceOptional<ILocalizationService>();
        var saveFileDialog = new SaveFileDialog
        {
            Title = localizationService?.GetString("ConfigEditorDialogSaveTitle") ?? "Save Configuration File",
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml|INI Files (*.ini)|*.ini|YAML Files (*.yml)|*.yml",
            FilterIndex = 1
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            _currentFilePath = saveFileDialog.FileName;
            CurrentFilePathTextBlock.Text = _currentFilePath;
            SaveCurrentFile();
        }
    }

    private void SaveCurrentFile()
    {
        var localizationService = ServiceLocator.GetServiceOptional<ILocalizationService>();
        
        try
        {
            StatusTextBlock.Text = localizationService?.GetString("ConfigEditorSavingFile") ?? "Saving file...";
            
            File.WriteAllText(_currentFilePath, ConfigTextBox.Text);
            _isModified = false;
            
            // Show success toast notification
            Services.NotificationService.Instance.ShowSuccess(
                localizationService?.GetString("ConfigEditorFileSaved") ?? "File saved successfully",
                localizationService?.GetString("SuccessSaved") ?? "File Saved");
            
            UpdateStatus();
            StatusTextBlock.Text = localizationService?.GetString("ConfigEditorFileSaved") ?? "File saved successfully";
        }
        catch (Exception ex)
        {
            var errorMsg = localizationService?.GetString("ConfigEditorErrorSavingFile", ex.Message) ?? $"Error saving file: {ex.Message}";
            StatusTextBlock.Text = errorMsg;
            LogService.Error($"Failed to save file {_currentFilePath}: {ex}");
            var saveErrorTitle = localizationService?.GetString("ConfigEditorSaveError") ?? "Save Error";
            var dialogService = ServiceLocator.GetServiceOptional<IDialogService>();
            if (dialogService != null)
            {
                _ = dialogService.ShowErrorAsync($"Failed to save file:\n\n{ex.Message}", saveErrorTitle);
            }
        }
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            if (_isModified)
            {
                var dialogService = ServiceLocator.GetServiceOptional<IDialogService>();
                if (dialogService != null)
                {
                    var result = await dialogService.ShowConfirmationAsync(
                        "You have unsaved changes. Do you want to reload and lose your changes?", 
                        "Unsaved Changes");
                    
                    if (!result)
                        return;
                }
            }
            
            LoadFile(_currentFilePath);
        }
        else
        {
            StatusTextBlock.Text = "No file to reload";
        }
    }

    private void ConfigTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _isModified = true;
        UpdateStatus();
    }

    private void SyntaxComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // This could be extended to provide syntax highlighting
        // For now, it's just a visual indicator
        if (SyntaxComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var syntax = selectedItem.Tag?.ToString();
            StatusTextBlock.Text = $"Syntax mode: {syntax}";
        }
    }
}
