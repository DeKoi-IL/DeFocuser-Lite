; FocuserApp installer -- DeFocuser Lite (WPF)
; Bundles the ASCOM driver DLL + the WPF mediator app.
; SourceDir resolves to the repository root (3 levels up from this file).
; Build script (build.ps1) injects MyAppVersion via /D before invoking ISCC.

#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif
#define MyAppPublisher "DeKoi"
#define MyAppName "DeFocuser Lite"

[Setup]
AppId={{2760CB5C-EDA1-41F8-BCD1-CE6E1A8B673B}
AppName={#MyAppPublisher}'s {#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DisableProgramGroupPage=yes
Compression=lzma
SolidCompression=yes
UninstallFilesDir="{app}\Uninstall"
SourceDir="..\..\.."
OutputDir="Installer"
OutputBaseFilename={#MyAppPublisher} {#MyAppName} Setup-{#MyAppVersion}
WizardImageFile="Code\Installer-Legacy\WizardImageFile.bmp"
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; ASCOM Driver DLL (thin IPC proxy)
Source: "Code\ASCOM_Driver\bin\Release\ASCOM.DeKoi.DeFocuserLite.dll"; DestDir: "{app}"
; FocuserApp WPF Controller EXE (owns serial connection, serves ASCOM clients)
Source: "Code\FocuserApp\bin\x64\Release\ASCOM.DeKoi.DeFocuserApp.exe"; DestDir: "{app}"
Source: "Code\FocuserApp\bin\x64\Release\ASCOM.DeKoi.DeFocuserApp.exe.config"; DestDir: "{app}"
; esptool.exe — used by the hub to flash ESP32-C3 firmware updates over serial.
Source: "Tools\esptool\esptool.exe"; DestDir: "{app}\tools"; Flags: skipifsourcedoesntexist

[Icons]
Name: "{group}\DeFocuser Lite Controller"; Filename: "{app}\ASCOM.DeKoi.DeFocuserApp.exe"
Name: "{commondesktop}\DeFocuser Lite Controller"; Filename: "{app}\ASCOM.DeKoi.DeFocuserApp.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; Register COM for 32-bit
Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 32bit
; Register COM for 64-bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64
; Relaunch after install. Split into two entries because Inno's 'postinstall'
; checkbox never renders during /SILENT installs (so the box is effectively
; unchecked and the app wouldn't relaunch). The hub's auto-update flow uses
; /SILENT, so we need a second unconditional [Run] guarded by WizardSilent.
Filename: "{app}\ASCOM.DeKoi.DeFocuserApp.exe"; Description: "Launch {#MyAppPublisher}'s {#MyAppName}"; Flags: nowait postinstall runascurrentuser; Check: not WizardSilent
Filename: "{app}\ASCOM.DeKoi.DeFocuserApp.exe"; Flags: nowait runascurrentuser; Check: WizardSilent

[UninstallRun]
Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 32bit; RunOnceId: "RegasmUnreg32"
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64; RunOnceId: "RegasmReg64"
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64; RunOnceId: "RegasmUnreg64"

[Code]
const REQUIRED_PLATFORM_VERSION = 6.2;

function PlatformVersion(): Double;
var
   PlatVerString : String;
begin
   Result := 0.0;
   try
      if RegQueryStringValue(HKEY_LOCAL_MACHINE_32, 'Software\ASCOM','PlatformVersion', PlatVerString) then
      begin
         Result := StrToFloat(PlatVerString);
      end;
   except
      ShowExceptionMessage;
      Result:= -1.0;
   end;
end;

function InitializeSetup(): Boolean;
var
   PlatformVersionNumber : double;
 begin
   Result := FALSE;
   PlatformVersionNumber := PlatformVersion();
   If PlatformVersionNumber >= REQUIRED_PLATFORM_VERSION then
      Result := TRUE
   else
      if PlatformVersionNumber = 0.0 then
         MsgBox('No ASCOM Platform is installed. Please install Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later from https://www.ascom-standards.org', mbCriticalError, MB_OK)
      else
         MsgBox('ASCOM Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later is required, but Platform '+ Format('%3.1f', [PlatformVersionNumber]) + ' is installed. Please install the latest Platform before continuing; you will find it at https://www.ascom-standards.org', mbCriticalError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UninstallExe: String;
  UninstallRegistry: String;
begin
  if (CurStep = ssInstall) then
    begin
      UninstallRegistry := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}' + '_is1');
      if RegQueryStringValue(HKLM, UninstallRegistry, 'UninstallString', UninstallExe) then
        begin
          MsgBox('Setup will now remove the previous version.', mbInformation, MB_OK);
          Exec(RemoveQuotes(UninstallExe), ' /SILENT', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
          sleep(1000);
        end
  end;
end;
