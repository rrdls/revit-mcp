param(
    [string[]]$RevitVersions = @("2021", "2022", "2023", "2024", "2025", "2026"),
    [string]$Configuration = "Release",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "addin\RevitMcpAddin\RevitMcpAddin.csproj"
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "dist\RevitMcp\addins"
}

foreach ($version in $RevitVersions) {
    $revitInstallDir = "C:\Program Files\Autodesk\Revit $version"
    if (!(Test-Path (Join-Path $revitInstallDir "RevitAPI.dll"))) {
        Write-Warning "Skipping Revit $version because RevitAPI.dll was not found at $revitInstallDir"
        continue
    }

    $targetFramework = if ([int]$version -le 2024) { "net48" } else { "net8.0-windows" }

    dotnet restore $projectPath -p:RevitVersion=$version -p:RevitInstallDir="$revitInstallDir" -p:TargetFramework=$targetFramework
    dotnet build $projectPath -c $Configuration -f $targetFramework -p:RevitVersion=$version -p:RevitInstallDir="$revitInstallDir" --no-restore

    $sourceDir = Join-Path $repoRoot "addin\RevitMcpAddin\bin\$Configuration\$targetFramework"
    $targetDir = Join-Path $OutputRoot $version
    if (Test-Path $targetDir) {
        Remove-Item -Recurse -Force $targetDir
    }
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    Copy-Item -Path (Join-Path $sourceDir "*") -Destination $targetDir -Recurse -Force

    Write-Host "Packaged Revit $version add-in:"
    Write-Host "  $targetDir"
}
