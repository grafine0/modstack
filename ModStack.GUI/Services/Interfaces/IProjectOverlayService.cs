using ModStack.GUI.ViewModels;

namespace ModStack.GUI.Services.Interfaces
{
    /// <summary>
    /// Service for handling project-related overlay operations
    /// </summary>
    public interface IProjectOverlayService
    {
        /// <summary>
        /// Shows a success overlay for project deletion
        /// </summary>
        /// <param name="project">The project that was deleted</param>
        /// <returns>Task representing the async operation</returns>
        Task ShowDeleteSuccessOverlayAsync(ProjectDisplayItem project);
    }
}
