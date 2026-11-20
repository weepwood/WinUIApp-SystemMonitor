; ISCC /DAppVersion=1.2.0 Packaging\SystemMonitor.iss (after dotnet publish -o portable)
[Setup]
AppId=WinUIApp-SystemMonitor
AppName=System Monitor
AppPublisher=weepwood
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0
DefaultDirName={localappdata}\Programs\WinUIApp-SystemMonitor
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\installer
OutputBaseFilename=SystemMonitor-v{#AppVersion}-win-x64-setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\WinUIApp.exe
DisableProgramGroupPage=yes

[Files]
Source: "..\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\System Monitor"; Filename: "{app}\WinUIApp.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\WinUIApp.exe"; WorkingDir: "{app}"; Description: "Launch System Monitor"; Flags: postinstall nowait skipifsilent
