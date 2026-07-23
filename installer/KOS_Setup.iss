#define MyAppName "KOS"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "강산건축디자인"
#define MyAppExeName "KOS.exe"

[Setup]
AppId={{7E4C72D1-8C98-482A-AB1B-23C24B52DF26}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\KOS
DefaultGroupName=KOS
OutputDir=..\artifacts
OutputBaseFilename=KOS_Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName=KOS
SetupLogging=yes

[Files]
Source: "..\publish\KOS\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 KOS 바로가기 만들기"; GroupDescription: "추가 아이콘:"; Flags: checkedonce

[Icons]
Name: "{autoprograms}\KOS"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\KOS"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "KOS 실행"; Flags: nowait postinstall skipifsilent
