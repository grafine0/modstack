# 🚀 Quick Start: Building Your ModStack Installer

Follow these simple steps to create a professional installer for ModStack.

---

## ✅ Step 1: Install Inno Setup (5 minutes)

1. **Download Inno Setup:**
   - Go to: https://jrsoftware.org/isdl.php
   - Download the latest version (Inno Setup 6.x)
   - File size: ~3 MB

2. **Install:**
   - Run the downloaded installer
   - Click "Next" through all prompts (use default settings)
   - Installation location: `C:\Program Files (x86)\Inno Setup 6\`

---

## ✅ Step 2: Build ModStack Application (2 minutes)

**Open Command Prompt or PowerShell** and run:

```bash
cd G:\ModStack
dotnet build ModStack.sln --configuration Release
```

**Wait for:** "Build succeeded" message

**Output location:** `G:\ModStack\ModStack.GUI\bin\Release\net8.0-windows\`

---

## ✅ Step 3: Create the Installer (1 minute)

### Option A: Using the Batch File (Easiest)

```bash
cd G:\ModStack\installer
build-installer.bat
```

### Option B: Using Inno Setup GUI

1. Open **Inno Setup Compiler** from Start Menu
2. Click **File → Open**
3. Navigate to: `G:\ModStack\installer\ModStackSetup.iss`
4. Click **Build → Compile** (or press F9)

---

## ✅ Step 4: Test the Installer (5 minutes)

1. **Find your installer:**
   - Location: `G:\ModStack\installer\dist\ModStackSetup.exe`

2. **Run it as Administrator:**
   
   **Option A: Easy Method**
   - Run: `installer\INSTALL_AS_ADMIN.bat`
   - This automatically requests admin privileges
   
   **Option B: Manual Method**
   - Right-click `ModStackSetup.exe`
   - Select "Run as administrator"
   - Click "Yes" when Windows prompts for elevation

3. **Follow the wizard:**
   - Choose installation directory (default: `C:\ModStack`)
   - Windows will automatically prompt for admin access

3. **Verify:**
   - ✅ Application launches from Start Menu
   - ✅ Desktop shortcut works (if you chose to create one)
   - ✅ Application runs correctly
   - ✅ Settings show correct root directory

---

## ✅ Step 5: Distribute (Optional)

Your installer is ready! You can now:

- **Share it with users** via your website
- **Upload to GitHub** releases
- **Send to testers** for feedback

---

## 🔧 Troubleshooting

### "Inno Setup not found" Error
- Make sure Inno Setup is installed
- Check it's in: `C:\Program Files (x86)\Inno Setup 6\`

### "Build output not found" Error
- Run: `dotnet build ModStack.sln --configuration Release`
- Check: `G:\ModStack\ModStack.GUI\bin\Release\net8.0-windows\ModStack.GUI.exe` exists

### "File not found" during compilation
- Make sure you're in the `installer` directory
- All paths in the script are relative to the `installer` folder

---

## 📊 What Gets Installed

When users run your installer, they get:

- ✅ **ModStack Application** - All executable files
- ✅ **Dependencies** - All required DLLs and runtimes
- ✅ **Documentation** - README and usage guides
- ✅ **Shortcuts** - Start Menu and Desktop (optional)
- ✅ **Registry Settings** - Root directory configuration
- ✅ **Uninstaller** - Clean removal capability

---

## 🎯 Advanced Options

### Silent Installation
```bash
ModStackSetup.exe /SILENT
```

### Custom Directory
```bash
ModStackSetup.exe /DIR="D:\MyCustomPath"
```

### Completely Silent (No UI)
```bash
ModStackSetup.exe /VERYSILENT /NORESTART
```

---

## 📞 Need Help?

If you encounter any issues:
1. Check the `installer/README.md` for detailed documentation
2. Review the Inno Setup log file (created during compilation)
3. Make sure all file paths in `ModStackSetup.iss` are correct

---

## ✨ That's It!

You now have a professional installer for ModStack that:
- ✅ Asks users where to install
- ✅ Defaults to `C:\ModStack` (as we configured)
- ✅ Creates proper shortcuts
- ✅ Sets up registry entries
- ✅ Includes an uninstaller

**Happy deploying! 🎉**
