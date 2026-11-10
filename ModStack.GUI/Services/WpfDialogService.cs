using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ModStack.GUI.Services.Interfaces;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// WPF-specific implementation of dialog service
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        public async Task<bool> ShowConfirmationAsync(string message, string title)
        {
            return await Task.FromResult(
                MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes
            );
        }

        public async Task ShowErrorAsync(string message, string title)
        {
            await Task.Run(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        }

        public async Task ShowInformationAsync(string message, string title)
        {
            await Task.Run(() => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information));
        }

        public async Task<Color?> ShowColorPickerAsync(Color currentColor)
        {
            return await Task.FromResult(ShowColorPickerDialog(currentColor));
        }

        public async Task<string?> SelectFolderAsync(string title, string initialDirectory)
        {
            return await Task.FromResult(ShowFolderBrowserDialog(title, initialDirectory));
        }

        private Color? ShowColorPickerDialog(Color currentColor)
        {
            try
            {
                var colorDialog = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B),
                    FullOpen = true,
                    AllowFullOpen = true,
                    AnyColor = true,
                    SolidColorOnly = false
                };

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedColor = colorDialog.Color;
                    return Color.FromArgb(selectedColor.A, selectedColor.R, selectedColor.G, selectedColor.B);
                }

                return null; // User cancelled
            }
            catch
            {
                // If color dialog fails, return null
                return null;
            }
        }

        private string? ShowFolderBrowserDialog(string title, string initialDirectory)
        {
            try
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = title,
                    SelectedPath = initialDirectory,
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return folderDialog.SelectedPath;
                }

                return null; // User cancelled
            }
            catch
            {
                // If folder dialog fails, return null
                return null;
            }
        }
    }
}
