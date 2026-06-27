param(
    [string[]]$RevitVersions = @("2021", "2022", "2023", "2024", "2025", "2026"),
    [switch]$SkipPythonExe,
    [switch]$SkipCloudflared,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $repoRoot "dist\RevitMcp"

New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

if (!$SkipPythonExe) {
    & (Join-Path $PSScriptRoot "build-server-exe.ps1")
}

if (!$SkipCloudflared) {
    & (Join-Path $PSScriptRoot "fetch-cloudflared.ps1")
}

& (Join-Path $PSScriptRoot "package-addins.ps1") -RevitVersions $RevitVersions -Configuration Release
& (Join-Path $PSScriptRoot "check-release-layout.ps1") -PackageRoot $packageRoot -RequireAddin

if (!$SkipInstaller) {
    $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($iscc) {
        & $iscc.Source (Join-Path $repoRoot "installer\RevitMcp.iss")
    }
    else {
        Write-Warning "Inno Setup ISCC.exe was not found. Install Inno Setup or run installer\RevitMcp.iss manually."
    }
}

Write-Host "Package root:"
Write-Host "  $packageRoot"
