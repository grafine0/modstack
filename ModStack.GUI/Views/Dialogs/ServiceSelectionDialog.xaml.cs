using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ModStack.Common.Models;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Dialog for selecting a service to add to a stack
/// </summary>
public partial class ServiceSelectionDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private ObservableCollection<ServiceDefinition> _availableServices;
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

    private readonly ModStack.GUI.Services.Interfaces.IDialogService _dialogService;

    public ServiceSelectionDialog(
        ModStack.GUI.Services.Interfaces.IDialogService dialogService,
        List<ServiceDefinition> availableServices)
    {
        InitializeComponent();
        _dialogService = dialogService;
        DataContext = this;

        AvailableServices = new ObservableCollection<ServiceDefinition>(availableServices);
        
        // Ensure proper centering
        this.Loaded += (s, e) => CenterDialog();
    }
    
    private void CenterDialog()
    {
        if (Owner != null)
        {
            // Center relative to owner window
            Left = Owner.Left + (Owner.Width - Width) / 2;
            Top = Owner.Top + (Owner.Height - Height) / 2;
        }
        else
        {
            // Center on screen using WPF
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            Left = (screenWidth - Width) / 2;
            Top = (screenHeight - Height) / 2;
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedService != null)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            await _dialogService.ShowInformationAsync("Please select a service to add.", "No Service Selected");
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

