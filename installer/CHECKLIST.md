# 📋 Pre-Release Checklist

Use this checklist before distributing your ModStack installer to users.

---

## 🔨 Build Preparation

- [ ] **Clean build completed**
  ```bash
  dotnet clean
  dotnet build ModStack.sln --configuration Release
  ```

- [ ] **No build errors or warnings**
  - Check output for any issues
  - Fix all warnings if possible

- [ ] **Application runs correctly**
  - Launch from: `ModStack.GUI\bin\Release\net8.0-windows\ModStack.GUI.exe`
  - Test all main features
  - Verify no crashes

---

## 📦 Installer Creation

- [ ] **Inno Setup installed**
  - Version 6.x or later
  - Available at: https://jrsoftware.org/isdl.php

- [ ] **Installer script updated**
  - Version number is correct in `ModStackSetup.iss`
  - AppId is unique (not the default GUID)
  - URLs updated (GitHub, support, etc.)

- [ ] **Installer builds successfully**
  ```bash
  cd installer
  build-installer.bat
  ```

- [ ] **Output file created**
  - Check: `installer\dist\ModStackSetup.exe` exists
  - File size is reasonable (~50-200 MB depending on dependencies)

---

## 🧪 Installation Testing

### Test on Clean Machine (or VM)

- [ ] **Installation succeeds**
  - Run as administrator
  - Choose custom directory
  - Complete without errors

- [ ] **Files installed correctly**
  - Application files in: `C:\Program Files\ModStack\` (or chosen location)
  - Data directory in: `C:\ModStack\` (or user-chosen location)

- [ ] **Shortcuts created**
  - Start Menu shortcut works
  - Desktop shortcut works (if selected)

- [ ] **Registry entries set**
  ```bash
  reg query "HKCU\Software\ModStack"
  ```
  - RootDirectory is set correctly
  - All directory paths are correct

- [ ] **Application launches**
  - From Start Menu
  - From Desktop shortcut
  - Directly from install folder

- [ ] **Application functions correctly**
  - Can access settings
  - Can manage services
  - Can create projects
  - Logs are being written

- [ ] **Uninstaller works**
  - Uninstall from Control Panel
  - Or run: `C:\Program Files\ModStack\unins000.exe`
  - All files removed
  - Registry entries removed
  - Shortcuts removed

---

## 📝 Documentation

- [ ] **README.md updated**
  - Installation instructions
  - System requirements
  - Known issues

- [ ] **Version number updated**
  - In ModStackSetup.iss
  - In application assembly info
  - In README.md

- [ ] **Changelog created**
  - List new features
  - List bug fixes
  - List breaking changes

---

## 🔒 Security & Signing (Optional but Recommended)

- [ ] **Code signing certificate obtained**
  - From trusted CA (e.g., DigiCert, Sectigo)
  - Or self-signed for internal use

- [ ] **Installer signed**
  - Use SignTool.exe
  - Or configure in Inno Setup

- [ ] **Virus scan completed**
  - Scan with antivirus
  - Upload to VirusTotal.com for verification

---

## 🚀 Distribution

- [ ] **Installer tested on multiple machines**
  - Windows 10
  - Windows 11
  - Different user permission levels

- [ ] **Silent install tested**
  ```bash
  ModStackSetup.exe /SILENT
  ```

- [ ] **Upgrade tested**
  - Install old version
  - Install new version over it
  - Verify settings are preserved

- [ ] **Upload locations prepared**
  - GitHub Releases
  - Website download page
  - Documentation updated with download link

---

## ✅ Final Checks

- [ ] **File size reasonable**
  - Installer should be 50-200 MB
  - Not excessively large

- [ ] **Installer name descriptive**
  - Example: `ModStackSetup-v1.0.0.exe`
  - Includes version number

- [ ] **Checksums generated**
  ```bash
  certutil -hashfile ModStackSetup.exe SHA256
  ```
  - Include in release notes
  - Allows users to verify integrity

- [ ] **Release notes prepared**
  - What's new
  - Bug fixes
  - Installation instructions
  - Known issues

---

## 🎯 Ready to Release!

Once all items are checked:

1. ✅ Upload installer to distribution channels
2. ✅ Update website/documentation
3. ✅ Announce release
4. ✅ Monitor for issues

---

## 📊 Post-Release Monitoring

- [ ] Monitor user feedback
- [ ] Track installation success rate
- [ ] Check for crash reports
- [ ] Prepare patch releases if needed

---

**Good luck with your release! 🎉**
