@echo off
echo ================================================
echo ModStack Installer - Administrator Required
echo ================================================
echo.
echo This installer requires administrator privileges
echo to install system services and manage ports.
echo.
echo Windows will prompt you for administrator access.
echo Please click "Yes" when prompted.
echo.
pause
echo.
echo Starting ModStack installer...
echo.

REM Check if installer exists
if not exist "dist\ModStackSetup.exe" (
    echo ERROR: Installer not found!
    echo Please run build-installer.bat first.
    echo.
    pause
    exit /b 1
)

REM Run the installer with elevation
powershell -Command "Start-Process -FilePath 'dist\ModStackSetup.exe' -Verb RunAs"

echo.
echo Installer launched with administrator privileges.
echo If you don't see the installer window, check your taskbar.
echo.
pause
