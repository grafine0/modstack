@echo off
echo Building ModStack Installer...
echo.

REM Check if Inno Setup is installed
set INNO_SETUP_PATH=""
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set INNO_SETUP_PATH="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set INNO_SETUP_PATH="C:\Program Files\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" (
    set INNO_SETUP_PATH="C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 5\ISCC.exe" (
    set INNO_SETUP_PATH="C:\Program Files\Inno Setup 5\ISCC.exe"
) else (
    echo ERROR: Inno Setup not found!
    echo Please install Inno Setup from: https://jrsoftware.org/isinfo.php
    echo.
    pause
    exit /b 1
)

echo Found Inno Setup at: %INNO_SETUP_PATH%
echo.

REM Check if build output exists
if not exist "..\ModStack.GUI\bin\Release\net8.0-windows\ModStack.GUI.exe" (
    echo ERROR: Build output not found!
    echo Please build the application first:
    echo   dotnet build ModStack.sln --configuration Release
    echo.
    pause
    exit /b 1
)

echo Build output found. Creating installer...
echo.

REM Create dist directory if it doesn't exist
if not exist "dist" mkdir dist

REM Run Inno Setup compiler
%INNO_SETUP_PATH% "ModStackSetup.iss"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo SUCCESS: Installer created successfully!
    echo Output: dist\ModStackSetup.exe
    echo.
    echo You can now distribute this installer to users.
) else (
    echo.
    echo ERROR: Installer creation failed!
    echo Please check the Inno Setup script for errors.
)

echo.
pause
