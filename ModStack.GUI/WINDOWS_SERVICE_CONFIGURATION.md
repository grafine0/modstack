# Windows Service Configuration System

This document explains how the new database-driven Windows service name mapping system works.

## Overview

Previously, service restart functionality used hardcoded mappings like:
```csharp
var name when name.Contains("apache") && name.Contains("2.4.65") => "Apache2.4"
```

This was brittle and broke when service versions changed. Now the system uses:
1. **Database-driven mappings** stored in `ServiceDefinition.WindowsServiceName`
2. **Auto-detection** of installed Windows services
3. **Configurable defaults** based on service type and version
4. **Fallback patterns** for common services

## How It Works

### 1. Database Lookup (Primary)
```csharp
// Service definition stores the actual Windows service name
serviceDefinition.WindowsServiceName = "Apache2.4";
```

### 2. Auto-Detection (Secondary)
```csharp
// Automatically detects installed Windows services on startup
await serviceConfigService.AutoDetectWindowsServiceNamesAsync();
```

### 3. Default Configuration (Tertiary)
```csharp
// Applies sensible defaults based on service type/version
await serviceConfigService.ConfigureDefaultWindowsServiceNamesAsync();
```

### 4. Fallback Patterns (Last Resort)
```csharp
// Simple pattern matching for common services
return nameLower switch
{
    var name when name.Contains("apache") => "Apache2.4",
    var name when name.Contains("mysql") => "MySQL80",
    // ...
};
```

## Configuration

### Automatic Configuration (Default)
The system automatically configures Windows service names on application startup:

1. **Auto-detects** installed Windows services
2. **Matches** them with ModStack service definitions
3. **Applies defaults** for unmatched services
4. **Logs results** for troubleshooting

### Manual Configuration
You can manually configure Windows service names:

```csharp
var helper = ServiceLocator.GetService<ServiceManagementHelper>();

// Configure specific service
await helper.ConfigureWindowsServiceNameAsync(serviceId, "Apache2.4");

// Refresh all configurations
await helper.RefreshServiceConfigurationsAsync();
```

### Database Direct
Update the `ServiceDefinition` table directly:
```sql
UPDATE Services 
SET WindowsServiceName = 'Apache2.4' 
WHERE Name = 'apache-2.4.65';
```

## Common Service Mappings

| Service Display Name | Windows Service Name | Notes |
|---------------------|---------------------|--------|
| Apache HTTP Server 2.4.65 | `Apache2.4` | Standard Apache installation |
| MySQL 8.0.35 | `MySQL80` | MySQL 8.0 series |
| MySQL 8.4.2 | `MySQL84` | MySQL 8.4 series |
| MariaDB 10.11 | `MariaDB` | Standard MariaDB |
| Nginx 1.25.3 | `nginx` | Standard Nginx |
| PostgreSQL 16 | `PostgreSQL` | Standard PostgreSQL |

## Benefits

✅ **Version-agnostic** - No hardcoded version numbers  
✅ **Configurable** - Database-driven configuration  
✅ **Auto-detection** - Finds installed services automatically  
✅ **Fallback-safe** - Graceful degradation if configuration fails  
✅ **Logging** - Full audit trail of service restart attempts  
✅ **Maintainable** - Easy to update without code changes  

## Troubleshooting

### Service Not Restarting?
1. Check if `WindowsServiceName` is configured:
   ```sql
   SELECT Name, DisplayName FROM Services;
   ```

2. Verify Windows service exists:
   ```powershell
   Get-Service -Name "Apache2.4"
   ```

3. Check application logs for service mapping warnings

### Auto-Detection Not Working?
1. Run manual refresh:
   ```csharp
   await helper.RefreshServiceConfigurationsAsync();
   ```

2. Check if running with admin privileges (required for service enumeration)

3. Verify services are actually installed as Windows services

### Adding New Service Types
1. Update `ServiceConfigurationService.GetDefaultWindowsServiceName()`
2. Add detection patterns in `DetectWindowsServiceName()`
3. Test with `RefreshServiceConfigurationsAsync()`

## Migration from Hardcoded System

Existing installations will automatically migrate:
1. Old hardcoded mappings serve as fallbacks
2. Auto-detection runs on first startup
3. Database is populated with detected values
4. Future versions can remove hardcoded fallbacks

No manual intervention required! 🎉
