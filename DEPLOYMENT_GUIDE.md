# 🚀 ModStack Deployment Guide

This guide covers everything you need to deploy ModStack with a professional installer.

---

## 📁 What We've Created

Your workspace now includes a complete installer setup:

```
G:\ModStack\
├── installer\
│   ├── ModStackSetup.iss      ← Main installer script
│   ├── build-installer.bat    ← Easy build script
│   ├── license.txt             ← License agreement
│   ├── readme.txt              ← Pre-install info
│   ├── README.md               ← Detailed documentation
│   ├── QUICK_START.md          ← Step-by-step guide
│   └── CHECKLIST.md            ← Pre-release checklist
```

---

## ⚡ Quick Start (3 Steps)

### 1️⃣ Install Inno Setup
- Download from: https://jrsoftware.org/isdl.php
- Install with default settings

### 2️⃣ Build Your Application
```bash
cd G:\ModStack
dotnet build ModStack.sln --configuration Release
```

### 3️⃣ Create the Installer
```bash
cd installer
build-installer.bat
```

**Output:** `installer\dist\ModStackSetup.exe`

---

## 📚 Documentation Files

| File | Purpose |
|------|---------|
| **QUICK_START.md** | Step-by-step instructions for first-time setup |
| **CHECKLIST.md** | Pre-release testing checklist |
| **README.md** | Complete documentation for the installer |
| **This file** | Overview and deployment strategy |

---

## 🎯 What the Installer Does

When users run `ModStackSetup.exe`:

1. **Shows welcome screen** with application info
2. **Displays license** agreement (from `license.txt`)
3. **Shows readme** information (from `readme.txt`)
4. **Asks for installation location** (defaults to `C:\Program Files\ModStack`)
5. **Asks for data directory** (defaults to `C:\ModStack`)
   - This is where services, projects, logs will be stored
   - Users can customize this location
6. **Installs files** to chosen locations
7. **Creates shortcuts** (Start Menu + optional Desktop)
8. **Sets registry values** with chosen directories
9. **Launches application** (optional)

---

## 🔧 Customization

### Change Default Data Directory

Edit `ModStackSetup.iss` line 59:
```pascal
RootDirPage.Values[0] := 'C:\ModStack';  ← Change this
```

### Change Version Number

Edit `ModStackSetup.iss` line 5:
```pascal
#define MyAppVersion "1.0.0"  ← Change this
```

### Update URLs

Edit `ModStackSetup.iss` line 7:
```pascal
#define MyAppURL "https://github.com/yourusername/modstack"  ← Change this
```

---

## 🧪 Testing Strategy

### Phase 1: Developer Machine
1. Build installer
2. Run on your development machine
3. Verify basic functionality

### Phase 2: Clean Environment
1. Use a Virtual Machine or test PC
2. Fresh Windows installation
3. Run installer as different user types:
   - Administrator
   - Standard user
4. Test all features

### Phase 3: Upgrade Testing
1. Install old version
2. Install new version
3. Verify settings preserved
4. Verify no data loss

### Phase 4: Uninstall Testing
1. Install application
2. Use it (create projects, configure services)
3. Uninstall
4. Verify clean removal
5. Check no files left behind

---

## 📦 Distribution Options

### Option 1: GitHub Releases
```bash
# Create a release
gh release create v1.0.0 installer\dist\ModStackSetup.exe --title "ModStack v1.0.0" --notes "Release notes here"
```

### Option 2: Website Download
- Upload `ModStackSetup.exe` to your web server
- Create download page with:
  - System requirements
  - Installation instructions
  - SHA256 checksum
  - Release notes

### Option 3: Direct Distribution
- Email to testers
- Share via cloud storage
- Include in documentation

---

## 🔐 Security Considerations

### Code Signing (Recommended for Public Release)

**Why:** Prevents Windows SmartScreen warnings

**How:**
1. Purchase code signing certificate ($100-500/year)
   - DigiCert, Sectigo, etc.
2. Sign the installer:
   ```bash
   signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com ModStackSetup.exe
   ```

**Or:** Add to Inno Setup script:
```pascal
[Setup]
SignTool=custom
SignedUninstaller=yes
```

### Without Code Signing
- Users will see "Unknown publisher" warning
- They need to click "More info" → "Run anyway"
- Fine for internal/testing use

---

## 📊 Deployment Checklist

Before distributing to users:

- [ ] **Application tested** and working
- [ ] **Installer builds** without errors
- [ ] **Installation tested** on clean machine
- [ ] **Uninstallation tested** completely
- [ ] **Documentation updated** (version, release notes)
- [ ] **Shortcuts work** correctly
- [ ] **Registry entries** are correct
- [ ] **Silent install** works (for enterprise deployment)
- [ ] **Upgrade path** tested (if applicable)
- [ ] **Virus scanned** (optional but good practice)

---

## 🚨 Common Issues & Solutions

### Issue: "File not found" during build
**Solution:** Check paths in `ModStackSetup.iss` are relative to `installer\` folder

### Issue: Application doesn't find data directory
**Solution:** Verify registry entries are set:
```bash
reg query "HKCU\Software\ModStack" /v RootDirectory
```

### Issue: Installer triggers antivirus
**Solution:** 
- Get code signing certificate
- Or whitelist with antivirus vendors
- Or use VirusTotal to verify

### Issue: Can't uninstall completely
**Solution:** Check `[UninstallDelete]` section in script includes all directories

---

## 🎯 Next Steps

1. **Read** `installer\QUICK_START.md` for immediate action items
2. **Build** your first installer following those instructions
3. **Test** thoroughly using `installer\CHECKLIST.md`
4. **Customize** installer script for your needs
5. **Distribute** when ready!

---

## 📞 Support Resources

- **Inno Setup Documentation:** https://jrsoftware.org/ishelp/
- **Inno Setup Forum:** https://groups.google.com/g/innosetup
- **Stack Overflow:** Tag `innosetup`

---

## ✨ Summary

You now have:
- ✅ Professional installer with custom directory selection
- ✅ Defaults to `C:\ModStack` as configured
- ✅ Registry integration for settings persistence
- ✅ Start Menu and Desktop shortcuts
- ✅ Complete uninstaller
- ✅ Documentation for building and testing
- ✅ Deployment checklists and guides

**Your application is ready for professional distribution! 🎉**

---

*Last updated: October 2024*
