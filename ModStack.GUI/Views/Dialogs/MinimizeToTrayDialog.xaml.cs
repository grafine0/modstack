using System.Windows;

namespace ModStack.GUI.Views.Dialogs;

/// <summary>
/// Interaction logic for MinimizeToTrayDialog.xaml
/// </summary>
public partial class MinimizeToTrayDialog : Window
{
    public enum MinimizeToTrayResult
    {
        Yes,
        No,
        Cancel
    }

    public MinimizeToTrayResult Result { get; private set; } = MinimizeToTrayResult.Cancel;
    public bool RememberChoice { get; private set; } = false;

    public MinimizeToTrayDialog()
    {
        InitializeComponent();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MinimizeToTrayResult.Yes;
        RememberChoice = RememberChoiceCheckBox.IsChecked ?? false;
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MinimizeToTrayResult.No;
        RememberChoice = RememberChoiceCheckBox.IsChecked ?? false;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MinimizeToTrayResult.Cancel;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = MinimizeToTrayResult.Cancel;
        DialogResult = false;
        Close();
    }
}
