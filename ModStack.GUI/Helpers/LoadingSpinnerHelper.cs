using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ModStack.GUI.Helpers
{
    /// <summary>
    /// Helper class for creating and managing animated loading spinners
    /// </summary>
    public static class LoadingSpinnerHelper
    {
        /// <summary>
        /// Creates an animated loading spinner TextBlock
        /// </summary>
        public static TextBlock CreateAnimatedSpinner()
        {
            var spinner = new TextBlock
            {
                Text = "⏳",
                RenderTransform = new RotateTransform(),
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // Get and start the rotation animation
            var storyboard = (Storyboard)Application.Current.Resources["RotateStoryboard"];
            var clonedStoryboard = storyboard.Clone();
            Storyboard.SetTarget(clonedStoryboard, spinner);
            clonedStoryboard.Begin();

            return spinner;
        }

        /// <summary>
        /// Stops the animation on a spinner TextBlock
        /// </summary>
        public static void StopSpinnerAnimation(TextBlock spinner)
        {
            if (spinner?.RenderTransform is RotateTransform transform)
            {
                // Stop any running animations on this transform
                transform.BeginAnimation(RotateTransform.AngleProperty, null);
            }
        }

        /// <summary>
        /// Sets up a button with an animated loading spinner and returns the original content
        /// </summary>
        public static object SetLoadingSpinner(Button button)
        {
            var originalContent = button.Content;
            var spinner = CreateAnimatedSpinner();
            button.Content = spinner;
            return originalContent;
        }

        /// <summary>
        /// Restores the button's original content and stops any spinner animation
        /// </summary>
        public static void RestoreButtonContent(Button button, object originalContent)
        {
            if (button.Content is TextBlock spinner)
            {
                StopSpinnerAnimation(spinner);
            }
            button.Content = originalContent;
        }
    }
}
