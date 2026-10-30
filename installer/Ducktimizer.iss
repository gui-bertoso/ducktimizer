#define MyAppName "Ducktimizer"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "patoLizo"
#define MyAppExeName "Ducktimizer.exe"

[Setup]
AppId={{7C152A4E-D2DB-47F8-9874-BC0CCAF70AA7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Ducktimizer
DefaultGroupName=Ducktimizer
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
InfoBeforeFile=INSTALL_NOTICE.txt
OutputDir=..\outputs\installer
OutputBaseFilename=Ducktimizer-Setup-{#MyAppVersion}-win-x64
SetupIconFile=..\PatoLizoOptimizer.Avalonia\Assets\logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=yes
AppMutex=Local\Ducktimizer.SingleInstance
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador do Ducktimizer
VersionInfoProductName={#MyAppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\outputs\publish\Ducktimizer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PatoLizoOptimizer.Avalonia\DEPENDENCY-LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Ducktimizer"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Ducktimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Ducktimizer"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
