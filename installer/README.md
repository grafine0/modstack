# ModStack Installer

This directory contains the Inno Setup installer script and supporting files for creating a professional installer for the ModStack application.

## Files

- `ModStackSetup.iss` - Main Inno Setup script
- `build-installer.bat` - Batch file to build the installer
- `readme.txt` - Information shown before installation
- `license.txt` - License agreement shown during installation
- `README.md` - This file

## Prerequisites

1. **Inno Setup** - Download and install from [https://jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php)
2. **Built Application** - The ModStack application must be built in Release mode first

## Building the Installer

### Method 1: Using the Batch File (Recommended)
1. Build the ModStack application:
   ```bash
   dotnet build --configuration Release
   ```
2. Run the batch file:
   ```bash
   cd installer
   build-installer.bat
   ```

### Method 2: Using Inno Setup GUI
1. Open Inno Setup Compiler
2. Open `ModStackSetup.iss`
3. Click "Build" → "Compile"

## Installer Features

### Custom Directory Selection
- Users can choose where to install ModStack (default: `C:\ModStack`)
- Validates directory permissions
- Creates all necessary subdirectories (Services, Projects, Logs, Config)

### Registry Integration
- Sets up proper Windows registry entries
- Stores user-chosen directory paths
- Enables proper uninstallation

### Shortcuts and Integration
- Start Menu shortcut
- Optional Desktop shortcut
- Optional Quick Launch shortcut
- Uninstaller entry in Add/Remove Programs

### Professional Features
- License agreement display
- Readme information
- Modern wizard interface
- Progress indicators
- Error handling

## Customization

### Changing Default Directory
Edit the `ModStackSetup.iss` file and modify:
```pascal
RootDirPage.Values[0] := 'C:\YourPreferredDirectory';
```

### Adding Files
Add files to the `[Files]` section:
```pascal
Source: "path\to\your\file"; DestDir: "{app}\subfolder"; Flags: ignoreversion
```

### Modifying Registry Entries
Edit the `[Registry]` section to add or modify registry entries.

### Changing Application Information
Update the constants at the top of `ModStackSetup.iss`:
```pascal
#define MyAppName "Your App Name"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
```

## Output

The installer will be created as `dist\ModStackSetup.exe` and will include:
- All application files
- .NET 8.0 runtime (if needed)
- Documentation
- Sample configurations

## Testing

1. Build the installer
2. Run `ModStackSetup.exe` on a test machine
3. Verify:
   - Installation completes successfully
   - Application launches correctly
   - All features work as expected
   - Uninstallation removes everything properly

## Troubleshooting

### "Inno Setup not found" Error
- Install Inno Setup from the official website
- Make sure it's installed in the default location

### "Build output not found" Error
- Build the application first: `dotnet build --configuration Release`
- Ensure the output is in `bin\Release\net8.0-windows\`

### Installer Creation Fails
- Check the Inno Setup script syntax
- Ensure all referenced files exist
- Run Inno Setup Compiler manually to see detailed error messages

## Advanced Features

### Silent Installation
```bash
ModStackSetup.exe /SILENT
```

### Custom Installation Directory
```bash
ModStackSetup.exe /DIR="D:\MyModStack"
```

### Unattended Installation
```bash
ModStackSetup.exe /VERYSILENT /NORESTART
```
