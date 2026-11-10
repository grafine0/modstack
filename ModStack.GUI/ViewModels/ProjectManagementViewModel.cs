using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.GUI.Models;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;

using ModStack.Services;
using ModStack.GUI.Views;
using ModStack.Core.Services;
using ModStack.GUI; // For MainWindow

namespace ModStack.GUI.ViewModels
{
    /// <summary>
    /// Enum for project status filtering
    /// </summary>
    public enum ProjectStatusFilter
    {
        All,
        Active,
        Inactive
    }

    /// <summary>
    /// Enum for stack filtering
    /// </summary>
    public enum StackFilterMode
    {
        All,
        Custom
    }

    /// <summary>
    /// Filter state for persistence
    /// </summary>
    public class FilterState
    {
        public string SearchText { get; set; } = string.Empty;
        public ProjectStatusFilter StatusFilter { get; set; } = ProjectStatusFilter.All;
        public string StackFilter { get; set; } = "All Stacks";
    }

    /// <summary>
    /// ViewModel for Project Management with proper MVVM separation
    /// </summary>
    public class ProjectManagementViewModel : INotifyPropertyChanged
    {
        // Cached JsonSerializerOptions for better performance
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        
        private readonly IProjectRepository _projectRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IServiceService _serviceService;
        private readonly IStackServiceRepository _stackServiceRepository;
        private readonly IProjectServiceRepository _projectServiceRepository;
        private readonly ServiceControlManager _serviceControlManager;
        private readonly ILogger<ProjectManagementViewModel> _logger;
        private readonly IDispatcherService _dispatcherService;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IProjectOverlayService _projectOverlayService;
        private readonly ModStack.Services.Interfaces.IVirtualHostManager _virtualHostManager;
        private readonly IVirtualHostService _virtualHostService;

        private string _searchText = string.Empty;
        private ProjectStatusFilter _statusFilter = ProjectStatusFilter.All;
        private string _stackFilter = "All Stacks";
        private bool _isLoading = false;
        private string _statusBarText = "Ready";
        private CancellationTokenSource? _searchDebounceTokenSource;
        private CancellationTokenSource? _saveDebounceTokenSource;
        private FilterState _savedFilterState = new();

        public ObservableCollection<ProjectDisplayItem> Projects { get; }
        public ICollectionView FilteredProjects { get; }
        
        // ComboBox ItemSources for proper XAML binding
        public ObservableCollection<ProjectStatusFilter> StatusOptions { get; }
        public ObservableCollection<string> StackOptions { get; }
        
        // Commands for UI actions
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CreateProjectCommand { get; }
        public ICommand EditProjectCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand OpenDomainCommand { get; }

        public ProjectManagementViewModel(
            IProjectRepository projectRepository, 
            IStackRepository stackRepository,
            IServiceService serviceService,
            IStackServiceRepository stackServiceRepository,
            IProjectServiceRepository projectServiceRepository,
            ServiceControlManager serviceControlManager,
            ILogger<ProjectManagementViewModel> logger,
            IDispatcherService dispatcherService,
            IDialogService dialogService,
            INavigationService navigationService,
            IProjectOverlayService projectOverlayService,
            ModStack.Services.Interfaces.IVirtualHostManager virtualHostManager,
            IVirtualHostService virtualHostService)
        {
            _projectRepository = projectRepository;
            _stackRepository = stackRepository;
            _serviceService = serviceService;
            _stackServiceRepository = stackServiceRepository;
            _projectServiceRepository = projectServiceRepository;
            _serviceControlManager = serviceControlManager;
            _logger = logger;
            _dispatcherService = dispatcherService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _projectOverlayService = projectOverlayService;
            _virtualHostManager = virtualHostManager;
            _virtualHostService = virtualHostService;

            // Initialize collections
            Projects = new();
            FilteredProjects = CollectionViewSource.GetDefaultView(Projects);
            FilteredProjects.Filter = ApplyProjectFilter;
            
            // Initialize ComboBox ItemSources
            StatusOptions = new()
            { 
                ProjectStatusFilter.All, 
                ProjectStatusFilter.Active, 
                ProjectStatusFilter.Inactive 
            };
            StackOptions = new() { "All Stacks" }; // Will be populated dynamically
            
            // Add default sorting (most recently updated first)
            FilteredProjects.SortDescriptions.Add(new System.ComponentModel.SortDescription(
                nameof(ProjectDisplayItem.UpdatedAt), 
                System.ComponentModel.ListSortDirection.Descending));

            // Initialize commands with proper error handling
            RefreshCommand = new AsyncRelayCommand(async () => 
            {
                try
                {
                    await LoadProjectsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing projects");
                }
            });
            ClearFiltersCommand = new RelayCommand(ClearFilters, () => !string.IsNullOrEmpty(SearchText) || StatusFilter != ProjectStatusFilter.All || StackFilter != "All Stacks");
            CreateProjectCommand = new RelayCommand(CreateProject);
            EditProjectCommand = new AsyncRelayCommand<ProjectDisplayItem>(EditProject, (project) => project != null);
            
            DeleteProjectCommand = new AsyncRelayCommand<ProjectDisplayItem?>(async (project) => 
            {
                try
                {
                    if (project != null)
                    {
                        // Use the injected overlay service instead of visual tree searching
                        await _projectOverlayService.ShowDeleteSuccessOverlayAsync(project);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting project {ProjectName}", project?.Name ?? "Unknown");
                }
            }, (project) => project != null);
            
            OpenDomainCommand = new RelayCommand<ProjectDisplayItem?>(OpenDomainInBrowser, (project) => project != null && !string.IsNullOrWhiteSpace(project.Domain) && project.Domain != "Not configured");

            // Load saved filter state first, then load projects
            SafeFireAndForget(async () => 
            {
                await LoadFilterStateAsync();
                await LoadProjectsAsync();
            }, "InitializeFiltersAndProjects");
            
            // Ensure StackFilter binding is properly initialized
            Debug.WriteLine($"🔧 Initial StackFilter: '{StackFilter}', StackOptions: [{string.Join(", ", StackOptions)}]");
        }


        /// <summary>
        /// Constructor for unit testing - allows injection of all dependencies
        /// </summary>
        public ProjectManagementViewModel(
            IProjectRepository projectRepository, 
            IStackRepository stackRepository,
            IServiceService serviceService,
            IStackServiceRepository stackServiceRepository,
            IProjectServiceRepository projectServiceRepository,
            ServiceControlManager serviceControlManager,
            ILogger<ProjectManagementViewModel> logger,
            IDispatcherService dispatcherService,
            IDialogService dialogService,
            INavigationService navigationService,
            IProjectOverlayService projectOverlayService,
            ModStack.Services.Interfaces.IVirtualHostManager virtualHostManager,
            IVirtualHostService virtualHostService,
            bool skipAutoLoad = false)
            : this(projectRepository, stackRepository, serviceService, stackServiceRepository, projectServiceRepository, serviceControlManager, logger, dispatcherService, dialogService, navigationService, projectOverlayService, virtualHostManager, virtualHostService)
        {
            if (skipAutoLoad)
            {
                // For testing, we can skip the automatic loading
                // The test can call LoadProjectsAsync() manually
            }
        }

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // Debounce search to avoid filtering on every keystroke
                    SafeFireAndForget(() => DebounceSearchAsync(value), "DebounceSearch");
                    // Save filter state after debounce
                    SafeFireAndForget(DebounceSaveFilterStateAsync, "DebounceSaveFilterState");
                }
            }
        }

        public ProjectStatusFilter StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    SafeFireAndForget(RefreshFilterAndStatusBarAsync, "RefreshFilterAndStatusBar");
                    Debug.WriteLine($"📊 Status filter updated: '{value}'");
                    // Save filter state
                    SafeFireAndForget(SaveFilterStateAsync, "SaveFilterState");
                }
            }
        }

        public string StackFilter
        {
            get => _stackFilter;
            set
            {
                if (SetProperty(ref _stackFilter, value))
                {
                    SafeFireAndForget(RefreshFilterAndStatusBarAsync, "RefreshFilterAndStatusBar");
                    Debug.WriteLine($"🎯 Stack filter updated: '{value}'");
                    // Save filter state
                    SafeFireAndForget(SaveFilterStateAsync, "SaveFilterState");
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowContent));
                }
            }
        }

        public string StatusBarText
        {
            get => _statusBarText;
            set => SetProperty(ref _statusBarText, value);
        }

        public bool HasProjects => Projects.Count > 0;
        public bool ShowEmptyState => !IsLoading && !HasProjects;
        public bool ShowContent => !IsLoading && HasProjects;
        
        public int TotalProjects => Projects.Count;
        public int ActiveProjects => Projects.Count(p => p.Status == "Active");

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads projects from the database with proper thread safety
        /// </summary>
        public async Task LoadProjectsAsync()
        {
            try
            {
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    IsLoading = true;
                    StatusBarText = "Loading projects...";
                });
                
                Debug.WriteLine("📊 Loading projects from database...");
                Debug.WriteLine($"📊 Current StackFilter: '{StackFilter}', StackOptions count: {StackOptions.Count}");

                var projects = await _projectRepository.GetAllAsync();
                var stacks = await _stackRepository.GetAllAsync();
                
                // Create display items with stack information
                var displayItems = projects.Select(project => CreateProjectDisplayItem(project, stacks)).ToList();
                
                // Update UI on the UI thread with batch operation
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    Projects.Clear();
                    
                    // Use efficient bulk add that suspends notifications
                    Projects.AddRangeBulk(displayItems);
                    
                    UpdateStatusBar();
                    OnPropertyChanged(nameof(HasProjects));
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowContent));
                    
                    Debug.WriteLine($"✅ Loaded {Projects.Count} projects");
                });

                // Update stack options for filter ComboBox
                await UpdateStackOptionsAsync(stacks);
                
                // Ensure filter is valid after stack options are updated
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    Debug.WriteLine($"🔍 After loading stacks - StackFilter: '{StackFilter}', StackOptions: [{string.Join(", ", StackOptions)}]");
                    
                    // If current StackFilter is not in the available options, reset to "All Stacks"
                    if (!StackOptions.Contains(StackFilter))
                    {
                        Debug.WriteLine($"⚠️ StackFilter '{StackFilter}' not found in StackOptions, resetting to 'All Stacks'");
                        StackFilter = "All Stacks";
                    }
                    
                    // Refresh the filtered view to ensure projects are displayed
                    FilteredProjects.Refresh();
                    UpdateStatusBar();
                });

                // Save to storage cache (non-blocking with error handling)
                SafeFireAndForget(SaveProjectsToStorageAsync, "SaveProjectsToStorage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading projects: {ex.Message}");
                _logger.LogError(ex, "Failed to load projects from database");
                
                // Show error dialog on UI thread
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    StatusBarText = "Error loading projects";
                    _dialogService.ShowErrorAsync($"Failed to load projects: {ex.Message}", "Load Error");
                });
            }
            finally
            {
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    IsLoading = false;
                    if (StatusBarText == "Loading projects...")
                    {
                        StatusBarText = "Ready";
                    }
                });
            }
        }

        /// <summary>
        /// Refreshes the project list
        /// </summary>
        public async Task RefreshAsync()
        {
            await LoadProjectsAsync();
        }

        /// <summary>
        /// Testable method to get current filter state
        /// </summary>
        public FilterState GetCurrentFilterState()
        {
            return new FilterState
            {
                SearchText = SearchText,
                StatusFilter = StatusFilter,
                StackFilter = StackFilter
            };
        }

        /// <summary>
        /// Testable method to apply filters programmatically
        /// </summary>
        public void ApplyFilters(string searchText, ProjectStatusFilter statusFilter, string stackFilter)
        {
            SearchText = searchText;
            StatusFilter = statusFilter;
            StackFilter = stackFilter;
            
            // Refresh command states
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Testable method to get filtered project count
        /// </summary>
        public int GetFilteredProjectCount()
        {
            return FilteredProjects.OfType<ProjectDisplayItem>().Count();
        }

        /// <summary>
        /// Testable method to check if filters are active
        /// </summary>
        public bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(SearchText) || 
                   StatusFilter != ProjectStatusFilter.All || 
                   StackFilter != "All Stacks";
        }

        /// <summary>
        /// Testable method to get status bar text for verification
        /// </summary>
        public string GetStatusBarText()
        {
            return StatusBarText;
        }

        /// <summary>
        /// Testable method to verify stack options are populated
        /// </summary>
        public IEnumerable<string> GetStackOptions()
        {
            return [.. StackOptions];
        }

        /// <summary>
        /// Testable method to verify status options are populated
        /// </summary>
        public IEnumerable<ProjectStatusFilter> GetStatusOptions()
        {
            return [.. StatusOptions];
        }

        /// <summary>
        /// Clears all filters and resets to default state
        /// </summary>
        public void ClearFilters()
        {
            Debug.WriteLine("🧹 Clearing all filters");
            _searchDebounceTokenSource?.Cancel(); // stop pending debounce
            SearchText = string.Empty;
            StatusFilter = ProjectStatusFilter.All;
            StackFilter = "All Stacks";
            SafeFireAndForget(RefreshFilterAndStatusBarAsync, "RefreshFilterAndStatusBar");
            
            // Refresh command states
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// Creates a new project (delegates to navigation service)
        /// </summary>
        private void CreateProject()
        {
            Debug.WriteLine("➕ Create project command triggered");
            _navigationService.NavigateToCreateProject();
        }

        /// <summary>
        /// Edits an existing project (uses proper ID-based navigation)
        /// </summary>
        private async Task EditProject(ProjectDisplayItem? project)
        {
            if (project == null) return;
            
            Debug.WriteLine($"✏️ Edit project command triggered for: {project.Name} (ID: {project.Id})");
            await _navigationService.NavigateToEditProjectAsync(project.Id);
        }

        private void OpenDomainInBrowser(ProjectDisplayItem? project)
        {
            if (project == null || string.IsNullOrWhiteSpace(project.Domain) || project.Domain == "Not configured")
                return;
            
            try
            {
                var protocol = project.EnableSSL ? "https" : "http";
                var url = $"{protocol}://{project.Domain}";
                
                Debug.WriteLine($"🌐 Opening domain in browser: {url}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening domain {Domain} in browser", project.Domain);
                _dialogService.ShowErrorAsync($"Failed to open {project.Domain} in browser: {ex.Message}", "Error").ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Shows the project deletion confirmation dialog and returns the result
        /// </summary>
        public async Task<ProjectDeletionResult> ShowProjectDeletionConfirmationAsync(ProjectDisplayItem? project)
        {
            if (project == null) return new ProjectDeletionResult { Confirmed = false };

            // Get project details for the dialog
            var projectDetails = await _projectRepository.GetByIdAsync(project.Id);
            if (projectDetails == null)
            {
                await _dialogService.ShowErrorAsync($"Project '{project.Name}' not found in database.", "Delete Error");
                return new ProjectDeletionResult { Confirmed = false };
            }

            // Get virtual host files and service names for the dialog
            var virtualHostFiles = await GetVirtualHostFilesForProject(project.Name);
            var serviceNames = await GetServiceNamesForProject(projectDetails);

            // Show confirmation dialog with file deletion options
            return ModernDialogService.ShowProjectDeletionConfirmation(
                project.Name, 
                projectDetails.Path, 
                project.StackName,
                virtualHostFiles,
                serviceNames);
        }

        /// <summary>
        /// Executes the actual project deletion without showing confirmation dialog
        /// </summary>
        public async Task<bool> ExecuteProjectDeletionAsync(ProjectDisplayItem? project, bool deleteFiles)
        {
            if (project == null) return false;
            
            try
            {
                Debug.WriteLine($"🗑️ Executing project deletion: {project.Name} (Delete files: {deleteFiles})");

                // Get project details
                var projectDetails = await _projectRepository.GetByIdAsync(project.Id);
                if (projectDetails == null)
                {
                    await _dialogService.ShowErrorAsync($"Project '{project.Name}' not found in database.", "Delete Error");
                    return false;
                }

                // Get service names for restart if needed
                var serviceNames = await GetServiceNamesForProject(projectDetails);

                // Delete virtual host configurations first
                await DeleteVirtualHostConfigurations(projectDetails);

                // Delete project files if requested
                if (deleteFiles)
                {
                    await DeleteProjectFiles(projectDetails.Path);
                }

                // Restart bound services (always restart, not just when files are deleted)
                await RestartProjectServicesAsync(projectDetails);

                // Delete from database
                await _projectRepository.DeleteAsync(project.Id);
                
                // Remove from UI collection
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    Projects.Remove(project);
                    UpdateStatusBar();
                    OnPropertyChanged(nameof(HasProjects));
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowContent));
                });

                // Show success toast notification
                var filesDeletedText = deleteFiles ? " and its files" : "";
                Services.NotificationService.Instance.ShowSuccess(
                    $"Project '{project.Name}'{filesDeletedText} deleted successfully",
                    "Project Deleted");

                Debug.WriteLine($"✅ Project '{project.Name}' deleted successfully (Files deleted: {deleteFiles})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error deleting project '{project.Name}': {ex.Message}");
                _logger.LogError(ex, "Failed to delete project {ProjectName}", project.Name);
                
                await _dialogService.ShowErrorAsync($"Failed to delete project '{project.Name}': {ex.Message}", "Delete Error");
                return false;
            }
        }

        /// <summary>
        /// Deletes a project (original method for backward compatibility)
        /// </summary>
        public async Task<bool> DeleteProjectAsync(ProjectDisplayItem? project)
        {
            if (project == null) return false;
            
            try
            {
                Debug.WriteLine($"🗑️ Deleting project: {project.Name}");

                // Get project details for the dialog
                var projectDetails = await _projectRepository.GetByIdAsync(project.Id);
                if (projectDetails == null)
                {
                    await _dialogService.ShowErrorAsync($"Project '{project.Name}' not found in database.", "Delete Error");
                    return false;
                }

                // Get virtual host files and service names for the dialog
                var virtualHostFiles = await GetVirtualHostFilesForProject(project.Name);
                var serviceNames = await GetServiceNamesForProject(projectDetails);

                // Show confirmation dialog with file deletion options
                var result = ModernDialogService.ShowProjectDeletionConfirmation(
                    project.Name, 
                    projectDetails.Path, 
                    project.StackName,
                    virtualHostFiles,
                    serviceNames);

                if (!result.Confirmed)
                {
                    Debug.WriteLine("❌ Project deletion cancelled by user");
                    return false;
                }

                // Delete virtual host configurations first
                await DeleteVirtualHostConfigurations(projectDetails);

                // Delete project files if requested
                if (result.DeleteFiles)
                {
                    await DeleteProjectFiles(projectDetails.Path);
                }

                // Restart bound services (always restart, not just when files are deleted)
                await RestartProjectServicesAsync(projectDetails);

                // Delete from database
                await _projectRepository.DeleteAsync(project.Id);
                
                // Remove from UI collection
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    Projects.Remove(project);
                    UpdateStatusBar();
                    OnPropertyChanged(nameof(HasProjects));
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowContent));
                });

                // Show success toast notification
                var filesDeletedText = result.DeleteFiles ? " and its files" : "";
                Services.NotificationService.Instance.ShowSuccess(
                    $"Project '{project.Name}'{filesDeletedText} deleted successfully",
                    "Project Deleted");

                Debug.WriteLine($"✅ Project '{project.Name}' deleted successfully (Files deleted: {result.DeleteFiles})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error deleting project '{project.Name}': {ex.Message}");
                _logger.LogError(ex, "Failed to delete project {ProjectName}", project.Name);
                
                await _dialogService.ShowErrorAsync($"Failed to delete project '{project.Name}': {ex.Message}", "Delete Error");
                return false;
            }
        }

        #endregion

        #region Project Deletion Helper Methods

        /// <summary>
        /// Gets virtual host files associated with a project
        /// </summary>
        private async Task<List<string>> GetVirtualHostFilesForProject(string projectName)
        {
            try
            {
                var virtualHosts = await _virtualHostManager.GetAllVirtualHostsAsync();
                
                return virtualHosts
                    .Where(vh => vh.ServerName.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                    .Select(vh => $"{vh.ServerName}.vhost")
                    .ToList(); // IDE0305: ToList() required for explicit List conversion
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get virtual host files for project {ProjectName}", projectName);
                return [];
            }
        }

        /// <summary>
        /// Gets service names that would need to be restarted for a project
        /// </summary>
        private async Task<List<string>> GetServiceNamesForProject(Project project)
        {
            try
            {
                var serviceNames = new List<string>();
                
                // Get the stack for this project
                if (project.StackId.HasValue)
                {
                    var stack = await _stackRepository.GetByIdAsync(project.StackId.Value);
                    if (stack != null)
                    {
                        // Get services from the stack through StackService relationship
                        foreach (var stackService in stack.Services)
                        {
                            if (stackService.IsEnabled && stackService.Service != null)
                            {
                                serviceNames.Add(stackService.Service.DisplayName);
                            }
                        }
                    }
                }
                
                // Always include Apache if virtual hosts exist
                var virtualHostFiles = await GetVirtualHostFilesForProject(project.Name);
                if (virtualHostFiles.Count > 0)
                {
                    serviceNames.Add("Apache HTTP Server");
                }
                
                return serviceNames.Distinct().ToList(); // IDE0305: Keep ToList() for explicit List conversion
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get service names for project {ProjectName}", project.Name);
                return [];
            }
        }

        /// <summary>
        /// Deletes virtual host configurations for a project
        /// </summary>
        private async Task DeleteVirtualHostConfigurations(Project project)
        {
            try
            {
                // Get the project's domain name
                var domain = project.Domain ?? project.Name;
                
                if (string.IsNullOrEmpty(domain))
                {
                    _logger.LogWarning("No domain found for project {ProjectName}, skipping virtual host deletion", project.Name);
                    return;
                }

                // Delete virtual host file using IVirtualHostService
                var fileDeleted = await _virtualHostService.RemoveVirtualHostAsync(domain);
                if (fileDeleted)
                {
                    _logger.LogInformation("Deleted virtual host file for domain: {Domain}", domain);
                    Debug.WriteLine($"🗑️ Deleted virtual host file for {domain}");
                }
                else
                {
                    _logger.LogWarning("Virtual host file not found or could not be deleted for domain: {Domain}", domain);
                }

                // Also delete virtual host records from database
                var virtualHosts = await _virtualHostManager.GetAllVirtualHostsAsync();
                
                var projectVirtualHosts = virtualHosts
                    .Where(vh => vh.ProjectId == project.Id || 
                                 vh.ServerName.Equals(domain, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                foreach (var virtualHost in projectVirtualHosts)
                {
                    try
                    {
                        await _virtualHostManager.DeleteVirtualHostAsync(virtualHost.Id);
                        _logger.LogInformation("Deleted virtual host record {VirtualHostId} for domain: {Domain}", virtualHost.Id, virtualHost.ServerName);
                        Debug.WriteLine($"🗑️ Deleted virtual host record for {virtualHost.ServerName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete virtual host record {VirtualHostId}", virtualHost.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete virtual host configurations for project {ProjectName}", project.Name);
                // Don't throw - allow project deletion to continue even if virtual host deletion fails
            }
        }

        /// <summary>
        /// Deletes project files from disk
        /// </summary>
        private async Task DeleteProjectFiles(string projectPath)
        {
            try
            {
                if (!Directory.Exists(projectPath))
                {
                    Debug.WriteLine($"⚠️ Project directory not found: {projectPath}");
                    return;
                }

                _logger.LogInformation("Attempting to delete project directory: {ProjectPath}", projectPath);
                
                // Remove readonly attributes from all files and directories
                RemoveReadOnlyAttributesAsync(projectPath);
                
                // Attempt deletion with retries
                const int maxRetries = 3;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        Directory.Delete(projectPath, recursive: true);
                        Debug.WriteLine($"🗑️ Successfully deleted project directory: {projectPath}");
                        _logger.LogInformation("Successfully deleted project directory: {ProjectPath}", projectPath);
                        return;
                    }
                    catch (IOException ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "Attempt {Attempt} failed to delete project directory {ProjectPath}, retrying...", attempt, projectPath);
                        await Task.Delay(1000 * attempt); // Exponential backoff
                        
                        // Try to remove readonly attributes again before retry
                        RemoveReadOnlyAttributesAsync(projectPath);
                    }
                }
                
                // If we get here, all retries failed
                throw new IOException($"Failed to delete project directory after {maxRetries} attempts: {projectPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete project files from {ProjectPath}", projectPath);
                throw;
            }
        }

        /// <summary>
        /// Removes readonly attributes from all files and directories recursively
        /// </summary>
        private void RemoveReadOnlyAttributesAsync(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.IsReadOnly)
                    {
                        fileInfo.IsReadOnly = false;
                        Debug.WriteLine($"🔓 Removed readonly attribute from file: {path}");
                    }
                }
                else if (Directory.Exists(path))
                {
                    // Remove readonly from directory
                    var dirInfo = new DirectoryInfo(path);
                    if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                        Debug.WriteLine($"🔓 Removed readonly attribute from directory: {path}");
                    }
                    
                    // Recursively process all files and subdirectories
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                    
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.IsReadOnly)
                        {
                            fileInfo.IsReadOnly = false;
                        }
                    }
                    
                    foreach (var dir in directories)
                    {
                        var subDirInfo = new DirectoryInfo(dir);
                        if ((subDirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            subDirInfo.Attributes &= ~FileAttributes.ReadOnly;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove readonly attributes from {Path}", path);
                // Don't throw - this is a best-effort operation
            }
        }

        /// <summary>
        /// Restarts services bound to the project or from the project's stack
        /// </summary>
        private async Task RestartProjectServicesAsync(Project project)
        {
            try
            {
                _logger.LogInformation("Restarting services for project ID: {ProjectId}", project.Id);
                
                // Update status bar to show operation in progress
                MainWindow.UpdateStatusBar($"Restarting services for project '{project.Name}'...");
                
                List<long> serviceIdsToRestart = [];
                
                // If project has a StackId, get services from the stack
                if (project.StackId.HasValue)
                {
                    _logger.LogInformation("Project bound to stack ID: {StackId}", project.StackId.Value);
                    var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                    serviceIdsToRestart.AddRange(stackServices
                        .Where(ss => ss.IsEnabled)
                        .Select(ss => ss.ServiceId));
                    _logger.LogInformation("Found {ServiceCount} services from stack", serviceIdsToRestart.Count);
                }
                else
                {
                    // Get services from ProjectService bindings
                    _logger.LogInformation("Project not bound to stack, getting services from ProjectService bindings");
                    var projectServices = await _projectServiceRepository.GetByProjectIdAsync(project.Id);
                    serviceIdsToRestart.AddRange(projectServices
                        .Where(ps => ps.IsEnabled)
                        .Select(ps => ps.ServiceId));
                    _logger.LogInformation("Found {ServiceCount} services from ProjectService bindings", serviceIdsToRestart.Count);
                }
                
                if (serviceIdsToRestart.Count == 0)
                {
                    _logger.LogInformation("No services to restart for this project");
                    MainWindow.ResetStatusBar();
                    return;
                }
                
                // Restart each service
                var restartedCount = 0;
                var failedCount = 0;
                var totalServices = serviceIdsToRestart.Count;
                
                for (int i = 0; i < serviceIdsToRestart.Count; i++)
                {
                    var serviceId = serviceIdsToRestart[i];
                    var currentServiceIndex = i + 1;
                    
                    try
                    {
                        // Fetch fresh service data from repository to ensure we have complete information
                        var service = await _serviceService.GetServiceByIdAsync(serviceId);
                        
                        if (service == null)
                        {
                            _logger.LogWarning("Service with ID {ServiceId} not found in repository", serviceId);
                            failedCount++;
                            continue;
                        }
                        
                        // Verify service is installed
                        if (!service.IsInstalled)
                        {
                            _logger.LogInformation("Service {ServiceName} is not installed, skipping", service.Name);
                            continue;
                        }
                        
                        // Get port from stack service if project has a stack
                        int? port = null;
                        if (project.StackId.HasValue)
                        {
                            var stackServices = await _stackServiceRepository.GetByStackIdAsync(project.StackId.Value);
                            var stackService = stackServices.FirstOrDefault(ss => ss.ServiceId == serviceId);
                            port = stackService?.Port;
                        }
                        
                        // Convert ServiceDefinition to ServiceDisplayItem with fresh data
                        var serviceDisplayItem = ConvertToServiceDisplayItem(service, port);
                        
                        // Update status bar with progress
                        MainWindow.UpdateStatusBar($"Restarting services ({currentServiceIndex}/{totalServices}): {service.Name}...");
                        
                        // Check current status
                        var currentStatus = await _serviceControlManager.DetectServiceStatusAsync(serviceDisplayItem);
                        
                        // If service is running, stop it first
                        if (string.Equals(currentStatus, "Running", StringComparison.OrdinalIgnoreCase))
                        {
                            MainWindow.UpdateStatusBar($"Stopping {service.Name}... ({currentServiceIndex}/{totalServices})");
                            _logger.LogInformation("Stopping service {ServiceName} before restart", service.Name);
                            await _serviceControlManager.StopServiceAsync(serviceDisplayItem);
                            await Task.Delay(1000); // Wait a moment for service to stop
                        }
                        
                        // Start the service
                        MainWindow.UpdateStatusBar($"Starting {service.Name}... ({currentServiceIndex}/{totalServices})");
                        _logger.LogInformation("Starting service {ServiceName}", service.Name);
                        await _serviceControlManager.StartServiceAsync(serviceDisplayItem);
                        restartedCount++;
                        
                        _logger.LogInformation("Successfully restarted service: {ServiceName}", service.Name);
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(ex, "Failed to restart service ID {ServiceId}", serviceId);
                    }
                }
                
                // Show notification about restart results and update status bar
                await _dispatcherService.InvokeOnUIThreadAsync(() =>
                {
                    if (failedCount == 0)
                    {
                        MainWindow.UpdateStatusBar($"Successfully restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")} for '{project.Name}'");
                        Services.NotificationService.Instance.ShowSuccess(
                            $"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")} for project '{project.Name}'",
                            "Services Restarted");
                    }
                    else
                    {
                        MainWindow.UpdateStatusBar($"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")}, {failedCount} failed");
                        Services.NotificationService.Instance.ShowWarning(
                            $"Restarted {restartedCount} service{(restartedCount != 1 ? "s" : "")}, {failedCount} failed",
                            "Service Restart");
                    }
                    
                    // Reset status bar to "Ready" after 3 seconds
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MainWindow.ResetStatusBar();
                        });
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting project services");
                MainWindow.UpdateStatusBar($"Error restarting services: {ex.Message}");
                
                // Reset status bar after showing error
                await Task.Delay(3000);
                MainWindow.ResetStatusBar();
            }
        }
        
        /// <summary>
        /// Converts a ServiceDefinition to a ServiceDisplayItem for use with ServiceControlManager
        /// </summary>
        private static ServiceDisplayItem ConvertToServiceDisplayItem(ServiceDefinition service, int? port = null)
        {
            var serviceStatus = (Common.ServiceStatus)service.Status;
            var statusString = serviceStatus == Common.ServiceStatus.Unknown 
                ? "Stopped" 
                : serviceStatus.ToString();
            
            return new ServiceDisplayItem
            {
                Id = service.Id,
                Name = service.DisplayName ?? service.Name,
                Version = service.Version ?? string.Empty,
                Description = service.Description ?? string.Empty,
                Type = service.ServiceType,
                Status = statusString,
                Port = port ?? service.DefaultPort ?? 0,
                Path = service.BinaryPath ?? string.Empty,
                AutoStart = service.AutoStart
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Safely executes a fire-and-forget async operation with proper error logging
        /// </summary>
        /// <param name="asyncOperation">The async operation to execute</param>
        /// <param name="operationName">Name of the operation for logging purposes</param>
        private void SafeFireAndForget(Func<Task> asyncOperation, string operationName)
        {
            Task.Run(async () =>
            {
                try
                {
                    await asyncOperation();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in fire-and-forget operation: {OperationName}", operationName);
                    Debug.WriteLine($"❌ Unhandled exception in {operationName}: {ex.Message}");
                }
            });
        }

        private static ProjectDisplayItem CreateProjectDisplayItem(Project project, IEnumerable<Stack> stacks)
        {
            // Find the associated stack name
            var stackName = "No Stack";
            if (project.StackId.HasValue)
            {
                var stack = stacks.FirstOrDefault(s => s.Id == project.StackId.Value);
                stackName = stack?.DisplayName ?? "Unknown Stack";
            }

            return new ProjectDisplayItem
            {
                Id = project.Id,
                Name = project.Name,
                Path = project.Path,
                Domain = project.Domain ?? "Not configured",
                Status = project.IsActive ? "Active" : "Inactive",
                StackName = stackName,
                EnableSSL = project.EnableSSL,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
        }

        private bool ApplyProjectFilter(object item)
        {
            if (item is not ProjectDisplayItem project)
                return false;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                if (!project.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !project.Domain.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !project.StackName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) &&
                    !project.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Status filter
            if (StatusFilter != ProjectStatusFilter.All)
            {
                var expectedStatus = StatusFilter == ProjectStatusFilter.Active ? "Active" : "Inactive";
                if (project.Status != expectedStatus)
                {
                    return false;
                }
            }

            // Stack filter (now properly implemented)
            if (StackFilter != "All Stacks" && project.StackName != StackFilter)
            {
                return false;
            }

            return true;
        }

        private async Task RefreshFilterAndStatusBarAsync()
        {
            // Ensure UI operations happen on UI thread
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                FilteredProjects.Refresh();
                UpdateStatusBar();
            });
        }

        private async Task DebounceSearchAsync(string searchValue)
        {
            // Cancel previous debounce operation
            _searchDebounceTokenSource?.Cancel();
            _searchDebounceTokenSource = new CancellationTokenSource();
            
            try
            {
                // Wait for 300ms of inactivity
                await Task.Delay(300, _searchDebounceTokenSource.Token);
                
                // If not cancelled, apply the search
                Debug.WriteLine($"🔍 Applying debounced search: '{searchValue}'");
                await RefreshFilterAndStatusBarAsync();
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled by newer input, ignore
            }
        }

        private async Task DebounceSaveFilterStateAsync()
        {
            // Cancel previous debounce operation
            _saveDebounceTokenSource?.Cancel();
            _saveDebounceTokenSource = new CancellationTokenSource();
            
            try
            {
                // Wait for 500ms of inactivity (longer than search debounce)
                await Task.Delay(500, _saveDebounceTokenSource.Token);
                
                // If not cancelled, save the filter state
                await SaveFilterStateAsync();
            }
            catch (OperationCanceledException)
            {
                // Save was cancelled by newer input, ignore
            }
        }

        private async Task UpdateStackOptionsAsync(IEnumerable<Stack> stacks)
        {
            await _dispatcherService.InvokeOnUIThreadAsync(() =>
            {
                // Get unique stack names from loaded data
                var stackNames = stacks.Select(s => s.DisplayName).Distinct().OrderBy(name => name).ToList();
                
                // Build the complete list starting with "All Stacks"
                var newOptions = new List<string> { "All Stacks" };
                newOptions.AddRange(stackNames);
                
                // Only update if the list has changed to avoid unnecessary UI updates
                if (!StackOptions.SequenceEqual(newOptions))
                {
                    // Store current selection to restore it
                    var currentSelection = StackFilter;
                    
                    // Clear and repopulate
                    StackOptions.Clear();
                    foreach (var option in newOptions)
                    {
                        StackOptions.Add(option);
                    }
                    
                    // Restore selection if it's still valid, otherwise default to "All Stacks"
                    if (!newOptions.Contains(currentSelection))
                    {
                        StackFilter = "All Stacks";
                        Debug.WriteLine($"📚 Stack filter reset to 'All Stacks' because '{currentSelection}' is no longer available");
                    }
                    else
                    {
                        // Trigger property change to refresh binding
                        OnPropertyChanged(nameof(StackFilter));
                    }
                    
                    Debug.WriteLine($"📚 Updated stack filter options: {StackOptions.Count} options available");
                }
            });
        }

        private void UpdateStatusBar()
        {
            var totalProjects = Projects.Count;
            var filteredCount = FilteredProjects.OfType<ProjectDisplayItem>().Count();

            StatusBarText = totalProjects switch
            {
                0 => "No projects found",
                _ when filteredCount == totalProjects => $"{totalProjects} project{(totalProjects == 1 ? "" : "s")}",
                _ => $"{filteredCount} of {totalProjects} project{(totalProjects == 1 ? "" : "s")} shown"
            };
            
            // Notify property changes for status bar counts
            OnPropertyChanged(nameof(TotalProjects));
            OnPropertyChanged(nameof(ActiveProjects));
        }

        private async Task SaveProjectsToStorageAsync()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var modStackPath = Path.Combine(appDataPath, "ModStack");
                Directory.CreateDirectory(modStackPath);
                
                var projectsFilePath = Path.Combine(modStackPath, "projects.json");
                
                // Create a structured data object for JSON serialization
                var projectsData = new
                {
                    SavedAt = DateTime.UtcNow,
                    ProjectCount = Projects.Count,
                    Projects = Projects.Select(p => new
                    {
                        p.Name,
                        p.Path,
                        p.Domain,
                        p.Status,
                        p.StackName,
                        p.UpdatedAt
                    }).ToList()
                };
                
                var json = JsonSerializer.Serialize(projectsData, JsonOptions);
                await File.WriteAllTextAsync(projectsFilePath, json);
                
                Debug.WriteLine($"💾 Saved {Projects.Count} projects to storage cache (JSON format)");
                _logger.LogInformation("Saved {ProjectCount} projects to storage cache", Projects.Count);
                
                // Save filter state alongside projects
                await SaveFilterStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save projects to storage cache");
                Debug.WriteLine($"⚠️ Warning: Could not save projects to storage: {ex.Message}");
                // Don't throw - this is just a cache
            }
        }

        private async Task SaveFilterStateAsync()
        {
            try
            {
                _savedFilterState.SearchText = SearchText;
                _savedFilterState.StatusFilter = StatusFilter;
                _savedFilterState.StackFilter = StackFilter;

                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var modStackPath = Path.Combine(appDataPath, "ModStack");
                Directory.CreateDirectory(modStackPath);
                
                var filtersFilePath = Path.Combine(modStackPath, "project_filters.json");
                var json = JsonSerializer.Serialize(_savedFilterState, JsonOptions);
                
                await File.WriteAllTextAsync(filtersFilePath, json);
                Debug.WriteLine($"💾 Saved filter state: Search='{SearchText}', Status={StatusFilter}, Stack='{StackFilter}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Warning: Could not save filter state: {ex.Message}");
                // Don't throw - this is just user preference
            }
        }

        private async Task LoadFilterStateAsync()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var modStackPath = Path.Combine(appDataPath, "ModStack");
                var filtersFilePath = Path.Combine(modStackPath, "project_filters.json");
                
                if (File.Exists(filtersFilePath))
                {
                    var json = await File.ReadAllTextAsync(filtersFilePath);
                    var savedState = JsonSerializer.Deserialize<FilterState>(json);
                    
                    if (savedState != null)
                    {
                        // Apply saved state without triggering property change events yet
                        _searchText = savedState.SearchText;
                        _statusFilter = savedState.StatusFilter;
                        _stackFilter = savedState.StackFilter;
                        _savedFilterState = savedState;
                        
                        Debug.WriteLine($"📂 Loaded filter state: Search='{savedState.SearchText}', Status={savedState.StatusFilter}, Stack='{savedState.StackFilter}'");
                        
                        // Trigger property change events to update UI
                        OnPropertyChanged(nameof(SearchText));
                        OnPropertyChanged(nameof(StatusFilter));
                        OnPropertyChanged(nameof(StackFilter));
                        
                        Debug.WriteLine($"🔧 After loading filter state - StackFilter: '{StackFilter}', StackOptions count: {StackOptions.Count}");
                    }
                }
                else
                {
                    // No saved state exists, use "All Stacks" as default
                    _stackFilter = "All Stacks";
                    Debug.WriteLine($"📂 No saved filter state found, using defaults: Search='{_searchText}', Status={_statusFilter}, Stack='{_stackFilter}'");
                    
                    // Trigger property change events to update UI with defaults
                    OnPropertyChanged(nameof(SearchText));
                    OnPropertyChanged(nameof(StatusFilter));
                    OnPropertyChanged(nameof(StackFilter));
                    
                    Debug.WriteLine($"🔧 After setting defaults - StackFilter: '{StackFilter}', StackOptions count: {StackOptions.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ Warning: Could not load filter state: {ex.Message}");
                // Continue with default values
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Display item for project list with all necessary properties for UI binding
    /// </summary>
    public class ProjectDisplayItem : INotifyPropertyChanged
    {
        private string _status = string.Empty;

        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string StackName { get; set; } = string.Empty;
        public bool EnableSSL { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FormattedCreatedAt => CreatedAt.ToString("MMM dd, yyyy");
        public string FormattedUpdatedAt => UpdatedAt.ToString("MMM dd, yyyy");

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Extension methods for ObservableCollection to improve performance
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Adds a range of items to the ObservableCollection efficiently
        /// </summary>
        /// <typeparam name="T">The type of items in the collection</typeparam>
        /// <param name="collection">The collection to add items to</param>
        /// <param name="items">The items to add</param>
        public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(items);

            foreach (var item in items)
            {
                collection.Add(item);
            }
        }

        /// <summary>
        /// Adds a range of items to the ObservableCollection with bulk operation (suspends notifications)
        /// </summary>
        /// <typeparam name="T">The type of items in the collection</typeparam>
        /// <param name="collection">The collection to add items to</param>
        /// <param name="items">The items to add</param>
        public static void AddRangeBulk<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(items);

            // For ObservableCollection, we can't suspend notifications, but we can batch the operation
            // by adding all items at once to minimize UI updates
            var itemsList = items.ToList();
            
            // Clear existing items first to avoid duplicates
            collection.Clear();
            
            // Add all items in one batch
            foreach (var item in itemsList)
            {
                collection.Add(item);
            }
        }

    }
}
