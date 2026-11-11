; Build with scripts/build.ps1 -Installer. AppId must stay stable across upgrades.
#ifndef AppVersion
  #error AppVersion must be supplied by the build script
#endif

[Setup]
AppId={{E0C54F92-1958-46F9-A59A-B561D23C17F2}
AppName=ScreenShot
AppVersion={#AppVersion}
AppPublisher=ScreenShot
AppPublisherURL=https://github.com/burakistikam/ScreenShot
AppSupportURL=https://github.com/burakistikam/ScreenShot/issues
AppUpdatesURL=https://github.com/burakistikam/ScreenShot/releases
DefaultDirName={localappdata}\Programs\ScreenShot
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
WizardStyle=modern
SetupIconFile=..\src\ScreenShot\Assets\app.ico
UninstallDisplayIcon={app}\ScreenShot.exe
OutputDir=..\artifacts
OutputBaseFilename=ScreenShot-{#AppVersion}-Setup
Compression=lzma2
SolidCompression=yes
AppMutex=Local\ScreenShot.Desktop.v1
CloseApplications=no
RestartApplications=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start ScreenShot in the background when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "..\artifacts\win-x64\ScreenShot.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\win-x64\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\win-x64\docs\images\*.png"; DestDir: "{app}\docs\images"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\ScreenShot"; Filename: "{app}\ScreenShot.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\ScreenShot"; Filename: "{app}\ScreenShot.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\ScreenShot"; Filename: "{app}\ScreenShot.exe"; Parameters: "--background"; WorkingDir: "{app}"; Tasks: startup

[InstallDelete]
Type: files; Name: "{userstartup}\ScreenShot.lnk"; Check: not WizardIsTaskSelected('startup')
Type: files; Name: "{userdesktop}\ScreenShot.lnk"; Check: not WizardIsTaskSelected('desktopicon')

[Run]
Filename: "{app}\ScreenShot.exe"; Parameters: "--background"; Description: "Start ScreenShot in the background"; Flags: nowait postinstall skipifsilent
