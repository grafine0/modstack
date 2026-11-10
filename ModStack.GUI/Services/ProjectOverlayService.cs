using System.Windows;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Views;

namespace ModStack.GUI.Services
{
    /// <summary>
    /// Service for handling project-related overlay operations
    /// </summary>
    public class ProjectOverlayService : IProjectOverlayService
    {
        private readonly IDialogService _dialogService;

        public ProjectOverlayService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Shows a success overlay for project deletion
        /// </summary>
        /// <param name="project">The project that was deleted</param>
        /// <returns>Task representing the async operation</returns>
        public async Task ShowDeleteSuccessOverlayAsync(ProjectDisplayItem project)
        {
            try
            {
                // Find the ProjectManagementView in the visual tree
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    var projectView = FindProjectManagementView(mainWindow);
                    if (projectView != null)
                    {
                        await projectView.DeleteProjectWithSuccessAsync(project);
                        return;
                    }
                }

                // Fallback: show a simple message box
                await _dialogService.ShowInformationAsync($"Project '{project.Name}' has been deleted successfully.", "Project Deleted");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error showing delete success overlay: {ex.Message}");
                // Fallback: show a simple message box
                await _dialogService.ShowInformationAsync($"Project '{project.Name}' has been deleted successfully.", "Project Deleted");
            }
        }

        /// <summary>
        /// Helper method to find ProjectManagementView in the visual tree
        /// </summary>
        private static ProjectManagementView? FindProjectManagementView(DependencyObject parent)
        {
            if (parent == null) return null;

            if (parent is ProjectManagementView projectView)
                return projectView;

            var childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var result = FindProjectManagementView(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
