using System.ComponentModel;
using System.Windows;
using ModStack.Common.Models;
using System.Collections.Generic;
using System.Linq;

namespace ModStack.GUI.Views.Dialogs;

public partial class ProjectDeletionConfirmationDialog : Window, INotifyPropertyChanged
{
    private ProjectDeletionViewModel _viewModel;
    
    public new bool? DialogResult { get; private set; }
    public bool DeleteFiles => _viewModel.DeleteFiles;

    public ProjectDeletionConfirmationDialog(string projectName, string projectPath, string stackName, 
        List<string> virtualHostFiles, List<string> serviceNames)
    {
        InitializeComponent();
        
        _viewModel = new ProjectDeletionViewModel(projectName, projectPath, stackName, virtualHostFiles, serviceNames);
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

public class ProjectDeletionViewModel : INotifyPropertyChanged
{
    private bool _deleteFiles = false;
    
    public string ProjectName { get; }
    public string ProjectPath { get; }
    public string StackName { get; }
    public List<string> VirtualHostFiles { get; }
    public List<string> ServiceNames { get; }
    
    public string MainMessage { get; }
    public List<string> Warnings { get; }
    public string FilesDeletionMessage { get; }
    public string ServiceRestartMessage { get; }
    public string ActionSummary 
    { 
        get 
        {
            if (DeleteFiles)
            {
                var fileCount = VirtualHostFiles.Count + 1;
                return $"Will delete {fileCount} file(s) and restart {ServiceNames.Count} service(s)";
            }
            else
            {
                return "Will only remove from ModStack (files preserved)";
            }
        }
    }
    
    public bool DeleteFiles
    {
        get => _deleteFiles;
        set
        {
            if (_deleteFiles != value)
            {
                _deleteFiles = value;
                OnPropertyChanged(nameof(DeleteFiles));
                OnPropertyChanged(nameof(ActionSummary));
                OnPropertyChanged(nameof(ServiceRestartMessage));
            }
        }
    }

    public ProjectDeletionViewModel(string projectName, string projectPath, string stackName, 
        List<string> virtualHostFiles, List<string> serviceNames)
    {
        ProjectName = projectName;
        ProjectPath = projectPath;
        StackName = stackName;
        VirtualHostFiles = virtualHostFiles ?? new List<string>();
        ServiceNames = serviceNames ?? new List<string>();
        
        // Create main message
        MainMessage = $"Are you sure you want to delete project '{projectName}'?";
        
        // Create file deletion message
        var fileCount = VirtualHostFiles.Count + 1; // +1 for project directory
        FilesDeletionMessage = $"This will permanently delete:\n" +
                              $"• Project directory: {projectPath}\n" +
                              $"• Virtual host configuration files ({VirtualHostFiles.Count} file(s))";
        
        // Create service restart message
        if (ServiceNames.Any())
        {
            ServiceRestartMessage = $"The following services will be restarted to apply changes:\n" +
                                   string.Join("\n", ServiceNames.Select(s => $"• {s}"));
        }
        else
        {
            ServiceRestartMessage = "No services need to be restarted.";
        }
        
        // Create warnings
        Warnings = new List<string>
        {
            "This action cannot be undone.",
            "All project files and configurations will be permanently removed.",
            "Virtual host configurations will be removed from Apache/Nginx."
        };
        
        if (ServiceNames.Any())
        {
            Warnings.Add($"Services will be restarted: {string.Join(", ", ServiceNames)}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
