# PackageInstaller Configuration Enhancement

## Overview

The `PackageInstaller` has been updated to use the configurable services directory from `IPathConfigurationService` instead of relying on hardcoded paths. This ensures that all package installations respect the user's configured service directory settings.

## Changes Made

### 1. Dependency Injection

**Before:**
```csharp
public PackageInstaller(ModStack.Core.Repositories.IServiceRepository serviceRepository)
{
    _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", "ModStack Package Manager");
    _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
}
```

**After:**
```csharp
public PackageInstaller(
    ModStack.Core.Repositories.IServiceRepository serviceRepository,
    IPathConfigurationService pathConfigurationService)
{
    _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", "ModStack Package Manager");
    _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
    _pathConfigurationService = pathConfigurationService ?? throw new ArgumentNullException(nameof(pathConfigurationService));
}
```

### 2. Path Generation Logic

**Before:**
```csharp
private string GenerateVersionSpecificPath(PackageInstallationConfig config)
{
    var basePath = config.InstallPath;  // Used hardcoded or passed-in path
    // ... rest of logic
}
```

**After:**
```csharp
private string GenerateVersionSpecificPath(PackageInstallationConfig config)
{
    // Use the configurable services directory instead of config.InstallPath
    var basePath = _pathConfigurationService.GetServicesDirectory();
    // ... rest of logic
}
```

## Installation Flow

1. **Path Resolution**: `PackageInstaller` calls `_pathConfigurationService.GetServicesDirectory()` to get the configured services directory
2. **Path Prioritization**: The path configuration service uses this hierarchy:
   - User-configured `ServicesDirectory` setting
   - Legacy `InstallPath` setting (backward compatibility)
   - Cross-platform default path (`%AppData%\ModStack\Services` on Windows)
3. **Version-Specific Path**: Generated using the pattern `{ServicesDirectory}/{PackageName}/{PackageName-Version}`

## Example Installation Paths

### With Default Configuration:
- **Windows**: `%AppData%\ModStack\Services\apache\apache-2.4.58`
- **Linux/macOS**: `~/.config/ModStack/Services/apache/apache-2.4.58`

### With Custom Configuration:
- **Custom**: `D:\MyServices\apache\apache-2.4.58`

## Benefits

- ✅ **Centralized Configuration**: All services install to the user's configured directory
- ✅ **Cross-Platform**: Automatic platform-appropriate defaults
- ✅ **Backward Compatibility**: Existing installations continue to work
- ✅ **User Control**: Full control over where services are installed
- ✅ **Consistency**: All ModStack components use the same path configuration

## Migration Notes

- Existing installations are not affected - they continue to work from their current locations
- New installations will use the configured services directory
- Users can migrate existing services by:
  1. Changing the services directory setting
  2. Moving existing service folders to the new location
  3. Updating service configurations if needed

## Settings Integration

The package installer now fully integrates with the `ISettingsService` directory configuration:

```csharp
// User can configure via settings
var settingsService = serviceProvider.GetService<ISettingsService>();
settingsService.ServicesDirectory = @"D:\MyCustomServicesPath";
settingsService.SaveSettings();

// PackageInstaller will automatically use the new path
var packageInstaller = serviceProvider.GetService<PackageInstaller>();
await packageInstaller.InstallPackageAsync(config); // Will install to D:\MyCustomServicesPath
```
