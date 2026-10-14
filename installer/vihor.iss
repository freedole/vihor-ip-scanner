; Vihor IP Scanner — Inno Setup script
; Build:  iscc /DAppVersion=0.1.0 installer\vihor.iss
; Requires Inno Setup 6.5 or newer (language files in .\Languages are from Inno Setup 6.5).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppName      "Vihor IP Scanner"
#define AppExe       "VihorIPScanner.exe"
#define AppPublisher "cyber-security.ba"
#define AppURL       "https://cyber-security.ba"
#define RepoURL      "https://github.com/freedole/vihor-ip-scanner"
#define BinDir       "..\src\Vihor\bin\Release\net48"

[Setup]
AppId={{BEB7B594-4657-411D-9C23-44D4919A96E0}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#RepoURL}/issues
AppUpdatesURL={#RepoURL}/releases
AppCopyright=Copyright (C) 2026 Đorđe Kaurinović, cyber-security.ba
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible x64compatible arm64
ArchitecturesInstallIn64BitMode=x64compatible arm64
MinVersion=6.1sp1
OutputDir=..\dist
OutputBaseFilename=VihorIPScanner-Setup-{#AppVersion}
SetupIconFile=..\src\Vihor\Resources\vihor.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
; Language selection: English is the default (first entry), no auto-detection from the OS
ShowLanguageDialog=yes
LanguageDetectionMethod=none
MissingMessagesWarning=no
NotRecognizedMessagesWarning=no

[Languages]
; Order here = order in the language selection list
Name: "english";      MessagesFile: "Languages\Default.isl"
Name: "serbianlatin"; MessagesFile: "Languages\SerbianLatin.isl"
Name: "croatian";     MessagesFile: "Languages\Croatian.isl"
Name: "bosnian";      MessagesFile: "Languages\Bosnian.isl"
Name: "albanian"; MessagesFile: "Languages\Albanian.isl"
Name: "arabic"; MessagesFile: "Languages\Arabic.isl"
Name: "armenian"; MessagesFile: "Languages\Armenian.isl"
Name: "brazilianportuguese"; MessagesFile: "Languages\BrazilianPortuguese.isl"
Name: "bulgarian"; MessagesFile: "Languages\Bulgarian.isl"
Name: "catalan"; MessagesFile: "Languages\Catalan.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "chinesetraditional"; MessagesFile: "Languages\ChineseTraditional.isl"
Name: "corsican"; MessagesFile: "Languages\Corsican.isl"
Name: "czech"; MessagesFile: "Languages\Czech.isl"
Name: "danish"; MessagesFile: "Languages\Danish.isl"
Name: "dutch"; MessagesFile: "Languages\Dutch.isl"
Name: "finnish"; MessagesFile: "Languages\Finnish.isl"
Name: "french"; MessagesFile: "Languages\French.isl"
Name: "german"; MessagesFile: "Languages\German.isl"
Name: "greek"; MessagesFile: "Languages\Greek.isl"
Name: "hebrew"; MessagesFile: "Languages\Hebrew.isl"
Name: "hungarian"; MessagesFile: "Languages\Hungarian.isl"
Name: "icelandic"; MessagesFile: "Languages\Icelandic.isl"
Name: "indonesian"; MessagesFile: "Languages\Indonesian.isl"
Name: "italian"; MessagesFile: "Languages\Italian.isl"
Name: "japanese"; MessagesFile: "Languages\Japanese.isl"
Name: "korean"; MessagesFile: "Languages\Korean.isl"
Name: "latvian"; MessagesFile: "Languages\Latvian.isl"
Name: "lithuanian"; MessagesFile: "Languages\Lithuanian.isl"
Name: "norwegian"; MessagesFile: "Languages\Norwegian.isl"
Name: "polish"; MessagesFile: "Languages\Polish.isl"
Name: "portuguese"; MessagesFile: "Languages\Portuguese.isl"
Name: "romanian"; MessagesFile: "Languages\Romanian.isl"
Name: "russian"; MessagesFile: "Languages\Russian.isl"
Name: "serbiancyrillic"; MessagesFile: "Languages\SerbianCyrillic.isl"
Name: "slovak"; MessagesFile: "Languages\Slovak.isl"
Name: "slovenian"; MessagesFile: "Languages\Slovenian.isl"
Name: "spanish"; MessagesFile: "Languages\Spanish.isl"
Name: "swedish"; MessagesFile: "Languages\Swedish.isl"
Name: "tamil"; MessagesFile: "Languages\Tamil.isl"
Name: "thai"; MessagesFile: "Languages\Thai.isl"
Name: "turkish"; MessagesFile: "Languages\Turkish.isl"
Name: "ukrainian"; MessagesFile: "Languages\Ukrainian.isl"
Name: "vietnamese"; MessagesFile: "Languages\Vietnamese.isl"

[CustomMessages]
NetMissing=Vihor IP Scanner requires Microsoft .NET Framework 4.8, which is not installed on this computer.%n%nOpen the download page now?
serbianlatin.NetMissing=Vihor IP Scanner zahteva Microsoft .NET Framework 4.8, koji nije instaliran na ovom računaru.%n%nOtvoriti stranicu za preuzimanje?
croatian.NetMissing=Vihor IP Scanner zahtijeva Microsoft .NET Framework 4.8, koji nije instaliran na ovom računalu.%n%nOtvoriti stranicu za preuzimanje?
bosnian.NetMissing=Vihor IP Scanner zahtijeva Microsoft .NET Framework 4.8, koji nije instaliran na ovom računaru.%n%nOtvoriti stranicu za preuzimanje?

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BinDir}\{#AppExe}";        DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";                 DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[INI]
; Language chosen in the installer becomes the app's default UI language
Filename: "{app}\vihor.ini"; Section: "Settings"; Key: "Language"; String: "{language}"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\vihor.ini"

[Code]
function IsDotNet48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
            and (Release >= 528040);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet48Installed() then
  begin
    if SuppressibleMsgBox(CustomMessage('NetMissing'), mbError, MB_YESNO, IDNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;
