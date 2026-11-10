using System.Windows;

namespace ModStack.GUI.Helpers
{
    /// <summary>
    /// Helper class to manage blur overlay for dialogs
    /// </summary>
    public static class DialogBlurHelper
    {
        private static MainWindow? _mainWindow;

        /// <summary>
        /// Sets the main window reference for blur operations
        /// </summary>
        /// <param name="mainWindow">The main window instance</param>
        public static void SetMainWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// Shows the blur overlay
        /// </summary>
        public static void ShowBlur()
        {
            _mainWindow?.ShowBlurOverlay();
        }

        /// <summary>
        /// Hides the blur overlay
        /// </summary>
        public static void HideBlur()
        {
            _mainWindow?.HideBlurOverlay();
        }

        /// <summary>
        /// Shows a dialog with blur overlay and memory monitoring
        /// </summary>
        /// <param name="dialog">The dialog to show</param>
        /// <returns>The dialog result</returns>
        public static bool? ShowDialogWithBlur(Window dialog)
        {
            // Monitor memory usage during dialog operations
            var memoryBefore = GC.GetTotalMemory(false) / 1024 / 1024;
            
            ShowBlur();
            try
            {
                var memoryAfterBlur = GC.GetTotalMemory(false) / 1024 / 1024;
                if (memoryAfterBlur - memoryBefore > 50) // Log if blur causes >50MB increase
                {
                    Console.WriteLine($"⚠️ Blur effect caused {memoryAfterBlur - memoryBefore}MB memory increase");
                }
                
                return dialog.ShowDialog();
            }
            finally
            {
                HideBlur();
                
                // Force garbage collection after dialog to clean up any allocated resources
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var memoryAfter = GC.GetTotalMemory(false) / 1024 / 1024;
                Console.WriteLine($"📊 Dialog memory impact: {memoryBefore}MB → {memoryAfter}MB (Net: {memoryAfter - memoryBefore}MB)");
            }
        }
    }
}
