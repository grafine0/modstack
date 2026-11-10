using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ModStack.GUI.Services;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Views.Controls;

namespace ModStack.GUI.Views
{
    /// <summary>
    /// Interaction logic for ProjectManagementView.xaml - Clean MVVM implementation
    /// </summary>
    public partial class ProjectManagementView : UserControl
    {
        private readonly ProjectManagementViewModel _viewModel;

        public ProjectManagementView(ProjectManagementViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Handle visibility changes to refresh data when view becomes visible
            this.IsVisibleChanged += ProjectManagementView_IsVisibleChanged;
            
            Debug.WriteLine("📋 ProjectManagementView initialized with clean MVVM pattern");
        }

        #region Event Handlers

        private void ProjectManagementView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Refresh data when view becomes visible with proper error handling
            if (IsVisible && _viewModel != null)
            {
                _ = _viewModel.RefreshAsync().ContinueWith(t => 
                {
                    if (t.IsFaulted)
                    {
                        Debug.WriteLine($"❌ Error refreshing projects on visibility change: {t.Exception?.GetBaseException().Message}");
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        /// <summary>
        /// Event handler for Create Project button click
        /// </summary>
        private void CreateProjectButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.CreateProjectCommand?.Execute(null);
        }

        /// <summary>
        /// Handles project deletion with success overlay
        /// </summary>
        public async Task DeleteProjectWithSuccessAsync(ProjectDisplayItem? project)
        {
            if (project == null) return;

            try
            {
                // First, show the confirmation dialog
                var result = await _viewModel.ShowProjectDeletionConfirmationAsync(project);
                
                if (result.Confirmed)
                {
                    // User confirmed, now show success overlay during actual deletion
                    await SuccessOverlay.ExecuteWithSuccessAsync(
                        async () =>
                        {
                            await _viewModel.ExecuteProjectDeletionAsync(project, result.DeleteFiles);
                        },
                        loadingMessage: "Deleting project...",
                        successMessage: "Project deleted successfully!",
                        subtitleMessage: $"'{project.Name}' has been removed from your projects.",
                        showSuccessDuration: 2500
                    );
                }
                // If not confirmed, do nothing (dialog was cancelled)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in DeleteProjectWithSuccessAsync: {ex.Message}");
                // Error handling is done in the ViewModel, just hide overlay
                SuccessOverlay.Hide();
            }
        }

        #endregion
    }
}
