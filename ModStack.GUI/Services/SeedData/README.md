# Package Seed Data System

The ModStack application now uses a JSON-based seed data system for initializing default packages and their versions. This makes it easy to update package configurations without requiring code changes.

## Files

- **`Services/SeedData/DefaultPackages.json`** - Contains the default packages and versions configuration
- **`Services/SeedData/SeedDataModels.cs`** - C# models for deserializing the JSON data

## JSON Structure

The `DefaultPackages.json` file follows this structure:

```json
{
  "version": "1.0",
  "description": "Default packages and versions to seed in the ModStack database",
  "packages": [
    {
      "name": "apache",
      "displayName": "Apache HTTP Server",
      "description": "Apache HTTP Server for web hosting",
      "category": "Web Server",
      "icon": "apache-icon.png",
      "isPopular": true,
      "tags": ["web", "server", "http", "apache"],
      "versions": [
        {
          "version": "2.4.58",
          "displayName": "Apache HTTP Server 2.4.58",
          "serviceName": "apache-2.4.58",
          "releaseDate": "2023-10-01T00:00:00Z",
          "isStable": true,
          "isRecommended": true,
          "isLTS": false,
          "size": 15728640,
          "downloadUrls": [
            "https://archive.apache.org/dist/httpd/httpd-2.4.58-win64-VS17.zip"
          ]
        }
      ]
    }
  ]
}
```

## Package Properties

Each package in the JSON file supports the following properties:

- **`name`** (string) - Unique identifier for the package
- **`displayName`** (string) - Human-readable name shown in the UI
- **`description`** (string) - Description of the package
- **`category`** (string) - Category for grouping packages (Web Server, Database, etc.)
- **`icon`** (string) - Icon filename for the package
- **`isPopular`** (boolean) - Whether the package should be marked as popular
- **`tags`** (array) - Array of tags for searching and filtering
- **`versions`** (array) - Array of available versions for this package

## Version Properties

Each version within a package supports:

- **`version`** (string) - Version number (e.g., "2.4.58", "8.3.0")
- **`displayName`** (string) - Human-readable version name
- **`serviceName`** (string) - Auto-generated service name for installation
- **`releaseDate`** (datetime) - Release date in ISO format
- **`isStable`** (boolean) - Whether this is a stable release
- **`isRecommended`** (boolean) - Whether this version is recommended
- **`isLTS`** (boolean) - Whether this is a Long Term Support version
- **`size`** (number) - Download size in bytes
- **`downloadUrls`** (array) - Array of download URLs

## How It Works

1. **Primary Method**: The system first attempts to load packages from `DefaultPackages.json`
2. **Fallback Method**: If the JSON file is missing or invalid, it falls back to hardcoded packages
3. **Package Creation**: Uses `PackageRepository` to create packages and their versions
4. **Duplicate Prevention**: Checks if packages already exist before creating them
5. **Logging**: Comprehensive logging shows which method was used and what packages were created

## Updating Packages

To add, remove, or modify default packages:

1. Edit the `DefaultPackages.json` file
2. Update the `version` field to indicate the change
3. Rebuild the application
4. The new packages will be seeded on the next database initialization

## Benefits

- **No Code Changes**: Update packages without touching C# code
- **Version Control**: Track package configuration changes in version control
- **Easy Maintenance**: Non-developers can update package configurations
- **Fallback Safety**: System remains functional even if JSON is corrupted
- **Extensible**: Easy to add new package properties in the future
- **Proper Architecture**: Seeds packages (available software) instead of services (installed instances)

## Debug Output

The system provides detailed logging:

```
🔧 Creating default packages from JSON seed data...
📋 Loaded 7 packages from seed data file (version: 1.0)
✅ Created package: Apache HTTP Server
  ✅ Added version: Apache HTTP Server 2.4.58
✅ Created package: Nginx
  ✅ Added version: Nginx 1.24.0
🎯 All 7 default packages created successfully from JSON
```

If JSON loading fails, you'll see:

```
❌ Failed to create default packages from JSON: [error message]
🔄 Falling back to hardcoded packages...
🔧 Creating default packages using fallback hardcoded data...
```

## Architecture Benefits

This approach correctly separates concerns:

- **Packages**: Available software that can be installed (seeded from JSON)
- **Services**: Actual installed instances (created when packages are installed)
- **Package Versions**: Different versions of the same package (e.g., PHP 8.3.0, PHP 8.4.2)

This makes the system more maintainable and follows proper software architecture principles.
