#define MyAppName "ClipVault"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ClipVault"
#define MyAppExeName "ClipVault.App.exe"

[Setup]
AppId={{D425290E-5F38-4A02-8B59-B332AA269EE5}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=ClipVault installer
VersionInfoProductName={#MyAppName}
DefaultDirName={localappdata}\Programs\ClipVault
DefaultGroupName=ClipVault
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts\installer
OutputBaseFilename=ClipVault-Setup-{#MyAppVersion}-x64
SetupIconFile=..\src\ClipVault.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
ChangesEnvironment=no
UseSetupLdr=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\ClipVault-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ClipVault"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\ClipVault"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch ClipVault"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Deliberately retain %LOCALAPPDATA%\ClipVault so uninstalling cannot silently erase clipboard history.
Type: filesandordirs; Name: "{app}"
