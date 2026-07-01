param(
    [string]$RevitVersion = "2026",
    [string]$Configuration = "Debug",
    [string]$ProjectPath = "",
    [string]$RevitInstallDir = "",
    [string]$TargetFramework = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot "addin\RevitMcpAddin\RevitMcpAddin.csproj"
}

if ([string]::IsNullOrWhiteSpace($RevitInstallDir)) {
    $RevitInstallDir = "C:\Program Files\Autodesk\Revit $RevitVersion"
}

if (!(Test-Path $ProjectPath)) {
    throw "Project not found: $ProjectPath"
}

if (!(Test-Path (Join-Path $RevitInstallDir "RevitAPI.dll"))) {
    throw "RevitAPI.dll not found in '$RevitInstallDir'. Pass -RevitInstallDir if Revit is installed elsewhere."
}

if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    if ([int]$RevitVersion -le 2024) {
        $TargetFramework = "net48"
    }
    else {
        $TargetFramework = "net8.0-windows"
    }
}

dotnet restore $ProjectPath -p:RevitVersion=$RevitVersion -p:RevitInstallDir="$RevitInstallDir" -p:TargetFramework=$TargetFramework
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

dotnet build $ProjectPath -c $Configuration -f $TargetFramework -p:RevitVersion=$RevitVersion -p:RevitInstallDir="$RevitInstallDir" --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$projectDir = Split-Path -Parent $ProjectPath
$assemblyPath = Join-Path $projectDir "bin\$Configuration\$TargetFramework\RevitMcpAddin.dll"
if (!(Test-Path $assemblyPath)) {
    throw "Built assembly not found: $assemblyPath"
}

$addinDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
New-Item -ItemType Directory -Force -Path $addinDir | Out-Null

$templatePath = Join-Path $projectDir "RevitMcp.addin.template"
$addinPath = Join-Path $addinDir "RevitMcp.addin"
$assemblyXmlPath = [System.Security.SecurityElement]::Escape($assemblyPath)

(Get-Content $templatePath -Raw).Replace("{{ASSEMBLY_PATH}}", $assemblyXmlPath) | Set-Content -Encoding UTF8 $addinPath

[xml]$addinXml = Get-Content $addinPath
$installedAssembly = $addinXml.RevitAddIns.AddIn.Assembly
if (!(Test-Path $installedAssembly)) {
    throw "Generated .addin points to a missing assembly: $installedAssembly"
}

Write-Host "Installed Revit MCP add-in:"
Write-Host "  $addinPath"
Write-Host "Assembly:"
Write-Host "  $assemblyPath"
Write-Host "Target framework:"
Write-Host "  $TargetFramework"
Write-Host ""
Write-Host "Next:"
Write-Host "  1. Start the MCP server."
Write-Host "  2. Open or restart Revit $RevitVersion."
