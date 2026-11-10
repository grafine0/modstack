# Directory Configuration Enhancement

## Overview

The `ISettingsService` has been extended to support custom directory configurations for projects, services, and logs. This allows users to specify where ModStack stores its data, providing flexibility for different deployment scenarios and user preferences.

## New Settings Properties

### Added to ISettingsService and SettingsService:

```csharp
// Directory Settings
string ProjectsDirectory { get; set; }
string ServicesDirectory { get; set; }  
string LogsDirectory { get; set; }
```

## Cross-Platform Default Paths

The system uses cross-platform default paths defined in `Constants.DefaultPaths`:

- **Projects**: `%AppData%\ModStack\Projects` (Windows) or `~/.config/ModStack/Projects` (Linux/macOS)
- **Services**: `%AppData%\ModStack\Services` (Windows) or `~/.config/ModStack/Services` (Linux/macOS)  
- **Logs**: `%AppData%\ModStack\Logs` (Windows) or `~/.config/ModStack/Logs` (Linux/macOS)

## Backward Compatibility

- **ServicesDirectory**: Falls back to the existing `InstallPath` setting for backward compatibility
- **ProjectsDirectory**: Falls back to default cross-platform path
- **LogsDirectory**: Falls back to default cross-platform path

## Registry Storage

Directory settings are persisted in the Windows Registry under `HKEY_CURRENT_USER\Software\ModStack`:

- `ProjectsDirectory`
- `ServicesDirectory` 
- `LogsDirectory`

## PathConfigurationService Integration

The `PathConfigurationService` has been updated to use these new settings:

```csharp
public string GetProjectsDirectory()
{
    var configuredPath = _settingsService.ProjectsDirectory;
    if (string.IsNullOrEmpty(configuredPath))
    {
        configuredPath = Constants.DefaultPaths.Projects;
    }
    
    EnsureDirectoryExists(configuredPath);
    return configuredPath;
}
```

## Usage Examples

### Programmatic Configuration

```csharp
// Inject ISettingsService
private readonly ISettingsService _settingsService;

// Set custom directories
_settingsService.ProjectsDirectory = @"D:\MyModStackProjects";
_settingsService.ServicesDirectory = @"D:\MyModStackServices";
_settingsService.LogsDirectory = @"D:\MyModStackLogs";

// Save the settings
_settingsService.SaveSettings();
```

### Default Behavior

If no custom paths are set, the system will automatically use cross-platform defaults:

```csharp
// These will automatically use defaults if not configured
var projectsPath = _pathConfigurationService.GetProjectsDirectory();
var servicesPath = _pathConfigurationService.GetServicesDirectory();  
var logsPath = _pathConfigurationService.GetLogsDirectory();
```

## Configuration Flow

1. **First Run**: System uses cross-platform defaults and saves them to registry
2. **User Customization**: User can modify directory paths through settings UI
3. **Validation**: `PathConfigurationService` ensures directories exist (creates if needed)
4. **Fallback**: If custom path is invalid, falls back to defaults

## Benefits

- **Flexibility**: Users can choose where ModStack stores data
- **Cross-Platform**: Automatic platform-appropriate defaults
- **Backward Compatibility**: Existing installations continue to work
- **Validation**: Automatic directory creation and validation
- **Centralized**: All path logic goes through `PathConfigurationService`

## Future Enhancements

- GUI settings panel for directory configuration
- Import/export of directory settings
- Validation of directory permissions
- Migration tools for moving existing data
