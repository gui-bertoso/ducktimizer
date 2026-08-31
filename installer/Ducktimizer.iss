#define MyAppName "Ducktimizer Pro"
#define MyAppVersion "3.4.0"
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
LicenseFile=INSTALL_NOTICE.txt
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
RestartApplications=no
AppMutex=Local\Ducktimizer.SingleInstance
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador do Ducktimizer Pro
VersionInfoProductName={#MyAppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "..\outputs\publish\Ducktimizer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PatoLizoOptimizer.Avalonia\DEPENDENCY-LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Ducktimizer Pro"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Ducktimizer Pro"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Ducktimizer Pro"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure InitializeWizard();
begin
  if not WizardSilent then
    MsgBox('A licença do Ducktimizer será vinculada automaticamente a este computador na primeira ativação. Depois disso, a mesma chave não poderá ativar outro dispositivo.', mbInformation, MB_OK);
end;
