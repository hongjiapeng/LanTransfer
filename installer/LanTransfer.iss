#define MyAppName "LanTransfer"
#define MyAppPublisher "LanTransfer contributors"
#define MyAppExeName "lantransfer.exe"

#ifndef AppVersion
  #define AppVersion "0.5.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh_CN"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "zh_TW"; MessagesFile: "languages\ChineseTraditional.isl"

[Setup]
AppId={{F4F3A6B5-1E24-4D67-9A8A-9C0A7A4B6D21}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/hongjiapeng/LanTransfer
AppSupportURL=https://github.com/hongjiapeng/LanTransfer/issues
AppMutex=Local\LanTransfer.SingleInstance
DefaultDirName={localappdata}\Programs\LanTransfer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
CloseApplications=yes
CloseApplicationsFilter=lantransfer.exe
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=LanTransfer-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\LanTransfer.Host\wwwroot\assets\lantransfer.ico
UninstallDisplayIcon={app}\wwwroot\assets\lantransfer.ico

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartOnSignIn}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LanTransfer"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\LanTransfer"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\LanTransfer"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchLanTransfer}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
en.CreateDesktopIcon=Create a desktop shortcut
en.AdditionalShortcuts=Additional shortcuts:
en.StartOnSignIn=Start LanTransfer when I sign in to Windows
en.AdditionalOptions=Additional options:
en.LaunchLanTransfer=Launch LanTransfer
zh_CN.CreateDesktopIcon=创建桌面快捷方式
zh_CN.AdditionalShortcuts=附加快捷方式：
zh_CN.StartOnSignIn=登录 Windows 后启动 LanTransfer
zh_CN.AdditionalOptions=附加选项：
zh_CN.LaunchLanTransfer=启动 LanTransfer
zh_TW.CreateDesktopIcon=建立桌面捷徑
zh_TW.AdditionalShortcuts=其他捷徑：
zh_TW.StartOnSignIn=登入 Windows 後啟動 LanTransfer
zh_TW.AdditionalOptions=其他選項：
zh_TW.LaunchLanTransfer=啟動 LanTransfer
