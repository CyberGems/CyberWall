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

spanish.CreateShortcutGroup=Crear accesos directos:
spanish.CyberWallShortcut=CyberWall (Firewall)
spanish.OptionsGroup=Opciones:
spanish.RunAtStartup=Ejecutar CyberWall al iniciar Windows

[Tasks]
Name: "desktopicon"; Description: "{cm:CyberWallShortcut}"; GroupDescription: "{cm:CreateShortcutGroup}"
Name: "startup"; Description: "{cm:RunAtStartup}"; GroupDescription: "{cm:OptionsGroup}"

[Files]
Source: ".\publish-win64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\CyberWall.ico"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; IconFilename: "{app}\Assets\CyberWall.ico"
; Startup is managed via Task Scheduler in [Run] for admin elevation

[Run]
Filename: "schtasks"; Parameters: "create /tn ""CyberWall"" /tr ""'{app}\{#AppExeName}' --minimized"" /sc onlogon /rl highest /f"; Tasks: startup; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""CyberWall-Allow-CyberWall"" dir=out action=allow program=""{app}\{#AppExeName}"" enable=yes profile=any"; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""CyberWall-Allow-CyberWall-in"" dir=in action=allow program=""{app}\{#AppExeName}"" enable=yes profile=any"; Flags: runhidden
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall shellexec runascurrentuser

[UninstallRun]
; Restore the firewall policy first: the app may have left the machine in default-deny.
Filename: "netsh"; Parameters: "advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound"; Flags: runhidden; RunOnceId: "RestoreFirewallPolicy"
Filename: "schtasks"; Parameters: "delete /tn ""CyberWall"" /f"; Flags: runhidden; RunOnceId: "RemoveStartupTask"
Filename: "schtasks"; Parameters: "delete /tn ""CyberWall Recovery"" /f"; Flags: runhidden; RunOnceId: "RemoveRecoveryTask"
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -WindowStyle Hidden -Command ""Get-NetFirewallRule -DisplayName 'CyberWall-*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "RemoveFirewallRules"
