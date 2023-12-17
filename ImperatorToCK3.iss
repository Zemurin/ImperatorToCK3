; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "ImperatorToCK3 Converter"
#define MyAppPublisher "Paradox Game Converters Group"
#define MyAppURL "https://paradoxgameconverters.com/"
#define MyAppExeName "ConverterFrontend.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{41F6197B-AB2F-4369-B443-EE8DA705EE1B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\ImperatorToCK3
DisableProgramGroupPage=yes
LicenseFile="ImperatorToCK3\Data_Files\converter_globals\license.txt"
InfoAfterFile="ImperatorToCK3\Data_Files\converter_globals\ReadMe.txt"
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=admin
OutputBaseFilename=ImperatorToCK3-win-x64-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
    
[InstallDelete]                                     
Type: filesandordirs; Name: "{app}\ImperatorToCK3\blankMod"
Type: filesandordirs; Name: "{app}\ImperatorToCK3\temp"

[Files]
Source: "Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\ConverterFrontend.exe"; IconFilename: "{app}\Assets\converter.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser

