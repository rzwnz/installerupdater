; ============================================================
; InstallerService Inno Setup Installer Script
; ============================================================
; Handles: service installation, registry keys, file permissions,
;          database migrations, and Windows service registration.
; ============================================================

#define MyAppName "InstallerService"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "rzwnz"
#define MyAppURL "https://github.com/rzwnz"
#define MyAppExeName "InstallerService.exe"
#define MyUpdaterExeName "InstallerUpdater.exe"
#define ServiceName "InstallerService"
#define UpdaterServiceName "InstallerUpdater"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\output
OutputBaseFilename=InstallerUpdaterSetup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
SetupLogging=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Types]
Name: "full"; Description: "Full installation (service + updater)"
Name: "serviceonly"; Description: "Service only (no auto-updater)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "service"; Description: "InstallerService (main service)"; Types: full serviceonly custom; Flags: fixed
Name: "updater"; Description: "InstallerUpdater (automatic updater)"; Types: full

[Files]
; Main service binaries
Source: "..\publish\InstallerService\*"; DestDir: "{app}"; Components: service; Flags: ignoreversion recursesubdirs
; Updater binaries
Source: "..\publish\InstallerUpdater\*"; DestDir: "{app}\updater"; Components: updater; Flags: ignoreversion recursesubdirs
; Migration scripts
Source: "..\src\InstallerService\Migrations\*.sql"; DestDir: "{commonappdata}\InstallerUpdater\migrations\sqlite"; Flags: ignoreversion
Source: "..\src\InstallerService\Migrations\*.sql"; DestDir: "{commonappdata}\InstallerUpdater\migrations\postgres"; Flags: ignoreversion
; Configuration templates
Source: "..\src\InstallerService\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "..\src\InstallerUpdater\appsettings.json"; DestDir: "{app}\updater"; Components: updater; Flags: onlyifdoesntexist

[Dirs]
Name: "{commonappdata}\InstallerUpdater"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\logs"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\updates"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\migrations"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\migrations\sqlite"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\migrations\postgres"; Permissions: admins-full system-full
Name: "{commonappdata}\InstallerUpdater\backup"; Permissions: admins-full system-full

[Registry]
; Service configuration registry keys
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; ValueType: string; ValueName: "DataPath"; ValueData: "{commonappdata}\InstallerUpdater"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; ValueType: string; ValueName: "Status"; ValueData: "Installed"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; ValueType: dword; ValueName: "AutoUpdate"; ValueData: "1"; Components: updater; Flags: uninsdeletevalue
; Uninstall: delete the entire key tree
Root: HKLM; Subkey: "SOFTWARE\rzwnz\InstallerService"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\rzwnz"; Flags: uninsdeletekeyifempty

[Run]
; Run database migrations after installation
Filename: "{app}\{#MyAppExeName}"; Parameters: "--migrate"; StatusMsg: "Running database migrations..."; Flags: runhidden waituntilterminated
; Start the main service
Filename: "sc.exe"; Parameters: "start {#ServiceName}"; StatusMsg: "Starting {#MyAppName}..."; Flags: runhidden waituntilterminated
; Start the updater service (if installed)
Filename: "sc.exe"; Parameters: "start {#UpdaterServiceName}"; Components: updater; StatusMsg: "Starting {#UpdaterServiceName}..."; Flags: runhidden waituntilterminated

[UninstallRun]
; Stop services before uninstall
Filename: "sc.exe"; Parameters: "stop {#UpdaterServiceName}"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated
; Small delay to let services stop
Filename: "cmd.exe"; Parameters: "/c timeout /t 3 /nobreak >nul"; Flags: runhidden waituntilterminated
; Remove services
Filename: "sc.exe"; Parameters: "delete {#UpdaterServiceName}"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated

[Code]
const
  SERVICE_WIN32_OWN_PROCESS = $10;
  SERVICE_AUTO_START = $2;
  SERVICE_DEMAND_START = $3;
  SERVICE_ERROR_NORMAL = $1;

// Forward declarations
function ServiceExists(ServiceName: string): Boolean; forward;
procedure InstallWindowsService(ServiceName, DisplayName, BinaryPath, Description: string; AutoStart: Boolean); forward;
procedure RemoveWindowsService(ServiceName: string); forward;
procedure StopWindowsService(ServiceName: string); forward;

function ServiceExists(ServiceName: string): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', 'query ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
            and (ResultCode = 0);
end;

procedure StopWindowsService(ServiceName: string);
var
  ResultCode: Integer;
begin
  if ServiceExists(ServiceName) then
  begin
    Exec('sc.exe', 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000); // Wait for service to stop
  end;
end;

procedure InstallWindowsService(ServiceName, DisplayName, BinaryPath, Description: string; AutoStart: Boolean);
var
  ResultCode: Integer;
  StartType: string;
begin
  if AutoStart then
    StartType := 'auto'
  else
    StartType := 'demand';

  // Remove existing service if present
  if ServiceExists(ServiceName) then
    RemoveWindowsService(ServiceName);

  // Create the service
  Exec('sc.exe',
    'create ' + ServiceName +
    ' binpath= "' + BinaryPath + '"' +
    ' start= ' + StartType +
    ' DisplayName= "' + DisplayName + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if ResultCode <> 0 then
    MsgBox('Failed to create service ' + ServiceName + '. Error code: ' + IntToStr(ResultCode), mbError, MB_OK);

  // Set description
  Exec('sc.exe',
    'description ' + ServiceName + ' "' + Description + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Configure recovery: restart on failure
  Exec('sc.exe',
    'failure ' + ServiceName + ' reset= 60 actions= restart/5000/restart/10000/restart/30000',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure RemoveWindowsService(ServiceName: string);
var
  ResultCode: Integer;
begin
  StopWindowsService(ServiceName);
  Exec('sc.exe', 'delete ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Install InstallerService as a Windows service
    InstallWindowsService(
      '{#ServiceName}',
      'Installer Service',
      ExpandConstant('{app}\{#MyAppExeName}'),
      'Installer local Windows service for Tomcat communication and data management',
      True);

    // Install InstallerUpdater as a Windows service (if component selected)
    if IsComponentSelected('updater') then
    begin
      InstallWindowsService(
        '{#UpdaterServiceName}',
        'Installer Updater',
        ExpandConstant('{app}\updater\{#MyUpdaterExeName}'),
        'Automatic update service for Installer - polls the server for new versions',
        True);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Stop and remove services
    RemoveWindowsService('{#UpdaterServiceName}');
    RemoveWindowsService('{#ServiceName}');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    // Optionally clean up data directory
    if MsgBox('Do you want to remove all InstallerService data (logs, database, etc.)?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{commonappdata}\InstallerUpdater'), True, True, True);
    end;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;

  // Stop existing services before upgrade
  if ServiceExists('{#UpdaterServiceName}') then
    StopWindowsService('{#UpdaterServiceName}');

  if ServiceExists('{#ServiceName}') then
    StopWindowsService('{#ServiceName}');
end;
