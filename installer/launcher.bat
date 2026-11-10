@echo off
REM ModStack Launcher - Automatically requests administrator privileges
REM This script ensures ModStack always runs with proper elevation

title ModStack Launcher

echo Starting ModStack...
echo.

REM Check if ModStack.exe exists
if not exist "ModStack.GUI.exe" (
    echo ERROR: ModStack.GUI.exe not found!
    echo Please ensure this launcher is in the same directory as ModStack.GUI.exe
    echo.
    pause
    exit /b 1
)

REM Launch ModStack with administrator privileges
powershell -Command "Start-Process -FilePath 'ModStack.GUI.exe' -Verb RunAs"

REM If PowerShell method fails, try alternative
if %ERRORLEVEL% NEQ 0 (
    echo PowerShell method failed, trying alternative...
    echo Please run ModStack.GUI.exe as administrator manually.
    echo.
    pause
)
