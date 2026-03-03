#define MyAppPublisher "DeKoi"
#define MyAppName "DeFocuser Lite"
#define MyAppVersion "2.0.0"

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
SourceDir=".."
OutputDir="Installer\Output"
OutputBaseFilename={#MyAppPublisher} {#MyAppName} Setup-{#MyAppVersion}
WizardImageFile="Installer\WizardImageFile.bmp"
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; ASCOM Driver DLL (thin IPC proxy)
Source: "ASCOM_Driver\bin\Release\ASCOM.DeKoi.DeFocuserLite.dll"; DestDir: "{app}"
; Mediator App EXE (owns serial connection, serves ASCOM clients)
Source: "Focuser_App_v2\bin\x64\Release\ASCOM.DeKoi.DeFocuserMediator.exe"; DestDir: "{app}"

[Icons]
Name: "{group}\DeFocuser Lite Controller"; Filename: "{app}\ASCOM.DeKoi.DeFocuserMediator.exe"
Name: "{commondesktop}\DeFocuser Lite Controller"; Filename: "{app}\ASCOM.DeKoi.DeFocuserMediator.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; Register COM for 32-bit
Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 32bit
; Register COM for 64-bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64

[UninstallRun]
Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden 64bit; Check: IsWin64

[Code]
const REQUIRED_PLATFORM_VERSION = 6.2; // Set this to the minimum required ASCOM Platform version for this application

//
// Function to return the ASCOM Platform's version number as a double.
//
function PlatformVersion(): Double;
var
   PlatVerString : String;
begin
   Result := 0.0; // Initialize the return value in case we can't read the registry
   try
      if RegQueryStringValue(HKEY_LOCAL_MACHINE_32, 'Software\ASCOM','PlatformVersion', PlatVerString) then
      begin // Successfully read the value from the registry
         Result := StrToFloat(PlatVerString); // Create a double from the X.Y Platform version string
      end;
   except
      ShowExceptionMessage;
      Result:= -1.0; // Indicate in the return value that an exception was generated
   end;
end;

//
// Before the installer UI appears, verify that the required ASCOM Platform version is installed.
//
function InitializeSetup(): Boolean;
var
   PlatformVersionNumber : double;
 begin
   Result := FALSE; // Assume failure
   PlatformVersionNumber := PlatformVersion(); // Get the installed Platform version as a double
   If PlatformVersionNumber >= REQUIRED_PLATFORM_VERSION then	// Check whether we have the minimum required Platform or newer
      Result := TRUE
   else
      if PlatformVersionNumber = 0.0 then
         MsgBox('No ASCOM Platform is installed. Please install Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later from https://www.ascom-standards.org', mbCriticalError, MB_OK)
      else
         MsgBox('ASCOM Platform ' + Format('%3.1f', [REQUIRED_PLATFORM_VERSION]) + ' or later is required, but Platform '+ Format('%3.1f', [PlatformVersionNumber]) + ' is installed. Please install the latest Platform before continuing; you will find it at https://www.ascom-standards.org', mbCriticalError, MB_OK);
end;

// Code to enable the installer to uninstall previous versions of itself when a new version is installed
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UninstallExe: String;
  UninstallRegistry: String;
begin
  if (CurStep = ssInstall) then // Install step has started
	begin
      // Create the correct registry location name, which is based on the AppId
      UninstallRegistry := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}' + '_is1');
      // Check whether an extry exists
      if RegQueryStringValue(HKLM, UninstallRegistry, 'UninstallString', UninstallExe) then
        begin // Entry exists and previous version is installed so run its uninstaller quietly after informing the user
          MsgBox('Setup will now remove the previous version.', mbInformation, MB_OK);
          Exec(RemoveQuotes(UninstallExe), ' /SILENT', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
          sleep(1000); // Give enough time for the install screen to be repainted before continuing
        end
  end;
end;
