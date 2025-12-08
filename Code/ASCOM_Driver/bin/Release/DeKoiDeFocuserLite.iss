;--------------------------------
; Inno Setup Script for DeKoi DeFocuser Lite
;--------------------------------
[Setup]
AppName=DeKoi DeFocuser Lite
AppVersion=1.0
DefaultDirName={pf}\ASCOM\DeKoi DeFocuser Lite
DefaultGroupName=ASCOM Drivers
OutputBaseFilename=DeKoiDeFocuserLite_Installer
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
; Your compiled driver DLL
Source: "D:\Astrophotography\DIY Projects\ascom-oag-focuser-2\ASCOM_Driver\bin\Release\ASCOM.DeKoi.DeFocuserLite.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DeKoi DeFocuser Lite"; Filename: "{app}\ASCOM.DeKoi.DeFocuserLite.dll"

[Run]
; Register 32-bit COM
Filename: "{dotnet20}\RegAsm.exe"; Parameters: """{app}\ASCOM.DeKoi.DeFocuserLite.dll"" /codebase"; Flags: runhidden waituntilterminated
; Register 64-bit COM
Filename: "{dotnet40}\RegAsm.exe"; Parameters: """{app}\ASCOM.DeKoi.DeFocuserLite.dll"" /codebase"; Flags: runhidden waituntilterminated

[UninstallRun]
; Unregister 32-bit COM
Filename: "{dotnet20}\RegAsm.exe"; Parameters: "/u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden waituntilterminated
; Unregister 64-bit COM
Filename: "{dotnet40}\RegAsm.exe"; Parameters: "/u ""{app}\ASCOM.DeKoi.DeFocuserLite.dll"""; Flags: runhidden waituntilterminated
