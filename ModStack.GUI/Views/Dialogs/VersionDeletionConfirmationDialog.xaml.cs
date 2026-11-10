using System.ComponentModel;
using System.Windows;
using ModStack.Common.Models;
using ModStack.GUI.Services;
using System.Collections.Generic;
using System.Linq;

namespace ModStack.GUI.Views.Dialogs;

public partial class VersionDeletionConfirmationDialog : Window, INotifyPropertyChanged
{
    private VersionDeletionViewModel _viewModel;
    
    public new bool? DialogResult { get; private set; }
    public bool DeleteFiles => _viewModel.DeleteFiles;

    public VersionDeletionConfirmationDialog(string packageName, string version, ServiceUninstallValidation validation)
    {
        InitializeComponent();
        
        _viewModel = new VersionDeletionViewModel(packageName, version, validation);
        DataContext = _viewModel;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
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

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class VersionDeletionViewModel : INotifyPropertyChanged
{
    private bool _deleteFiles = false;
    
    public string PackageName { get; }
    public string Version { get; }
    public ServiceUninstallValidation Validation { get; }
    
    public string MainMessage { get; }
    public List<ServiceDefinition> RunningServices { get; }
    public List<ServiceDefinition> AffectedServices { get; }
    public List<string> Warnings { get; }
    public string FilesDeletionMessage { get; }
    public string FileCountInfo { get; }
    
    public bool HasRunningServices => RunningServices.Any();
    public bool HasAffectedServices => AffectedServices.Any();
    public bool HasWarnings => Warnings.Any();
    
    public bool DeleteFiles
    {
        get => _deleteFiles;
        set
        {
            if (_deleteFiles != value)
            {
                _deleteFiles = value;
                OnPropertyChanged(nameof(DeleteFiles));
            }
        }
    }

    public VersionDeletionViewModel(string packageName, string version, ServiceUninstallValidation validation)
    {
        PackageName = packageName;
        Version = version;
        Validation = validation;
        
        // Create main message
        MainMessage = $"Are you sure you want to delete version {version} of {packageName}?";
        
        // Extract service information
        RunningServices = validation.RunningServices ?? new List<ServiceDefinition>();
        AffectedServices = validation.AffectedServices ?? new List<ServiceDefinition>();
        Warnings = validation.Warnings ?? new List<string>();
        
        // Create file deletion message
        if (validation.FilesToDeleteCount > 0)
        {
            FilesDeletionMessage = $"This will permanently remove {validation.FilesToDeleteCount:N0} files from your system.";
            FileCountInfo = $"{validation.FilesToDeleteCount:N0} files will be affected";
        }
        else
        {
            FilesDeletionMessage = "No installation files found to delete.";
            FileCountInfo = "No files to delete";
        }
        
        // Add specific warnings for version deletion
        if (!Warnings.Contains("This action cannot be undone."))
        {
            Warnings.Add("This action cannot be undone.");
        }
        
        if (HasRunningServices)
        {
            Warnings.Add("Running services will be stopped before deletion.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
