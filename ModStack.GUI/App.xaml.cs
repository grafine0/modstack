using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using ModStack.GUI.Services;
using ModStack.GUI.Services.Interfaces;
using ModStack.GUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModStack.Data.Interfaces;
using ModStack.Data.Repositories;
using ModStack.Services;
using ModStack.Services.Interfaces;
using ModStack.Core.Interfaces;
using ModStack.Core.Services;
using ModStack.Common.Services;
using ModStack.Common.Models;
using ModStack.Common;
using ModStack.Common.Interfaces;
using System.Runtime.InteropServices;
using LogService = ModStack.GUI.Services.LogService;

namespace ModStack.GUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = "ModStack_SingleInstance_Mutex";
    private const string PipeName = "ModStack_SingleInstance_Pipe";
    private static Mutex? _mutex;
    private static CancellationTokenSource? _pipeServerCts;

    // Win32 API for bringing window to foreground
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);
    
    private const int SW_RESTORE = 9;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Check for existing instance using a named mutex
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);
        
        if (!createdNew)
        {
            // Another instance is already running - signal it to come to foreground
            SignalExistingInstance();
            Current.Shutdown();
            return;
        }
        
        // Start the named pipe server to listen for signals from other instances
        StartPipeServer();
        
        // Set up global exception handling
        SetupGlobalExceptionHandling();
        
        Views.SplashWindow? splashWindow = null;
        
        try
        {
            // Wrap the entire startup process in a proper async method
            await OnStartupAsync(e);
        }
        catch (Exception ex)
        {
            // This catch block handles any exceptions from async void
            LogService.Error($"Critical startup error: {ex.Message}", ex);
            splashWindow?.CloseSplash();
            
            MessageBox.Show(
                $"Application failed to start:\n{ex.Message}\n\nPlease check the logs for more details.",
                "Critical Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Current.Shutdown();
        }
    }
    
    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseSingleInstanceResources();
        base.OnExit(e);
    }

    private static void ReleaseSingleInstanceResources()
    {
        try
        {
            _pipeServerCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation errors during shutdown
        }

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned by current thread or already released
            }
            finally
            {
                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
    
    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000); // Wait up to 1 second to connect
            
            using var writer = new StreamWriter(client);
            writer.WriteLine("SHOW");
            writer.Flush();
        }
        catch (Exception ex)
        {
            // If we can't connect, the existing instance may have crashed
            // Log the error but don't show to user since we're exiting anyway
            System.Diagnostics.Debug.WriteLine($"Failed to signal existing instance: {ex.Message}");
        }
    }
    
    private void StartPipeServer()
    {
        _pipeServerCts = new CancellationTokenSource();
        var token = _pipeServerCts.Token;
        
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(token);
                    
                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync();
                    
                    if (message == "SHOW")
                    {
                        // Bring the main window to foreground on the UI thread
                        await Dispatcher.InvokeAsync(() =>
                        {
                            BringMainWindowToForeground();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe server error: {ex.Message}");
                }
            }
        }, token);
    }
    

    private void BringMainWindowToForeground()
    {
        var mainWindow = MainWindow;
        if (mainWindow == null) return;
        
        var handle = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
        
        // If the window is minimized, restore it
        if (mainWindow.WindowState == WindowState.Minimized || IsIconic(handle))
        {
            ShowWindow(handle, SW_RESTORE);
            mainWindow.WindowState = WindowState.Normal;
        }
        
        // Bring window to foreground
        mainWindow.Activate();
        mainWindow.Topmost = true;  // Temporarily make topmost
        mainWindow.Topmost = false; // Remove topmost
        mainWindow.Focus();
        SetForegroundWindow(handle);
    }
    
    private static async Task OnStartupAsync(StartupEventArgs _)
    {
        Views.SplashWindow? splashWindow = null;
        
        try
        {
            // Initialize theme FIRST before showing splash screen
            InitializeThemeAsync();
            
            // Initialize logging system with settings on background thread
            await Task.Run(() => InitializeLogging());
            
            // Show splash screen with correct theme
            splashWindow = new Views.SplashWindow();
            splashWindow.Show();
            
            // Give splash screen time to initialize animations
            await Task.Delay(200);
            
            // Initialize logging system
            LogService.Info("ModStack application starting...");
            splashWindow.UpdateLoadingText("Initializing logging system...");
            splashWindow.SetProgress(20);
            await Task.Delay(200);
            
            // Update loading text
            splashWindow.UpdateLoadingText("Setting up user interface...");
            splashWindow.SetProgress(40);
            await Task.Delay(200);
            
            // Check permission context on background thread without blocking startup
            splashWindow.UpdateLoadingText("Checking permissions...");
            splashWindow.SetProgress(60);
            await Task.Delay(200);
            var isAdmin = await Task.Run(() => IsRunningAsAdministrator());
            if (!isAdmin)
            {
                LogService.Info("Application is running with standard user privileges (asInvoker)");
            }
            else
            {
                LogService.Info("Application is running as administrator");
            }
            
            // Initialize application directories on background thread
            splashWindow.UpdateLoadingText("Creating application directories...");
            splashWindow.SetProgress(70);
            await InitializeApplicationDirectoriesAsync();
            await Task.Delay(200);
            
            // Set up dependency injection
            splashWindow.UpdateLoadingText("Initializing database...");
            splashWindow.SetProgress(90);
            var serviceProvider = await SetupDependencyInjectionAsync();
            await Task.Delay(200);
            
            // Final loading step
            splashWindow.UpdateLoadingText("Loading application...");
            splashWindow.SetProgress(100);
            await Task.Delay(200); // Reduced from 1000ms to 600ms
            
            // Close splash screen and show main window
            
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            splashWindow.CloseSplash();
            
            // Show main window using DI container directly (no ServiceLocator)
            Current.MainWindow = mainWindow; // Set as the main window
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            LogService.Error($"Application startup failed: {ex.Message}");
            splashWindow?.CloseSplash();
            
            MessageBox.Show(
                $"Application failed to start:\n{ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Current.Shutdown();
        }
    }
    
    private static void SetupGlobalExceptionHandling()
    {
        // Handle unhandled exceptions from any thread
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                LogService.Error($"Unhandled exception: {exception.Message}", exception);
            }
            else
            {
                LogService.Error($"Unhandled exception: {e.ExceptionObject?.ToString() ?? "Unknown error"}", new Exception("Unknown exception type"));
            }
            
            // Show error dialog on UI thread if possible
            if (Current?.Dispatcher != null)
            {
                Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(
                        $"An unexpected error occurred:\n{exception?.Message}\n\nThe application will now close.",
                        "Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }));
            }
        };
        
        // Handle unhandled exceptions from the UI thread
        Current.DispatcherUnhandledException += (sender, e) =>
        {
            LogService.Error($"UI thread unhandled exception: {e.Exception.Message}", e.Exception);
            
            MessageBox.Show(
                $"An error occurred in the user interface:\n{e.Exception.Message}\n\nThe application will continue running.",
                "UI Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            
            // Mark as handled to prevent app crash
            e.Handled = true;
        };
        
        // Handle unhandled exceptions from background tasks
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogService.Error($"Unobserved task exception: {e.Exception.Message}", e.Exception);
            
            // Mark as observed to prevent app crash
            e.SetObserved();
        };
    }
    
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryRestartAsAdministrator()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            ReleaseSingleInstanceResources();

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(startInfo);
            Current?.Shutdown();
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to restart as administrator: {ex.Message}");
            return false;
        }
    }
    
    private static async Task InitializeApplicationDirectoriesAsync()
    {
        try
        {
            // Use AppData for initial directory creation (before DI is set up)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var modStackPath = Path.Combine(appDataPath, "ModStack");

            LogService.Info($"Initializing application directories in: {modStackPath}");

            // Create directories if they don't exist
            var directories = new[]
            {
                modStackPath,
                Path.Combine(modStackPath, "Data"),
                Path.Combine(modStackPath, "Services"),
                Path.Combine(modStackPath, "Logs"),
                Path.Combine(modStackPath, "Config"),
                Path.Combine(modStackPath, "Temp")
            };

            // Run directory creation tasks in parallel without blocking the UI thread
            var createDirectoryTasks = directories.Select(directory => Task.Run(() =>
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory); // Create directory in background
                    LogService.Info($"Created directory: {directory}");
                }
                else
                {
                    LogService.Info($"Directory already exists: {directory}");
                }
            })).ToArray();

            // Wait for all directory creation tasks to complete
            await Task.WhenAll(createDirectoryTasks);

            LogService.Info("Application directories initialized successfully");
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to initialize application directories: {ex.Message}", ex);

            var errorDetails = $"Failed to create application directories.\n\n" +
                            $"Error: {ex.Message}\n\n" +
                            $"This usually indicates:\n" +
                            $"• Insufficient file system permissions\n" +
                            $"• Disk space issues\n" +
                            $"• Antivirus software blocking directory creation\n\n" +
                            $"Please ensure the application has write access to:\n" +
                            $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}";

            // Show the error message in the UI thread (MessageBox should be called on UI thread)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    errorDetails,
                    "Directory Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    private static async Task<IServiceProvider> SetupDependencyInjectionAsync()
    {
        try
        {
            LogService.Info("Setting up dependency injection...");

            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
            });

            // Add database connection string
            var connectionString = GetDatabaseConnectionString();
            services.AddSingleton(provider => connectionString);

            // Entity Framework DbContext removed - repositories now use Dapper directly

            // Add repositories
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IStackRepository, StackRepository>();
            
            // Register both IServiceRepository interfaces to resolve namespace conflict
            services.AddScoped<IServiceRepository, ServiceRepository>();
            
            services.AddScoped<IStackServiceRepository, StackServiceRepository>();
            services.AddScoped<IProjectServiceRepository, ProjectServiceRepository>();
            services.AddScoped<IVirtualHostRepository, VirtualHostRepository>();
            services.AddScoped<IPortAllocationRepository, PortAllocationRepository>();
            services.AddScoped<IServiceLogRepository, ServiceLogRepository>();
            services.AddScoped<ISettingsRepository, SettingsRepository>();
            services.AddScoped<IPackageRepository, PackageRepository>();
            services.AddScoped<IMailCatcherRepository, MailCatcherRepository>();

            // Add services
            services.AddScoped<IStackManager, StackManager>();
            services.AddScoped<IProjectManager, ProjectManager>();
            services.AddScoped<ProjectManager>();
            services.AddScoped<WindowsServiceManager>();
            services.AddScoped<IServiceManager, WindowsServiceManager>();
            services.AddScoped<IServiceService, ServiceService>();
            services.AddScoped<VirtualHostManager>();
            services.AddScoped<StackServiceManager>();
            services.AddScoped<ConfigurationManager>();
            services.AddScoped<PackageManagerService>();
            services.AddScoped<PackageInstaller>();
            services.AddScoped<PackageUninstaller>();
            services.AddScoped<FrameworkPackageInstaller>();
            services.AddScoped<PHPExtensionManager>();
            services.AddScoped<IPHPExtensionRepository, PHPExtensionRepository>();
            services.AddScoped<ApacheModuleManager>();
            services.AddScoped<IApacheModuleRepository, ApacheModuleRepository>();
            services.AddScoped<ModStack.Services.Interfaces.IProcessRunner, ModStack.Services.Interfaces.SystemProcessRunner>();
            services.AddScoped<ModStack.Services.Interfaces.IServiceSafetyManager, ModStack.Services.ServiceSafetyManager>();
            services.AddHttpClient(); // Required for PHPExtensionManager
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddScoped<IPathConfigurationService, PathConfigurationService>();
            services.AddScoped<IServiceConfigurationService, ServiceConfigurationService>();
            services.AddScoped<IDatabaseManager, DatabaseManager>();
            services.AddSingleton<ModStack.Services.Interfaces.IMailCatcherService, ModStack.Services.MailCatcherService>();
            
            // Add new refactored services
            services.AddScoped<HostsFileManager>();
            services.AddScoped<ProjectFormHandler>();
            services.AddScoped<ISSLCertificateService, SSLCertificateService>();
            services.AddScoped<IProjectOverlayService, ProjectOverlayService>();
            services.AddScoped<IServiceInstallationChecker, ServiceInstallationChecker>();
            services.AddScoped<ServiceManagementHelper>();
            services.AddScoped<ApacheHttpdConfigurator>();
            services.AddScoped<IVirtualHostService, VirtualHostService>();
            services.AddScoped<IProjectCreationService, ProjectCreationService>();
            services.AddScoped<IViewFactoryService, ViewFactoryService>();
            services.AddScoped<ModStack.Services.Interfaces.IVirtualHostManager, ModStack.Services.VirtualHostManager>();
            services.AddScoped<ServiceControlManager>();
            
            // Add abstraction services
            services.AddScoped<ModStack.GUI.Services.Interfaces.IDispatcherService, WpfDispatcherService>();
            services.AddScoped<ModStack.GUI.Services.Interfaces.IDialogService, WpfDialogService>();
            services.AddScoped<ModStack.GUI.Services.Interfaces.INavigationService, WpfNavigationService>();
            services.AddSingleton<ModStack.GUI.Services.Interfaces.IThemeService, ModStack.GUI.Services.ThemeService>();
            services.AddScoped<ModStack.Core.Interfaces.IUpdateService, ModStack.Core.Services.UpdateService>();
            
            // Add localization service
            services.AddSingleton<ModStack.GUI.Services.Interfaces.ILocalizationService, ModStack.GUI.Services.LocalizationService>();
            
            // Add configuration services
            services.AddScoped<ModStack.GUI.Services.ApacheConfigurationService>();
            
            // Add ViewModels
            services.AddScoped<ModStack.GUI.ViewModels.ProjectManagementViewModel>();
            services.AddScoped<ModStack.GUI.ViewModels.StackManagementViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.ApacheModuleManagerViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.CreateProjectViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.CreatePackageViewModel>();
            services.AddScoped<ModStack.GUI.ViewModels.SettingsViewModel>();
            services.AddScoped<ModStack.GUI.ViewModels.ServiceManagementViewModel>();
            services.AddScoped<ModStack.GUI.ViewModels.MainViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.PackageManagerViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.CreateStackViewModel>();
            services.AddScoped<ModStack.GUI.ViewModels.MailCatcherViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.AboutViewModel>();
            services.AddTransient<ModStack.GUI.ViewModels.UpdateViewModel>();

            // Add views with constructor injection
            services.AddTransient<CreateProjectView>();
            services.AddTransient<CreatePackageView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<ServiceManagementView>();
            services.AddTransient<MainWindow>();
            services.AddTransient<ModStack.GUI.Views.MailCatcherView>();
            services.AddTransient<ModStack.GUI.Views.UpdateView>();

            // Add database initialization service
            services.AddScoped<DatabaseInitializationService>();

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();
            ServiceLocator.SetServiceProvider(serviceProvider);
            
            // Initialize localization service and set it in the markup extension
            var localizationService = serviceProvider.GetRequiredService<ModStack.GUI.Services.Interfaces.ILocalizationService>();
            ModStack.GUI.Converters.LocalizeExtension.SetLocalizationService(localizationService);
            
            // Load saved language preference from settings
            var settingsService = serviceProvider.GetRequiredService<ModStack.Core.Services.ISettingsService>();
            if (!string.IsNullOrWhiteSpace(settingsService.Language))
            {
                try
                {
                    localizationService.SetCulture(settingsService.Language);
                    LogService.Info($"Loaded language preference: {settingsService.Language}");
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Failed to set language from settings: {ex.Message}");
                }
            }

            // Initialize database on background thread to avoid blocking UI
            var dbInitService = serviceProvider.GetRequiredService<DatabaseInitializationService>();
            var initResult = await Task.Run(async () => await dbInitService.InitializeDatabaseAsync()).ConfigureAwait(false);
            
            if (initResult.Success)
            {
                LogService.Info($"Database initialized successfully: {initResult.MigratedProjects} projects migrated, {initResult.SeededPackages} packages seeded");
            }
            else
            {
                LogService.Error($"Database initialization failed: {initResult.ErrorMessage}");
            }

            // Initialize PHP extensions on background thread to avoid blocking UI
            var phpExtensionRepo = serviceProvider.GetRequiredService<IPHPExtensionRepository>();
            await Task.Run(async () => await phpExtensionRepo.InitializeDefaultExtensionsAsync()).ConfigureAwait(false);

            // Start MailCatcher Service
            var mailCatcherService = serviceProvider.GetRequiredService<ModStack.Services.Interfaces.IMailCatcherService>();
            await Task.Run(async () => await mailCatcherService.StartAsync()).ConfigureAwait(false);

            // Initialize service configurations (Windows service names) on background thread
            var serviceConfigService = serviceProvider.GetRequiredService<IServiceConfigurationService>();
            await Task.Run(async () => 
            {
                try
                {
                    await serviceConfigService.ConfigureDefaultWindowsServiceNamesAsync();
                    LogService.Info("Service configuration initialization completed");
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Service configuration initialization failed: {ex.Message}");
                }
            }).ConfigureAwait(false);

            // Fix invalid virtual host directives and cleanup orphaned virtual hosts on background thread
            var virtualHostService = serviceProvider.GetRequiredService<IVirtualHostService>();
            await Task.Run(async () => 
            {
                try
                {
                    var fixedCount = await virtualHostService.FixInvalidVirtualHostDirectivesAsync();
                    if (fixedCount > 0)
                    {
                        LogService.Info($"Fixed {fixedCount} virtual host file(s) with invalid directives");
                    }
                    
                    var cleanedCount = await virtualHostService.CleanupOrphanedVirtualHostsAsync();
                    if (cleanedCount > 0)
                    {
                        LogService.Info($"Cleaned up {cleanedCount} orphaned virtual host file(s)");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"Virtual host cleanup failed: {ex.Message}");
                }
            }).ConfigureAwait(false);

            LogService.Info("Dependency injection setup completed");
            return serviceProvider;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to setup dependency injection: {ex.Message}", ex);
            
            // Provide more detailed error information for debugging
            var errorDetails = $"Dependency injection setup failed.\n\n" +
                             $"Error: {ex.Message}\n\n" +
                             $"This usually indicates a problem with:\n" +
                             $"• Database connection or initialization\n" +
                             $"• Service registration conflicts\n" +
                             $"• Missing dependencies or assemblies\n\n" +
                             $"Please check the application logs for more details.";
            
            MessageBox.Show(
                errorDetails,
                "Application Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Current.Shutdown();
            throw; // Re-throw to be caught by the calling method
        }
    }

    private static string GetDatabaseConnectionString()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appDataPath, "ModStack", "Data", "modstack.db");
            
            LogService.Info($"Database path: {dbPath}");
            
            // Ensure directory exists
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                LogService.Info($"Created database directory: {dbDirectory}");
            }
            
            var connectionString = $"Data Source={dbPath};";
            LogService.Info("Database connection string generated successfully");
            
            return connectionString;
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to generate database connection string: {ex.Message}", ex);
            throw new InvalidOperationException("Failed to initialize database connection", ex);
        }
    }
    
    private static void InitializeThemeAsync()
    {
        try
        {
            LogService.Info("Initializing application theme...");
            
            // Load theme from registry
            string theme = "Light"; // Default theme
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ModStack");
            if (key != null)
            {
                theme = key.GetValue("Theme", "Light").ToString() ?? "Light";
            }
            
            // Apply theme using ThemeManager
            if (theme == "Dark")
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
                ApplyDarkTheme();
                LogService.Info("Dark theme initialized from settings");
            }
            else
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
                ApplyLightTheme();
                LogService.Info("Light theme initialized from settings");
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to initialize theme: {ex}");
            // Fallback to light theme
            ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
            ApplyLightTheme();
        }
    }
    
    // Old UpdateThemeResources method removed - now using separate theme files
    
    public static void ApplyTheme(Uri themeUri)
    {
        try
        {
            LogService.Info($"Applying theme: {themeUri}");
            
            var app = Current;
            if (app?.Resources == null) 
            {
                LogService.Error("Application resources are null");
                return;
            }
            
            // Load the new theme dictionary
            var themeDict = new ResourceDictionary { Source = themeUri };
            LogService.Info($"Created theme dictionary with source: {themeDict.Source}");
            
            // Find and remove existing theme dictionaries
            var themeDictionariesToRemove = new List<ResourceDictionary>();
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                if (dict.Source != null && 
                    (dict.Source.ToString().Contains("LightTheme.xaml") || 
                     dict.Source.ToString().Contains("DarkTheme.xaml") ||
                     dict.Source.ToString().Contains("Themes/LightTheme.xaml") ||
                     dict.Source.ToString().Contains("Themes/DarkTheme.xaml")))
                {
                    LogService.Info($"Found existing theme dictionary to remove: {dict.Source}");
                    themeDictionariesToRemove.Add(dict);
                }
            }
            
            // Remove existing theme dictionaries
            foreach (var dict in themeDictionariesToRemove)
            {
                app.Resources.MergedDictionaries.Remove(dict);
                LogService.Info($"Removed theme dictionary: {dict.Source}");
            }
            
            // Apply new theme
            app.Resources.MergedDictionaries.Add(themeDict);
            LogService.Info($"Added new theme dictionary: {themeDict.Source}");
            
            LogService.Info($"Theme applied successfully: {themeUri}");
        }
        catch (Exception ex)
        {
            LogService.Error($"Failed to apply theme: {ex}");
        }
    }
    
    public static void ApplyLightTheme()
    {
        ApplyTheme(new Uri("pack://application:,,,/Themes/LightTheme.xaml"));
    }
    
    public static void ApplyDarkTheme()
    {
        ApplyTheme(new Uri("pack://application:,,,/Themes/DarkTheme.xaml"));
    }
    
    private static void InitializeLogging()
    {
        try
        {
            // Load EnableLogging setting from registry
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ModStack");
            if (key != null)
            {
                var enableLogging = Convert.ToBoolean(key.GetValue("EnableLogging", true));
                LogService.SetLoggingEnabled(enableLogging);
            }
            else
            {
                // Default to enabled if no settings exist
                LogService.SetLoggingEnabled(true);
            }
        }
        catch (Exception ex)
        {
            // If there's an error loading settings, default to enabled
            LogService.SetLoggingEnabled(true);
            System.Diagnostics.Debug.WriteLine($"Failed to load logging setting: {ex}");
        }
    }
}

/// <summary>
/// Adapter class to bridge IServiceRepository to IServiceRepository
/// This resolves the namespace conflict between the two IServiceRepository interfaces
/// </summary>
public class ServiceRepositoryAdapter(ModStack.Data.Interfaces.IServiceRepository coreServiceRepository) : ModStack.Data.Interfaces.IServiceRepository
{
    private readonly ModStack.Data.Interfaces.IServiceRepository _coreServiceRepository = coreServiceRepository ?? throw new ArgumentNullException(nameof(coreServiceRepository));

    public async Task<IEnumerable<ServiceDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetAllAsync(cancellationToken);
    }

    public async Task<IEnumerable<ServiceDefinition>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetEnabledAsync(cancellationToken);
    }

    public async Task<IEnumerable<ServiceDefinition>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetInstalledAsync(cancellationToken);
    }

    public async Task<ServiceDefinition?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<ServiceDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetByNameAsync(name, cancellationToken);
    }

    public async Task<ServiceDefinition?> GetByNameAndVersionAsync(string name, string version, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetByNameAndVersionAsync(name, version, cancellationToken);
    }

    public async Task<ServiceDefinition?> GetByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.GetByTypeAsync(serviceType, cancellationToken);
    }

    public async Task<ServiceDefinition> AddAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.AddAsync(service, cancellationToken);
    }

    public void Add(ServiceDefinition service)
    {
        _coreServiceRepository.Add(service);
    }

    public async Task<ServiceDefinition> CreateAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.CreateAsync(service, cancellationToken);
    }

    public async Task<ServiceDefinition> UpdateAsync(ServiceDefinition service, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.UpdateAsync(service, cancellationToken);
    }

    public void Update(ServiceDefinition service)
    {
        _coreServiceRepository.Update(service);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<bool> MarkForDeletionAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.MarkForDeletionAsync(id, cancellationToken);
    }

    public async Task UpdateInstallationStatusAsync(long id, bool isInstalled, string? binaryPath = null, string? version = null, CancellationToken cancellationToken = default)
    {
        await _coreServiceRepository.UpdateInstallationStatusAsync(id, isInstalled, binaryPath, version, cancellationToken);
    }

    public async Task UpdateStatusAsync(long id, ServiceStatus status, CancellationToken cancellationToken = default)
    {
        await _coreServiceRepository.UpdateStatusAsync(id, status, cancellationToken);
    }

    public async Task UpdateConfigurationAsync(long id, string configurationJson, CancellationToken cancellationToken = default)
    {
        await _coreServiceRepository.UpdateConfigurationAsync(id, configurationJson, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.ExistsByNameAsync(name, cancellationToken);
    }

    public async Task<bool> ExistsByTypeAsync(ServiceType serviceType, CancellationToken cancellationToken = default)
    {
        return await _coreServiceRepository.ExistsByTypeAsync(serviceType, cancellationToken);
    }


}
