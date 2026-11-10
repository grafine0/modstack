using System.Threading.Tasks;
using System.Windows.Media;

namespace ModStack.GUI.Services.Interfaces
{
    /// <summary>
    /// Service for showing dialogs in a testable way
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">The dialog title</param>
        /// <returns>True if user confirms, false otherwise</returns>
        Task<bool> ShowConfirmationAsync(string message, string title);
        
        /// <summary>
        /// Shows an error dialog
        /// </summary>
        /// <param name="message">The error message to display</param>
        /// <param name="title">The dialog title</param>
        Task ShowErrorAsync(string message, string title);
        
        /// <summary>
        /// Shows an information dialog
        /// </summary>
        /// <param name="message">The information message to display</param>
        /// <param name="title">The dialog title</param>
        Task ShowInformationAsync(string message, string title);
        
        /// <summary>
        /// Shows a color picker dialog
        /// </summary>
        /// <param name="currentColor">The currently selected color</param>
        /// <returns>The selected color if user confirms, null if cancelled</returns>
        Task<Color?> ShowColorPickerAsync(Color currentColor);
        
        /// <summary>
        /// Shows a folder selection dialog
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="initialDirectory">The initial directory to show</param>
        /// <returns>The selected folder path if user confirms, null if cancelled</returns>
        Task<string?> SelectFolderAsync(string title, string initialDirectory);
    }
}