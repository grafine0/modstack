using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using ModStack.Common.Models;
using ModStack.GUI.Services;
using ModStack.Common;

namespace ModStack.GUI.ViewModels;

/// <summary>
/// ViewModel for service configuration dialog
/// </summary>
public class ServiceConfigurationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private ServiceDisplayItem _service;
    private ServiceConfiguration _configuration;
    private string _configurationJson = string.Empty;

    public ServiceDisplayItem Service
    {
        get => _service;
        set
        {
            _service = value;
            OnPropertyChanged(nameof(Service));
            UpdateConfigurationFromService();
        }
    }

    public ServiceConfiguration Configuration
    {
        get => _configuration;
        set
        {
            if (_configuration != null)
            {
                _configuration.PropertyChanged -= Configuration_PropertyChanged;
            }
            
            _configuration = value;
            
            if (_configuration != null)
            {
                _configuration.PropertyChanged += Configuration_PropertyChanged;
            }
            
            OnPropertyChanged(nameof(Configuration));
            UpdateConfigurationJson();
        }
    }

    public string ConfigurationJson
    {
        get => _configurationJson;
        set
        {
            _configurationJson = value;
            OnPropertyChanged(nameof(ConfigurationJson));
        }
    }

    public ServiceConfigurationViewModel(ServiceDisplayItem service)
    {
        _service = service;
        _configuration = ServiceConfiguration.FromServiceDisplayItem(service);
        _configuration.PropertyChanged += Configuration_PropertyChanged;
        UpdateConfigurationJson();
    }

    /// <summary>
    /// Handles property changes in the Configuration object
    /// </summary>
    private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update the JSON when configuration properties change
        UpdateConfigurationJson();
        
        // Apply changes back to the service
        Configuration.ApplyToServiceDisplayItem(Service);
    }

    /// <summary>
    /// Updates the configuration from the current service
    /// </summary>
    private void UpdateConfigurationFromService()
    {
        Configuration = ServiceConfiguration.FromServiceDisplayItem(Service);
    }

    /// <summary>
    /// Updates the JSON representation from the current configuration
    /// </summary>
    private void UpdateConfigurationJson()
    {
        try
        {
            ConfigurationJson = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            ConfigurationJson = "{}";
            Debug.WriteLine($"❌ Error serializing configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates and applies the current JSON configuration
    /// </summary>
    public bool TryApplyJsonConfiguration(out List<string> errors)
    {
        errors = new List<string>();
        
        try
        {
            // Parse JSON to configuration object
            var parsedConfig = JsonSerializer.Deserialize<ServiceConfiguration>(ConfigurationJson);
            if (parsedConfig == null)
            {
                errors.Add("Invalid JSON configuration");
                return false;
            }

            // Validate the configuration
            var validationErrors = parsedConfig.Validate();
            if (validationErrors.Count > 0)
            {
                errors.AddRange(validationErrors);
                return false;
            }

            // Note: Port conflict validation removed to allow users to configure 
            // same port for services they plan to run alternately

            // Apply the configuration
            Configuration = parsedConfig;
            Configuration.ApplyToServiceDisplayItem(Service);
            
            Debug.WriteLine($"✅ Configuration applied successfully for {Service.Name}");
            Debug.WriteLine($"   Port: {Configuration.Port}");
            Debug.WriteLine($"   AutoStart: {Configuration.AutoStart}");
            Debug.WriteLine($"   Path: {Configuration.Path}");
            Debug.WriteLine($"   Version: {Configuration.Version}");
            
            return true;
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON format: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            errors.Add($"Error parsing configuration: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Opens the service configuration file or directory
    /// </summary>
    public void OpenConfigurationFiles()
    {
        try
        {
            var configPath = GetServiceConfigPath();
            if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
            {
                Debug.WriteLine($"📁 Opening config file: {configPath}");
                // Open the configuration file with the default editor
                Process.Start(new ProcessStartInfo
                {
                    FileName = configPath,
                    UseShellExecute = true
                });
            }
            else
            {
                // Show the service directory if config file doesn't exist
                var serviceDir = GetServiceDirectory();
                if (!string.IsNullOrEmpty(serviceDir) && Directory.Exists(serviceDir))
                {
                    Debug.WriteLine($"📁 Opening service directory: {serviceDir}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = serviceDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    ModernDialogService.ShowWarning($"Configuration directory not found for {Service.Name}.", "Configuration Files");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Error opening configuration files: {ex.Message}");
            ModernDialogService.ShowError($"Error opening configuration files: {ex.Message}", "Configuration Error");
        }
    }

    /// <summary>
    /// Gets the expected configuration file path for the service
    /// </summary>
    private string GetServiceConfigPath()
    {
        var serviceDir = GetServiceDirectory();
        if (string.IsNullOrEmpty(serviceDir))
            return string.Empty;

        return Service.Type switch
        {
            ServiceType.Apache => GetApacheConfigPath(serviceDir),
            ServiceType.Nginx => GetNginxConfigPath(serviceDir),
            ServiceType.PHP => GetPhpConfigPath(serviceDir),
            ServiceType.MySQL => GetMySqlConfigPath(serviceDir),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Gets the service directory, handling various path formats
    /// </summary>
    private string GetServiceDirectory()
    {
        if (string.IsNullOrEmpty(Service.Path))
            return string.Empty;

        // If Service.Path is a file, get its directory
        if (File.Exists(Service.Path))
            return Path.GetDirectoryName(Service.Path) ?? string.Empty;

        // If Service.Path is already a directory, use it
        if (Directory.Exists(Service.Path))
            return Service.Path;

        // Try parent directory if current doesn't exist
        var parentDir = Path.GetDirectoryName(Service.Path);
        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
            return parentDir;

        return string.Empty;
    }

    private string GetApacheConfigPath(string serviceDir)
    {
        // Try multiple common Apache config locations
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "conf", "httpd.conf"),
            Path.Combine(serviceDir, "apache", "conf", "httpd.conf"),
            Path.Combine(serviceDir, "httpd.conf"),
            Path.Combine(serviceDir, "conf", "apache2.conf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetNginxConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "conf", "nginx.conf"),
            Path.Combine(serviceDir, "nginx.conf"),
            Path.Combine(serviceDir, "conf", "nginx", "nginx.conf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetPhpConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "php.ini"),
            Path.Combine(serviceDir, "conf", "php.ini"),
            Path.Combine(serviceDir, "etc", "php.ini")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    private string GetMySqlConfigPath(string serviceDir)
    {
        var possiblePaths = new[]
        {
            Path.Combine(serviceDir, "my.ini"),
            Path.Combine(serviceDir, "my.cnf"),
            Path.Combine(serviceDir, "conf", "my.ini"),
            Path.Combine(serviceDir, "etc", "my.cnf")
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }

    /// <summary>
    /// Thread-safe property change notification
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        else
        {
            Application.Current?.Dispatcher?.Invoke(() =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
