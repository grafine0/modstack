using System.ComponentModel;
using System.Windows;
using ModStack.Common.Models;
using System.Collections.Generic;
using System.Linq;

namespace ModStack.GUI.Views.Dialogs;

public partial class ServiceDeletionConfirmationDialog : Window, INotifyPropertyChanged
{
    private ServiceDeletionViewModel _viewModel;
    
    public new bool? DialogResult { get; private set; }
    public bool DeleteFiles => _viewModel.DeleteFiles;

    public ServiceDeletionConfirmationDialog(ServiceDisplayItem service)
    {
        InitializeComponent();
        
        _viewModel = new ServiceDeletionViewModel(service);
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

public class ServiceDeletionViewModel : INotifyPropertyChanged
{
    private bool _deleteFiles = false;
    
    public string ServiceName { get; }
    public string ServiceDisplayName { get; }
    public string ServiceType { get; }
    public string ServiceVersion { get; }
    public string ServicePort { get; }
    public string ServiceStatus { get; }
    public string ServicePath { get; }
    
    public string MainMessage { get; }
    public List<string> Warnings { get; }
    public string FilesDeletionMessage { get; }
    public string ActionSummary 
    { 
        get 
        {
            if (DeleteFiles)
            {
                return $"Will remove service definition and delete installation files";
            }
            else
            {
                return "Will only remove service definition (files preserved)";
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
            }
        }
    }

    public ServiceDeletionViewModel(ServiceDisplayItem service)
    {
        ServiceName = service.Name;
        ServiceDisplayName = service.Name;
        ServiceType = service.Type.ToString();
        ServiceVersion = service.Version;
        ServicePort = service.Port.ToString();
        ServiceStatus = service.Status;
        ServicePath = service.Path;
        
        // Create main message
        MainMessage = $"Are you sure you want to delete service '{service.Name}'?";
        
        // Create file deletion message
        FilesDeletionMessage = $"This will permanently delete:\n" +
                              $"• Service installation directory: {service.Path}\n" +
                              $"• All configuration files and data\n" +
                              $"• Any related virtual host configurations";
        
        // Create warnings
        Warnings = new List<string>
        {
            "This action cannot be undone.",
            "Service will be removed from ModStack permanently.",
            "Any projects using this service may be affected."
        };
        
        if (service.Status == "Running")
        {
            Warnings.Add("Service is currently running and will be stopped first.");
        }
        
        if (!string.IsNullOrEmpty(service.Path) && System.IO.Directory.Exists(service.Path))
        {
            Warnings.Add("Installation files will be permanently deleted if checkbox is selected.");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
