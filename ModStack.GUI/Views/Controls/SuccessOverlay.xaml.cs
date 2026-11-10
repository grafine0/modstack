using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ModStack.GUI.Helpers;

namespace ModStack.GUI.Views.Controls
{
    /// <summary>
    /// Reusable success overlay control that can be added to any dialog or view
    /// </summary>
    public partial class SuccessOverlay : UserControl
    {
        public SuccessOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows loading state with spinner
        /// </summary>
        /// <param name="loadingMessage">Message to display during loading</param>
        /// <param name="subtitleMessage">Subtitle message (optional)</param>
        public void ShowLoading(string loadingMessage = "Loading...", string subtitleMessage = null)
        {
            // Show blur effect on background
            // DialogBlurHelper.ShowBlur();
            
            // Ensure the UserControl itself is visible
            this.Visibility = Visibility.Visible;
            
            SuccessOverlayHelper.ShowLoadingOverlay(
                OverlayGrid, 
                OverlayLoadingSpinner, 
                OverlaySuccessIcon, 
                OverlayProgressText, 
                loadingMessage);

            if (!string.IsNullOrEmpty(subtitleMessage))
            {
                OverlaySubtitleText.Text = subtitleMessage;
            }
        }

        /// <summary>
        /// Shows success state with checkmark
        /// </summary>
        /// <param name="successMessage">Message to display on success</param>
        /// <param name="subtitleMessage">Subtitle message (optional)</param>
        public void ShowSuccess(string successMessage = "Success!", string subtitleMessage = null)
        {
            // Ensure the UserControl itself is visible
            this.Visibility = Visibility.Visible;
            
            SuccessOverlayHelper.ShowSuccessState(
                OverlayGrid, 
                OverlayLoadingSpinner, 
                OverlaySuccessIcon, 
                OverlayProgressText, 
                successMessage);

            if (!string.IsNullOrEmpty(subtitleMessage))
            {
                OverlaySubtitleText.Text = subtitleMessage;
            }
        }

        /// <summary>
        /// Hides the overlay
        /// </summary>
        public void Hide()
        {
            // Hide blur effect on background
            // DialogBlurHelper.HideBlur();
            
            this.Visibility = Visibility.Collapsed;
            SuccessOverlayHelper.HideOverlay(OverlayGrid);
        }

        /// <summary>
        /// Executes an async operation with loading and success states
        /// </summary>
        /// <param name="operation">The async operation to execute</param>
        /// <param name="loadingMessage">Message during loading</param>
        /// <param name="successMessage">Message on success</param>
        /// <param name="subtitleMessage">Subtitle message</param>
        /// <param name="showSuccessDuration">How long to show success state</param>
        public async Task ExecuteWithSuccessAsync(Func<Task> operation, 
            string loadingMessage = "Processing...", 
            string successMessage = "Success!", 
            string subtitleMessage = null,
            int showSuccessDuration = 2000)
        {
            try
            {
                ShowLoading(loadingMessage, subtitleMessage);
                await operation();
                ShowSuccess(successMessage, subtitleMessage);
                await Task.Delay(showSuccessDuration);
                Hide();
            }
            catch
            {
                Hide();
                throw;
            }
        }

        /// <summary>
        /// Executes an async operation with loading and success states and returns a result
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">The async operation to execute</param>
        /// <param name="loadingMessage">Message during loading</param>
        /// <param name="successMessage">Message on success</param>
        /// <param name="subtitleMessage">Subtitle message</param>
        /// <param name="showSuccessDuration">How long to show success state</param>
        /// <returns>The result of the operation</returns>
        public async Task<T> ExecuteWithSuccessAsync<T>(Func<Task<T>> operation, 
            string loadingMessage = "Processing...", 
            string successMessage = "Success!", 
            string subtitleMessage = null,
            int showSuccessDuration = 2000)
        {
            try
            {
                ShowLoading(loadingMessage, subtitleMessage);
                var result = await operation();
                ShowSuccess(successMessage, subtitleMessage);
                await Task.Delay(showSuccessDuration);
                Hide();
                return result;
            }
            catch
            {
                Hide();
                throw;
            }
        }
    }
}
