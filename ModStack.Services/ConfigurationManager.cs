using Microsoft.Extensions.Logging;
using ModStack.Common.Models;
using ModStack.Data.Interfaces;
using System.Text.Json;

namespace ModStack.Services;

/// <summary>
/// Manages configuration files and settings
/// </summary>
public class ConfigurationManager
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly ISettingsRepository _settingsRepository;

    public ConfigurationManager(
        ILogger<ConfigurationManager> logger,
        ISettingsRepository settingsRepository)
    {
        _logger = logger;
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// Saves a configuration setting
    /// </summary>
    public async Task<AppSetting> SaveConfigurationSettingAsync(SaveConfigurationSettingRequest request)
    {
        try
        {
            _logger.LogInformation("Saving configuration setting {Category}/{Key}", request.Category, request.Key);

            // Check if setting already exists
            var existingSetting = await _settingsRepository.GetByCategoryAndKeyAsync(request.Category, request.Key);
            
            var setting = existingSetting ?? new AppSetting
            {
                Category = request.Category,
                Key = request.Key,
                Value = request.Value,
                DataType = request.DataType,
                Description = request.Description,
                IsUserSetting = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (existingSetting != null)
            {
                setting.Value = request.Value;
                setting.DataType = request.DataType;
                setting.Description = request.Description;
                setting.UpdatedAt = DateTime.Now;
            }

            var result = existingSetting == null 
                ? await _settingsRepository.CreateAsync(setting)
                : await _settingsRepository.UpdateAsync(setting);

            _logger.LogInformation("Configuration setting {Category}/{Key} saved successfully", request.Category, request.Key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration setting {Category}/{Key}", request.Category, request.Key);
            throw;
        }
    }

    /// <summary>
    /// Gets a configuration setting by category and key
    /// </summary>
    public async Task<AppSetting?> GetConfigurationSettingAsync(string category, string key)
    {
        return await _settingsRepository.GetByCategoryAndKeyAsync(category, key);
    }

    /// <summary>
    /// Gets all configuration settings in a category
    /// </summary>
    public async Task<List<AppSetting>> GetConfigurationSettingsByCategoryAsync(string category)
    {
        return await _settingsRepository.GetByCategoryAsync(category);
    }

    /// <summary>
    /// Gets all configuration settings
    /// </summary>
    public async Task<List<AppSetting>> GetAllConfigurationSettingsAsync()
    {
        return await _settingsRepository.GetAllAsync();
    }

    /// <summary>
    /// Deletes a configuration setting
    /// </summary>
    public async Task DeleteConfigurationSettingAsync(string category, string key)
    {
        try
        {
            _logger.LogInformation("Deleting configuration setting {Category}/{Key}", category, key);

            var setting = await _settingsRepository.GetByCategoryAndKeyAsync(category, key);
            if (setting == null)
            {
                _logger.LogWarning("Configuration setting {Category}/{Key} not found", category, key);
                return;
            }

            await _settingsRepository.DeleteAsync(setting.Id);

            _logger.LogInformation("Configuration setting {Category}/{Key} deleted successfully", category, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete configuration setting {Category}/{Key}", category, key);
            throw;
        }
    }

    /// <summary>
    /// Saves application settings
    /// </summary>
    public async Task SaveApplicationSettingAsync(string key, object value)
    {
        try
        {
            var jsonValue = JsonSerializer.Serialize(value);
            await SaveConfigurationSettingAsync(new SaveConfigurationSettingRequest
            {
                Category = "ApplicationSettings",
                Key = key,
                Value = jsonValue,
                DataType = "json",
                Description = $"Application setting: {key}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save application setting {Key}", key);
            throw;
        }
    }

    /// <summary>
    /// Gets an application setting
    /// </summary>
    public async Task<T?> GetApplicationSettingAsync<T>(string key)
    {
        try
        {
            var setting = await GetConfigurationSettingAsync("ApplicationSettings", key);
            if (setting?.Value == null)
                return default;

            return JsonSerializer.Deserialize<T>(setting.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get application setting {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Saves service configuration
    /// </summary>
    public async Task SaveServiceConfigurationAsync(long serviceId, string configType, string content)
    {
        try
        {
            await SaveConfigurationSettingAsync(new SaveConfigurationSettingRequest
            {
                Category = "ServiceConfigurations",
                Key = $"{serviceId}_{configType}",
                Value = content,
                DataType = "text",
                Description = $"Service configuration for service {serviceId}, type {configType}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service configuration for service {ServiceId}", serviceId);
            throw;
        }
    }

    /// <summary>
    /// Gets service configuration
    /// </summary>
    public async Task<string?> GetServiceConfigurationAsync(long serviceId, string configType)
    {
        try
        {
            var setting = await GetConfigurationSettingAsync("ServiceConfigurations", $"{serviceId}_{configType}");
            return setting?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service configuration for service {ServiceId}", serviceId);
            return null;
        }
    }

    /// <summary>
    /// Saves stack configuration
    /// </summary>
    public async Task SaveStackConfigurationAsync(int stackId, string configType, string content)
    {
        try
        {
            await SaveConfigurationSettingAsync(new SaveConfigurationSettingRequest
            {
                Category = "StackConfigurations",
                Key = $"{stackId}_{configType}",
                Value = content,
                DataType = "text",
                Description = $"Stack configuration for stack {stackId}, type {configType}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save stack configuration for stack {StackId}", stackId);
            throw;
        }
    }

    /// <summary>
    /// Gets stack configuration
    /// </summary>
    public async Task<string?> GetStackConfigurationAsync(int stackId, string configType)
    {
        try
        {
            var setting = await GetConfigurationSettingAsync("StackConfigurations", $"{stackId}_{configType}");
            return setting?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stack configuration for stack {StackId}", stackId);
            return null;
        }
    }

    /// <summary>
    /// Exports all configurations to a directory
    /// </summary>
    public async Task ExportConfigurationsAsync(string exportPath)
    {
        try
        {
            _logger.LogInformation("Exporting configurations to {ExportPath}", exportPath);

            var settings = await GetAllConfigurationSettingsAsync();
            
            foreach (var setting in settings)
            {
                var fileName = $"{setting.Category}_{setting.Key}.txt";
                var filePath = Path.Combine(exportPath, fileName);
                
                if (!string.IsNullOrEmpty(setting.Value))
                {
                    await File.WriteAllTextAsync(filePath, setting.Value);
                }
            }

            _logger.LogInformation("Exported {Count} configuration settings", settings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configurations");
            throw;
        }
    }

    /// <summary>
    /// Imports configurations from a directory
    /// </summary>
    public async Task ImportConfigurationsAsync(string importPath)
    {
        try
        {
            _logger.LogInformation("Importing configurations from {ImportPath}", importPath);

            var files = Directory.GetFiles(importPath, "*.txt", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var content = await File.ReadAllTextAsync(file);

                // Parse category and key from filename
                var parts = fileName.Split('_', 2);
                if (parts.Length == 2)
                {
                    await SaveConfigurationSettingAsync(new SaveConfigurationSettingRequest
                    {
                        Category = parts[0],
                        Key = parts[1],
                        Value = content,
                        DataType = "text",
                        Description = $"Imported from {fileName}"
                    });
                }
            }

            _logger.LogInformation("Imported {Count} configuration files", files.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configurations");
            throw;
        }
    }

    /// <summary>
    /// Gets configuration statistics
    /// </summary>
    public async Task<ConfigurationStatistics> GetConfigurationStatisticsAsync()
    {
        var settings = await GetAllConfigurationSettingsAsync();
        
        return new ConfigurationStatistics
        {
            TotalConfigurationSettings = settings.Count,
            TotalSize = settings.Sum(s => s.Value?.Length ?? 0),
            SettingsByCategory = settings
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            SizeByCategory = settings
                .GroupBy(s => s.Category)
                .ToDictionary(g => g.Key, g => (long)g.Sum(s => s.Value?.Length ?? 0))
        };
    }

    private string DetermineCategoryFromPath(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        if (parts.Length > 0)
        {
            return parts[0] switch
            {
                "services" => "ServiceConfigurations",
                "stacks" => "StackConfigurations",
                "settings" => "ApplicationSettings",
                "projects" => "ProjectConfigurations",
                _ => "MiscConfigurations"
            };
        }

        return "MiscConfigurations";
    }
}

/// <summary>
/// Request model for saving configuration settings
/// </summary>
public class SaveConfigurationSettingRequest
{
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string DataType { get; set; } = "string";
    public string? Description { get; set; }
}

/// <summary>
/// Configuration statistics
/// </summary>
public class ConfigurationStatistics
{
    public int TotalConfigurationSettings { get; set; }
    public long TotalSize { get; set; }
    public Dictionary<string, int> SettingsByCategory { get; set; } = new();
    public Dictionary<string, long> SizeByCategory { get; set; } = new();
}
