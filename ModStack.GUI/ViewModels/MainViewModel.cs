using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ModStack.GUI.Models;
using ModStack.GUI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ModStack.GUI.ViewModels;

public partial class MainViewModel : ObservableObject, IRecipient<NavigationMessage>, IRecipient<StatusBarMessage>
{
    private readonly INavigationService _navigationService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private UserControl? _currentView;

    [ObservableProperty]
    private string _statusText = "Ready";

    public MainViewModel(INavigationService navigationService, ILogger<MainViewModel> logger)
    {
        _navigationService = navigationService;
        _logger = logger;
        
        // Register to receive navigation messages
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(NavigationMessage message)
    {
        CurrentView = message.View;
    }

    public void Receive(StatusBarMessage message)
    {
        StatusText = message.Message;
    }

    [RelayCommand]
    private void Navigate(string target)
    {
        switch (target)
        {
            case "Dashboard":
                // For dashboard, we'll need to add NavigateToDashboard to INavigationService
                // I'll add it in the next step.
                _navigationService.NavigateToDashboard();
                break;
            case "PackageManager":
                _navigationService.NavigateToPackageManager();
                break;
            case "CreatePackage":
                _navigationService.NavigateToCreatePackage();
                break;
            case "Services":
                _navigationService.NavigateToServiceManagement();
                break;
            case "ServiceLogs":
                _navigationService.NavigateToServiceLogs();
                break;
            case "Stacks":
                _navigationService.NavigateToStackManagement();
                break;
            case "CreateStack":
                _navigationService.NavigateToCreateStack();
                break;
            case "Projects":
                _navigationService.NavigateToProjectManagement();
                break;
            case "CreateProject":
                _navigationService.NavigateToCreateProject();
                break;
            case "ConfigEditor":
                _navigationService.NavigateToConfigEditor();
                break;
            case "HostsEditor":
                _navigationService.NavigateToHostsEditor();
                break;
            case "Settings":
                _navigationService.NavigateToSettings();
                break;
            case "Updates":
                _navigationService.NavigateToUpdates();
                break;
            case "MailCatcher":
                _navigationService.NavigateToMailCatcher();
                break;
            case "About":
                _navigationService.NavigateToAbout();
                break;
        }
    }
}
