using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ModStack.GUI.Helpers
{
    /// <summary>
    /// Helper class for managing success overlay states in dialogs and views
    /// </summary>
    public static class SuccessOverlayHelper
    {
        /// <summary>
        /// Shows a loading overlay with spinner
        /// </summary>
        /// <param name="overlayGrid">The Grid element that contains the overlay</param>
        /// <param name="loadingSpinner">The loading spinner element</param>
        /// <param name="successIcon">The success icon element</param>
        /// <param name="progressText">The progress text element</param>
        /// <param name="loadingMessage">The message to show during loading</param>
        public static void ShowLoadingOverlay(Grid overlayGrid, FrameworkElement loadingSpinner, 
            FrameworkElement successIcon, TextBlock progressText, string loadingMessage = "Loading...")
        {
            if (overlayGrid == null) return;

            // Show the overlay
            overlayGrid.Visibility = Visibility.Visible;
            
            // Show loading spinner, hide success icon
            if (loadingSpinner != null)
                loadingSpinner.Visibility = Visibility.Visible;
            if (successIcon != null)
                successIcon.Visibility = Visibility.Collapsed;
            
            // Update progress text
            if (progressText != null)
                progressText.Text = loadingMessage;
        }

        /// <summary>
        /// Shows success state with checkmark icon
        /// </summary>
        /// <param name="overlayGrid">The Grid element that contains the overlay</param>
        /// <param name="loadingSpinner">The loading spinner element</param>
        /// <param name="successIcon">The success icon element</param>
        /// <param name="progressText">The progress text element</param>
        /// <param name="successMessage">The message to show on success</param>
        public static void ShowSuccessState(Grid overlayGrid, FrameworkElement loadingSpinner, 
            FrameworkElement successIcon, TextBlock progressText, string successMessage = "Success!")
        {
            if (overlayGrid?.Visibility != Visibility.Visible) return;

            // Hide loading spinner, show success icon
            if (loadingSpinner != null)
                loadingSpinner.Visibility = Visibility.Collapsed;
            if (successIcon != null)
                successIcon.Visibility = Visibility.Visible;
            
            // Update progress text
            if (progressText != null)
                progressText.Text = successMessage;
        }

        /// <summary>
        /// Hides the overlay completely
        /// </summary>
        /// <param name="overlayGrid">The Grid element that contains the overlay</param>
        public static void HideOverlay(Grid overlayGrid)
        {
            if (overlayGrid != null)
                overlayGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows a loading overlay, performs an async operation, then shows success state
        /// </summary>
        /// <param name="overlayGrid">The Grid element that contains the overlay</param>
        /// <param name="loadingSpinner">The loading spinner element</param>
        /// <param name="successIcon">The success icon element</param>
        /// <param name="progressText">The progress text element</param>
        /// <param name="operation">The async operation to perform</param>
        /// <param name="loadingMessage">The message to show during loading</param>
        /// <param name="successMessage">The message to show on success</param>
        /// <param name="showSuccessDuration">How long to show success state before hiding (default: 2000ms)</param>
        public static async Task ExecuteWithSuccessOverlayAsync(Grid overlayGrid, FrameworkElement loadingSpinner, 
            FrameworkElement successIcon, TextBlock progressText, Func<Task> operation, 
            string loadingMessage = "Processing...", string successMessage = "Success!", 
            int showSuccessDuration = 2000)
        {
            try
            {
                // Show loading overlay
                ShowLoadingOverlay(overlayGrid, loadingSpinner, successIcon, progressText, loadingMessage);
                
                // Execute the operation
                await operation();
                
                // Show success state
                ShowSuccessState(overlayGrid, loadingSpinner, successIcon, progressText, successMessage);
                
                // Wait for success duration
                await Task.Delay(showSuccessDuration);
                
                // Hide overlay
                HideOverlay(overlayGrid);
            }
            catch (Exception)
            {
                // Hide overlay on error (error handling should be done by the caller)
                HideOverlay(overlayGrid);
                throw;
            }
        }

        /// <summary>
        /// Shows a loading overlay, performs an async operation, then shows success state
        /// </summary>
        /// <param name="overlayGrid">The Grid element that contains the overlay</param>
        /// <param name="loadingSpinner">The loading spinner element</param>
        /// <param name="successIcon">The success icon element</param>
        /// <param name="progressText">The progress text element</param>
        /// <param name="operation">The async operation to perform</param>
        /// <param name="loadingMessage">The message to show during loading</param>
        /// <param name="successMessage">The message to show on success</param>
        /// <param name="showSuccessDuration">How long to show success state before hiding (default: 2000ms)</param>
        /// <returns>The result of the operation</returns>
        public static async Task<T> ExecuteWithSuccessOverlayAsync<T>(Grid overlayGrid, FrameworkElement loadingSpinner, 
            FrameworkElement successIcon, TextBlock progressText, Func<Task<T>> operation, 
            string loadingMessage = "Processing...", string successMessage = "Success!", 
            int showSuccessDuration = 2000)
        {
            try
            {
                // Show loading overlay
                ShowLoadingOverlay(overlayGrid, loadingSpinner, successIcon, progressText, loadingMessage);
                
                // Execute the operation
                var result = await operation();
                
                // Show success state
                ShowSuccessState(overlayGrid, loadingSpinner, successIcon, progressText, successMessage);
                
                // Wait for success duration
                await Task.Delay(showSuccessDuration);
                
                // Hide overlay
                HideOverlay(overlayGrid);
                
                return result;
            }
            catch (Exception)
            {
                // Hide overlay on error (error handling should be done by the caller)
                HideOverlay(overlayGrid);
                throw;
            }
        }
    }
}
