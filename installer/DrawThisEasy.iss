; Inno Setup script for DrawThisEasy
; Compile with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\DrawThisEasy.iss
; Produces installer\Output\DrawThisEasy-Setup.exe

#define AppName "DrawThisEasy"
#define AppVersion "1.2.0"
#define AppPublisher "Roberto Renz"
#define AppExeName "DrawThisEasy.exe"

[Setup]
AppId={{8F3A1C7E-4B2D-4E9A-9C1F-DRAWTHISEASY01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-machine install (Program Files) — needs admin. Use lowest for per-user.
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=DrawThisEasy-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The self-contained single-file exe staged in the run folder
Source: "..\run\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
