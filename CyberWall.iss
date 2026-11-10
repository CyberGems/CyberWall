; Script generated for CyberWall
; Windows Filtering Platform Application Firewall
; Built by CyberGems (https://cybergems.org)

#define AppName "CyberWall"
#define AppPublisher "CyberGems"
#define AppURL "https://cybergems.org"
#define AppExeName "CyberWall.exe"

[Setup]
AppId={{C8B1E9A4-5A2C-4F71-9F3D-7E8B8B8B8B8B}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=CyberWall-Setup-{#AppVersion}
SetupIconFile=.\src\CyberWall.UI\Assets\CyberWall.ico
WizardSmallImageFile=.\installer\installerWizardSmall.bmp
WizardImageFile=.\installer\installerWizard.bmp
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
english.CreateShortcutGroup=Create shortcuts:
english.CyberWallShortcut=CyberWall (Firewall)
english.OptionsGroup=Options:
english.RunAtStartup=Run CyberWall when Windows starts
english.CloseBeforeUninstall=CyberWall is still running. Choose Exit from the CyberWall icon menu in the notification area, then click Retry.
english.StopServiceBeforeUninstall=The CyberWall Firewall service is still running. Stop it in the Windows Services app, then click Retry.
english.ServiceStatusUnknownBeforeUninstall=The uninstaller could not verify that the CyberWall Firewall service has stopped. Check its status in the Windows Services app, then click Retry.

spanish.CreateShortcutGroup=Crear accesos directos:
spanish.CyberWallShortcut=CyberWall (Firewall)
spanish.OptionsGroup=Opciones:
spanish.RunAtStartup=Ejecutar CyberWall al iniciar Windows
spanish.CloseBeforeUninstall=CyberWall sigue abierto. Elige Salir en el menú del icono de CyberWall en la bandeja del sistema y haz clic en Reintentar.
spanish.StopServiceBeforeUninstall=El servicio CyberWall Firewall sigue en ejecución. Detenlo en la aplicación Servicios de Windows y haz clic en Reintentar.
spanish.ServiceStatusUnknownBeforeUninstall=El desinstalador no pudo comprobar que el servicio CyberWall Firewall se haya detenido. Revisa su estado en la aplicación Servicios de Windows y haz clic en Reintentar.

[Tasks]
Name: "desktopicon"; Description: "{cm:CyberWallShortcut}"; GroupDescription: "{cm:CreateShortcutGroup}"
Name: "startup"; Description: "{cm:RunAtStartup}"; GroupDescription: "{cm:OptionsGroup}"

[Files]
Source: ".\publish-win64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\CyberWall.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\CyberWall.ico"
; Startup uses a Task Scheduler entry so the app can start elevated without a UAC prompt

[Registry]
; Clean up any legacy AppCompatFlags from older installers that forced HIGHDPIAWARE (locks app to System 96 DPI)
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: none; ValueName: "{app}\{#AppExeName}"; Flags: deletevalue uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; ValueType: none; ValueName: "{app}\{#AppExeName}"; Flags: deletevalue uninsdeletevalue

[Run]
; The startup task and the self-allow firewall rules are created by the application:
; spawning schtasks.exe or netsh.exe from the installer is what makes heuristic
; engines read the setup as a dropper.
Filename: "{app}\{#AppExeName}"; Parameters: "--register-startup"; Tasks: startup; Flags: runhidden
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall shellexec runascurrentuser

[UninstallRun]
; Restore the firewall policy first: the app may have left the machine in default-deny.
Filename: "netsh"; Parameters: "advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound"; Flags: runhidden; RunOnceId: "RestoreFirewallPolicy"
Filename: "schtasks"; Parameters: "delete /tn ""CyberWall"" /f"; Flags: runhidden; RunOnceId: "RemoveStartupTask"
Filename: "schtasks"; Parameters: "delete /tn ""CyberWall Recovery"" /f"; Flags: runhidden; RunOnceId: "RemoveRecoveryTask"
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -WindowStyle Hidden -Command ""Get-NetFirewallRule -DisplayName 'CyberWall-*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "RemoveFirewallRules"

[Code]
const
  SC_MANAGER_CONNECT = 1;
  SERVICE_QUERY_STATUS = 4;
  SERVICE_STOPPED = 1;
  ERROR_SERVICE_DOES_NOT_EXIST = 1060;

type
  TServiceStatus = record
    ServiceType: Cardinal;
    CurrentState: Cardinal;
    ControlsAccepted: Cardinal;
    Win32ExitCode: Cardinal;
    ServiceSpecificExitCode: Cardinal;
    CheckPoint: Cardinal;
    WaitHint: Cardinal;
  end;

function OpenSCManager(MachineName, DatabaseName: LongWord; DesiredAccess: Cardinal): THandle;
  external 'OpenSCManagerW@advapi32.dll stdcall uninstallonly';
function OpenService(Manager: THandle; ServiceName: String; DesiredAccess: Cardinal): THandle;
  external 'OpenServiceW@advapi32.dll stdcall uninstallonly';
function QueryServiceStatus(Service: THandle; var Status: TServiceStatus): Integer;
  external 'QueryServiceStatus@advapi32.dll stdcall uninstallonly';
function CloseServiceHandle(Handle: THandle): Integer;
  external 'CloseServiceHandle@advapi32.dll stdcall uninstallonly';

function CyberWallServiceState: Cardinal;
var
  Manager, Service: THandle;
  Status: TServiceStatus;
  ErrorCode: Integer;
begin
  { Zero means the status could not be verified. }
  Result := 0;
  Manager := OpenSCManager(0, 0, SC_MANAGER_CONNECT);
  if Manager = 0 then
  begin
    Log('Could not open the Service Control Manager. Error ' + IntToStr(DLLGetLastError));
    Exit;
  end;

  try
    Service := OpenService(Manager, 'CyberWall', SERVICE_QUERY_STATUS);
    if Service = 0 then
    begin
      ErrorCode := DLLGetLastError;
      if ErrorCode = ERROR_SERVICE_DOES_NOT_EXIST then
        Result := SERVICE_STOPPED
      else
        Log('Could not open the CyberWall service. Error ' + IntToStr(ErrorCode));
      Exit;
    end;

    try
      if QueryServiceStatus(Service, Status) <> 0 then
        Result := Status.CurrentState
      else
        Log('Could not query the CyberWall service. Error ' + IntToStr(DLLGetLastError));
    finally
      CloseServiceHandle(Service);
    end;
  finally
    CloseServiceHandle(Manager);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  BlockMessage: String;
  ServiceState: Cardinal;
begin
  if CurUninstallStep <> usUninstall then
    Exit;

  { Wait for the desktop app and the optional service before running firewall cleanup. }
  while True do
  begin
    if CheckForMutexes('Global\CyberWall_SingleInstance_Mutex') then
      BlockMessage := CustomMessage('CloseBeforeUninstall')
    else
    begin
      ServiceState := CyberWallServiceState;
      if ServiceState = SERVICE_STOPPED then
        Exit;
      if ServiceState = 0 then
        BlockMessage := CustomMessage('ServiceStatusUnknownBeforeUninstall')
      else
        BlockMessage := CustomMessage('StopServiceBeforeUninstall');
    end;

    Log(BlockMessage);
    if UninstallSilent then
      Abort;
    if MsgBox(BlockMessage, mbInformation, MB_RETRYCANCEL) <> IDRETRY then
      Abort;
  end;
end;
