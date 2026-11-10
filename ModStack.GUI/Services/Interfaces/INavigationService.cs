using ModStack.Common.Models;

namespace ModStack.GUI.Services.Interfaces
{
    /// <summary>
    /// Abstracts navigation operations for better testability and loose coupling
    /// </summary>
    public interface INavigationService
    {
        void NavigateToDashboard();
        void NavigateToPackageManager();
        void NavigateToCreatePackage();
        void NavigateToServiceManagement();
        void NavigateToServiceLogs();
        void NavigateToStackManagement();
        void NavigateToCreateStack();
        void NavigateToEditStack(Stack stack);
        void NavigateToProjectManagement();
        void NavigateToCreateProject();
        Task NavigateToEditProjectAsync(long projectId);
        void NavigateToConfigEditor();
        void NavigateToHostsEditor();
        void NavigateToSettings();
        void NavigateToAbout();
        void NavigateToUpdates();
        void NavigateToExtensions();
        void NavigateToMailCatcher();
        bool NavigateBack();
    }
}
