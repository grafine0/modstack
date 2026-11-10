using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using ModStack.GUI.Models;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.ViewModels;
using ModStack.GUI.Views;

namespace ModStack.GUI.Services
{
    public class WpfNavigationService : INavigationService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;

        // View Caching
        private readonly Dictionary<string, UserControl> _cachedViews = new();
        private readonly Dictionary<string, DateTime> _viewLastAccessed = new();
        private readonly object _cacheLock = new();
        private Timer? _cacheCleanupTimer;
        
        private const int CACHE_IDLE_TIMEOUT_MINUTES = 30;
        private const int CACHE_CLEANUP_INTERVAL_MINUTES = 5;

        public WpfNavigationService(IProjectRepository projectRepository, IDialogService dialogService, IServiceProvider serviceProvider)
        {
            _projectRepository = projectRepository;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;

            _cacheCleanupTimer = new Timer(CleanupExpiredCache, null, 
                TimeSpan.FromMinutes(CACHE_CLEANUP_INTERVAL_MINUTES), 
                TimeSpan.FromMinutes(CACHE_CLEANUP_INTERVAL_MINUTES));
        }

        private void Navigate(UserControl view, string viewName)
        {
            lock (_cacheLock)
            {
                _viewLastAccessed[viewName] = DateTime.Now;
            }
            
            // Send navigation message to MainViewModel
            WeakReferenceMessenger.Default.Send(new NavigationMessage(view));
        }

        private T GetOrCreateView<T>() where T : UserControl, new()
        {
            var viewName = typeof(T).Name;
            lock (_cacheLock)
            {
                if (!_cachedViews.TryGetValue(viewName, out var view))
                {
                    view = new T();
                    _cachedViews[viewName] = view;
                }
                return (T)view;
            }
        }
        
        private T GetOrCreateViewWithDI<T>() where T : UserControl
        {
            var viewName = typeof(T).Name;
            lock (_cacheLock)
            {
                if (!_cachedViews.TryGetValue(viewName, out var view))
                {
                    try 
                    {
                        view = _serviceProvider.GetRequiredService<T>();
                    }
                    catch (InvalidOperationException)
                    {
                        view = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<T>(_serviceProvider);
                    }
                    _cachedViews[viewName] = view;
                }
                return (T)view;
            }
        }

        public void NavigateToDashboard()
        {
            Navigate(GetOrCreateViewWithDI<DashboardView>(), "DashboardView");
        }

        public void NavigateToMailCatcher()
        {
            Navigate(GetOrCreateViewWithDI<MailCatcherView>(), "MailCatcherView");
        }

        public void NavigateToPackageManager()
        {
            Navigate(GetOrCreateViewWithDI<PackageManagerView>(), "PackageManagerView");
        }

        public void NavigateToCreatePackage()
        {
            var viewModel = _serviceProvider.GetRequiredService<CreatePackageViewModel>();
            Navigate(new CreatePackageView(viewModel), "CreatePackageView");
        }

        public void NavigateToServiceManagement()
        {
            Navigate(GetOrCreateViewWithDI<ServiceManagementView>(), "ServiceManagementView");
        }

        public void NavigateToServiceLogs()
        {
            Navigate(new ServiceLogsView(), "ServiceLogsView");
        }

        public void NavigateToStackManagement()
        {
            Navigate(GetOrCreateViewWithDI<StackManagementView>(), "StackManagementView");
        }

        public void NavigateToCreateStack()
        {
            Navigate(GetOrCreateViewWithDI<CreateStackView>(), "CreateStackView");
        }

        public void NavigateToEditStack(Stack stack)
        {
            var viewModel = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<CreateStackViewModel>(_serviceProvider, stack);
            var view = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<CreateStackView>(_serviceProvider, viewModel);
            Navigate(view, "CreateStackView");
        }

        public void NavigateToProjectManagement()
        {
            Navigate(GetOrCreateViewWithDI<ProjectManagementView>(), "ProjectManagementView");
        }

        public void NavigateToCreateProject()
        {
            var viewModel = _serviceProvider.GetRequiredService<CreateProjectViewModel>();
            Navigate(new CreateProjectView(viewModel), "CreateProjectView");
        }

        public async Task NavigateToEditProjectAsync(long projectId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                {
                    await _dialogService.ShowInformationAsync($"Project with ID {projectId} not found in database.", "Project Not Found");
                    return;
                }

                var viewModel = _serviceProvider.GetRequiredService<CreateProjectViewModel>();
                _ = viewModel.InitializeForEditAsync(project);
                var view = ActivatorUtilities.CreateInstance<CreateProjectView>(_serviceProvider, viewModel);
                Navigate(view, "CreateProjectView");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to edit project: {ex.Message}");
                _ = _dialogService.ShowErrorAsync($"Navigation error: {ex.Message}", "Error");
            }
        }

        public void NavigateToConfigEditor()
        {
            Navigate(new ConfigEditorView(), "ConfigEditorView");
        }

        public void NavigateToHostsEditor()
        {
            Navigate(new HostsEditorView(), "HostsEditorView");
        }

        public void NavigateToSettings()
        {
            Navigate(GetOrCreateViewWithDI<SettingsView>(), "SettingsView");
        }

        public void NavigateToAbout()
        {
            Navigate(new AboutView(), "AboutView");
        }

        public void NavigateToUpdates()
        {
            Navigate(GetOrCreateViewWithDI<UpdateView>(), "UpdateView");
        }

        public void NavigateToExtensions()
        {
            Navigate(GetOrCreateViewWithDI<ExtensionsView>(), "ExtensionsView");
        }

        public bool NavigateBack()
        {
            // Simple implementation since Frame is gone. Advanced back-stack would go here.
            return false;
        }

        private void CleanupExpiredCache(object? state)
        {
            lock (_cacheLock)
            {
                var currentTime = DateTime.Now;
                var viewsToRemove = new List<string>();

                foreach (var kvp in _viewLastAccessed)
                {
                    if ((currentTime - kvp.Value).TotalMinutes > CACHE_IDLE_TIMEOUT_MINUTES)
                    {
                        viewsToRemove.Add(kvp.Key);
                    }
                }

                foreach (var viewName in viewsToRemove)
                {
                    if (_cachedViews.TryGetValue(viewName, out var view))
                    {
                        if (view is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        _cachedViews.Remove(viewName);
                    }
                    _viewLastAccessed.Remove(viewName);
                    Debug.WriteLine($"Cleaned up cached view: {viewName}");
                }
            }
        }
    }
}
