param(
    [string]$PackageRoot = "",
    [switch]$RequireAddin
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $repoRoot "dist\RevitMcp"
}

$requiredFiles = @(
    "app\RevitMcpServer.exe",
    "app\RevitMcpLauncher.exe"
)

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $PackageRoot $relativePath
    if (!(Test-Path $path)) {
        throw "Release layout is missing required file: $path"
    }
}

$addinsRoot = Join-Path $PackageRoot "addins"
if (!(Test-Path $addinsRoot)) {
    throw "Release layout is missing addins folder: $addinsRoot"
}

$addinDlls = Get-ChildItem -Path $addinsRoot -Filter "RevitMcpAddin.dll" -Recurse -ErrorAction SilentlyContinue
if ($RequireAddin -and $addinDlls.Count -eq 0) {
    throw "Release layout has no packaged Revit add-in DLLs under $addinsRoot"
}

Write-Host "Release layout OK:"
Write-Host "  $PackageRoot"
Write-Host "Add-in DLLs found: $($addinDlls.Count)"

