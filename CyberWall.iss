; Script generated for CyberWall
; Windows Filtering Platform Application Firewall
; Built by CyberGems (https://cybergems.org)

#define AppName "CyberWall"
#define AppPublisher "CyberGems"
#define AppURL "https://cybergems.org"
#define AppExeName "CyberWall.UI.exe"

[Setup]
AppId={{C8B1E9A4-5A2C-4F71-9F3D-7E8B8B8B8B8B}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=CyberWall-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
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
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall
