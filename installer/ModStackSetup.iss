; ModStack Setup Script for Inno Setup
; This script creates a professional installer for the ModStack application

#define MyAppName "ModStack"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "ModStack Development Team"
#define MyAppURL "https://github.com/yourusername/modstack"
#define MyAppExeName "ModStack.GUI.exe"
#define MyAppDescription "Modular Web Development Stack Manager"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{12345678-1234-5678-9ABC-123456789012}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=license.txt
InfoBeforeFile=readme.txt
OutputDir=dist
OutputBaseFilename=ModStackSetup
; SetupIconFile=assets\icon.ico  ; Comment out - will add icon later
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Custom pages
[CustomMessages]
RootDirPageTitle=Choose Installation Directory
RootDirPageDescription=Please select the root directory where ModStack will store all its data.%n%nThis directory will contain:%n• Services (Apache, MySQL, PHP, etc.)%n• Projects%n• Logs%n• Configuration files%n%nRecommended: C:\ModStack

; Administrator requirement message
PrivilegesRequiredMessage=This application requires administrator privileges to install and manage system services. Please run the installer as administrator.

; Post-installation instructions
WelcomeLabel2=ModStack has been successfully installed!
WelcomeLabel3=To launch ModStack:
WelcomeLabel4=1. Right-click the Start Menu or Desktop shortcut
WelcomeLabel5=2. Select "Run as administrator"
WelcomeLabel6=3. Click "Yes" when prompted for elevation
WelcomeLabel7=ModStack requires administrator privileges to manage system services.

[Code]
var
  RootDirPage: TInputDirWizardPage;

function IsDirWritable(DirPath: String): Boolean;
var
  TestFile: String;
begin
  Result := False;
  try
    { Remove trailing backslash if present }
    if Copy(DirPath, Length(DirPath), 1) = '\' then
      DirPath := Copy(DirPath, 1, Length(DirPath) - 1);
    
    { Try to create a test file in the directory }
    if not DirExists(DirPath) then
    begin
      { If directory doesn't exist, check parent directory }
      DirPath := ExtractFilePath(DirPath);
      if not DirExists(DirPath) then
        Exit;
    end;
    
    TestFile := AddBackslash(DirPath) + '.modstack_test';
    if SaveStringToFile(TestFile, 'test', False) then
    begin
      DeleteFile(TestFile);
      Result := True;
    end;
  except
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  { Create the custom page }
  RootDirPage := CreateInputDirPage(wpSelectDir,
    ExpandConstant('{cm:RootDirPageTitle}'),
    ExpandConstant('{cm:RootDirPageDescription}'),
    'Please select the root directory where ModStack will store all its data:',
    False, '');
  RootDirPage.Add('');
  
  { Set default values }
  RootDirPage.Values[0] := 'C:\ModStack';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  RootDir: String;
begin
  Result := True;
  
  if CurPageID = RootDirPage.ID then
  begin
    RootDir := RootDirPage.Values[0];
    
    { Validate the root directory }
    if RootDir = '' then
    begin
      MsgBox('Please select a root directory.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
    
    { Check if directory is writable }
    if not IsDirWritable(ExtractFilePath(RootDir)) then
    begin
      if MsgBox('The selected directory may not be writable. Do you want to continue?', 
                mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
    
    { Store the root directory for later use }
    RootDir := AddBackslash(RootDir);
    RootDirPage.Values[0] := RootDir;
  end;
end;

function GetRootDirectory(Param: String): String;
begin
  Result := RootDirPage.Values[0];
end;

function GetServicesDirectory(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'Services';
end;

function GetProjectsDirectory(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'Projects';
end;

function GetLogsDirectory(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'Logs';
end;

function GetConfigDirectory(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'Config';
end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Main application files (adjust path to match your build output)
Source: "..\ModStack.GUI\bin\Release\net8.0-windows\ModStack.GUI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ModStack.GUI\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ModStack.GUI\bin\Release\net8.0-windows\*.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\ModStack.GUI\bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ModStack.GUI\bin\Release\net8.0-windows\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; Launcher script for easy admin elevation
Source: "launcher.bat"; DestDir: "{app}"; Flags: ignoreversion

; Application icon
Source: "..\ModStack.GUI\Resources\app-icon.ico"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Documentation (optional - will skip if doesn't exist)
Source: "..\README.md"; DestDir: "{app}\docs"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\USAGE_GUIDE.md"; DestDir: "{app}\docs"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\HOW_TO_RUN.md"; DestDir: "{app}\docs"; Flags: ignoreversion skipifsourcedoesntexist

; Sample configurations (optional)
Source: "..\Examples\*"; DestDir: "{app}\samples"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app-icon.ico"; Comment: "Run as Administrator - Right-click and select 'Run as administrator'"
Name: "{group}\Documentation"; Filename: "{app}\docs\README.md"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"; IconFilename: "{app}\app-icon.ico"; Comment: "Run as Administrator - Right-click and select 'Run as administrator'"

[Run]
; Removed automatic launch - user needs to run as administrator manually
; Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{code:GetRootDirectory}"
Type: dirifempty; Name: "{app}"

[Registry]
; Set the root directory in registry for the application to use
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "RootDirectory"; ValueData: "{code:GetRootDirectory}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "ServicesDirectory"; ValueData: "{code:GetServicesDirectory}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "ProjectsDirectory"; ValueData: "{code:GetProjectsDirectory}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "LogsDirectory"; ValueData: "{code:GetLogsDirectory}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "ConfigDirectory"; ValueData: "{code:GetConfigDirectory}"; Flags: uninsdeletekey

; Application settings
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "AutoStart"; ValueData: "False"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "MinimizeToTray"; ValueData: "True"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "EnableLogging"; ValueData: "True"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\ModStack"; ValueType: string; ValueName: "Theme"; ValueData: "Light"; Flags: uninsdeletekey
