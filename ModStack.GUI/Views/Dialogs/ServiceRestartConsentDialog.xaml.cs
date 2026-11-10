using ModStack.Common.Models;
using System.Collections.Generic;
using System.Windows;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Dialog to get user consent for restarting services during PHP configuration changes
/// </summary>
public partial class ServiceRestartConsentDialog : Window
{
    public bool UserConsented { get; private set; }
    
    public string ConsentMessage { get; }
    public List<ServiceDefinition> AffectedServices { get; }

    public ServiceRestartConsentDialog(string message, List<ServiceDefinition> affectedServices)
    {
        InitializeComponent();
        
        ConsentMessage = message;
        AffectedServices = affectedServices;
        UserConsented = false;
        
        DataContext = this;
        
        // Focus the Cancel button by default for safety
        CancelButton.Focus();
    }

    private void ProceedButton_Click(object sender, RoutedEventArgs e)
    {
        UserConsented = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        UserConsented = false;
        DialogResult = false;
        Close();
    }
}

