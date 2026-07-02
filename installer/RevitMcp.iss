#define MyAppName "Revit MCP"
#define MyAppVersion "0.3.0"
#define MyAppPublisher "Revit MCP"
#define SourceRoot "..\dist\RevitMcp"

[Setup]
AppId={{7E6C93B8-C2D8-4F6C-9A6A-9386278E048C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\RevitMcp
DefaultGroupName=Revit MCP
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
OutputDir=..\dist\installer
OutputBaseFilename=RevitMcpSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#SourceRoot}\app\*"; DestDir: "{app}\app"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\addins\*"; DestDir: "{app}\addins"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Revit MCP"; Filename: "{app}\app\RevitMcpLauncher.exe"
Name: "{userdesktop}\Revit MCP"; Filename: "{app}\app\RevitMcpLauncher.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\RevitMcp"

[Code]
procedure StopProcessByImageName(const ImageName: string);
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{cmd}'), '/C taskkill /IM "' + ImageName + '" /T /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function AddinXml(const AssemblyPath: string): string;
begin
  Result :=
    '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
    '<RevitAddIns>' + #13#10 +
    '  <AddIn Type="Application">' + #13#10 +
    '    <Name>Revit MCP</Name>' + #13#10 +
    '    <Assembly>' + AssemblyPath + '</Assembly>' + #13#10 +
    '    <AddInId>8C8A7F0D-9E9E-4C67-B7D4-2E6D67E9A172</AddInId>' + #13#10 +
    '    <FullClassName>RevitMcpAddin.App</FullClassName>' + #13#10 +
    '    <VendorId>RMCP</VendorId>' + #13#10 +
    '    <VendorDescription>Local MCP bridge for Revit</VendorDescription>' + #13#10 +
    '  </AddIn>' + #13#10 +
    '</RevitAddIns>' + #13#10;
end;

procedure InstallAddinForVersion(const Version: string);
var
  ApiPath: string;
  SourceAssembly: string;
  AddinDir: string;
  AddinPath: string;
begin
  ApiPath := ExpandConstant('{pf}\Autodesk\Revit ' + Version + '\RevitAPI.dll');
  SourceAssembly := ExpandConstant('{app}\addins\' + Version + '\RevitMcpAddin.dll');

  if FileExists(ApiPath) and FileExists(SourceAssembly) then
  begin
    AddinDir := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Version);
    ForceDirectories(AddinDir);
    AddinPath := AddinDir + '\RevitMcp.addin';
    SaveStringToFile(AddinPath, AddinXml(SourceAssembly), False);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopProcessByImageName('RevitMcpServer.exe');
    StopProcessByImageName('ngrok.exe');
  end;

  if CurStep = ssPostInstall then
  begin
    InstallAddinForVersion('2021');
    InstallAddinForVersion('2022');
    InstallAddinForVersion('2023');
    InstallAddinForVersion('2024');
    InstallAddinForVersion('2025');
    InstallAddinForVersion('2026');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Version: Integer;
  AddinPath: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    for Version := 2021 to 2026 do
    begin
      AddinPath := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + IntToStr(Version) + '\RevitMcp.addin');
      if FileExists(AddinPath) then
        DeleteFile(AddinPath);
    end;
  end;
end;
