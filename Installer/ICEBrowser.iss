#define MyAppName "ICE BROWSER"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ICE Browser"
#define MyAppExeName "ICEBrowser.exe"

[Setup]
AppId={{B1E1A7C1-1C19-4B91-8F8E-1CEB00001000}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ICE Browser
DefaultGroupName=ICE BROWSER
OutputDir=..\dist
OutputBaseFilename=ICE-BROWSER-Setup-1.0.0-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\ICE BROWSER"; Filename: "{app}\ICEBrowser.exe"
Name: "{autodesktop}\ICE BROWSER"; Filename: "{app}\ICEBrowser.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\ICEBrowser.exe"; Description: "Launch ICE BROWSER"; Flags: postinstall nowait skipifsilent
