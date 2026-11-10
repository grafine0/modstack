# Version Management Guide

This guide explains how to change the version number and release a new version of ModStack.

## Current Version

The current version is managed centrally in `Directory.Build.props`. The current version is **2.0.0**.

## How to Change the Version

### Step 1: Update Central Version File

Edit `Directory.Build.props` in the root directory:

```xml
<Project>
  <!-- Centralized version management for all projects -->
  <PropertyGroup>
    <Version>1.2.0</Version>              <!-- Change this: Major.Minor.Patch -->
    <AssemblyVersion>1.2.0.0</AssemblyVersion>  <!-- Change this: Major.Minor.Patch.0 -->
    <FileVersion>1.2.0.0</FileVersion>    <!-- Change this: Major.Minor.Patch.0 -->
    <Company>ModStack Development Team</Company>
    <Product>ModStack - Modular Web Development Stack Manager</Product>
    <Copyright>© 2025 ModStack. All rights reserved.</Copyright>
  </PropertyGroup>
</Project>
```

**Version Format:**
- **Major**: Breaking changes (e.g., 1.0.0 → 2.0.0)
- **Minor**: New features, backward compatible (e.g., 1.0.0 → 1.1.0)
- **Patch**: Bug fixes, backward compatible (e.g., 1.0.0 → 1.0.1)

### Step 2: Update Application Manifest

Edit `ModStack.GUI/app.manifest`:

```xml
<assemblyIdentity version="1.2.0.0" name="ModStack.GUI"/>
```

**Important:** This should match the `AssemblyVersion` from Step 1 (with `.0` appended).

### Step 3: Update Installer Script

Edit `installer/ModStackSetup.iss`:

```iss
#define MyAppVersion "1.2.0"
```

**Important:** This should match the `Version` from Step 1 (without the fourth number).

### Step 4: Rebuild the Solution

After updating the version numbers:

1. **Clean the solution**:
   ```bash
   dotnet clean
   ```

2. **Rebuild the solution**:
   ```bash
   dotnet build --configuration Release
   ```

   Or in Visual Studio:
   - Right-click solution → "Clean Solution"
   - Right-click solution → "Rebuild Solution"

3. **The UI will automatically display the new version** because it reads from the assembly at runtime.

## Version Display Locations

The version is automatically displayed in:

1. **About View** (`Views/AboutView.xaml`) - "Version X.X.X"
2. **Settings View** (`Views/SettingsView.xaml`) - "Version X.X.X"
3. **Main Window Status Bar** (`MainWindow.xaml`) - "vX.X.X"

All version displays read from `Constants.ApplicationVersion`, which gets the version from the assembly automatically. **You don't need to update these files manually.**

## Version Number Locations Summary

| File | What to Change | Example |
|------|---------------|---------|
| `Directory.Build.props` | `Version`, `AssemblyVersion`, `FileVersion` | 2.0.0, 2.0.0.0, 2.0.0.0 |
| `ModStack.GUI/app.manifest` | `assemblyIdentity version` | 2.0.0.0 |
| `installer/ModStackSetup.iss` | `#define MyAppVersion` | 2.0.0 |

**UI Files (Automatic - No Manual Changes Needed):**
- `Views/AboutView.xaml` - Reads from assembly
- `Views/SettingsView.xaml` - Reads from assembly
- `MainWindow.xaml` - Reads from assembly
- `ModStack.Common/Constants.cs` - Provides `ApplicationVersion` property

## Creating a Release

### 1. Update Version

Follow Steps 1-3 above to update the version number.

### 2. Build Release Version

```bash
dotnet build --configuration Release
```

### 3. Test the Application

- Run the application and verify the version appears correctly in:
  - About dialog
  - Settings page
  - Status bar
- Verify all features work as expected

### 4. Create Installer

If using Inno Setup:

1. Open `installer/ModStackSetup.iss` in Inno Setup Compiler
2. Build the installer (Build → Compile)
3. The installer will be created in `installer/dist/`

### 5. Create Release Package

1. Create a release folder (e.g., `releases/v1.2.0/`)
2. Copy the following:
   - `ModStack.GUI/bin/Release/net8.0-windows/*` (all files)
   - `installer/dist/ModStackSetup.exe` (the installer)
3. Optional: Create a ZIP file with the release

### 6. Update Release Notes

Create or update `CHANGELOG.md` or release notes documenting:
- New features
- Bug fixes
- Breaking changes (if any)
- Known issues

## Quick Version Update Checklist

- [ ] Update `Directory.Build.props` (Version, AssemblyVersion, FileVersion)
- [ ] Update `ModStack.GUI/app.manifest` (assemblyIdentity version)
- [ ] Update `installer/ModStackSetup.iss` (MyAppVersion)
- [ ] Clean and rebuild solution
- [ ] Verify version appears correctly in UI
- [ ] Test application functionality
- [ ] Build installer
- [ ] Update release notes

## Version History

- **2.0.0** - Current version (Decoupled updates page, architecture improvements)
- **1.1.0** - Previous stable version (with centralized version management)
- **1.0.0** - Initial release

## Notes

- The version in `Directory.Build.props` is the **source of truth** for all projects
- UI elements automatically read the version from the assembly, so they stay in sync
- Always rebuild after changing version numbers to ensure all files are updated
- The installer version should match the application version

