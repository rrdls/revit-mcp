param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pythonDir = Join-Path $repoRoot "python"
$venvDir = Join-Path $repoRoot ".venv-build"
$distDir = Join-Path $repoRoot "dist\RevitMcp\app"
$workDir = Join-Path $repoRoot "dist\pyinstaller-work"

if (Test-Path $venvDir) {
    Remove-Item -Recurse -Force $venvDir
}

python -m venv $venvDir

$pythonExe = Join-Path $venvDir "Scripts\python.exe"
& $pythonExe -m pip install --upgrade pip
& $pythonExe -m pip install -e "${pythonDir}[build]"

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $pythonExe -m PyInstaller `
    --noconfirm `
    --clean `
    --onefile `
    --name RevitMcpServer `
    --distpath $distDir `
    --workpath $workDir `
    --specpath (Join-Path $repoRoot "dist") `
    (Join-Path $repoRoot "packaging\pyinstaller\server_entry.py")

& $pythonExe -m PyInstaller `
    --noconfirm `
    --clean `
    --onefile `
    --windowed `
    --name RevitMcpLauncher `
    --distpath $distDir `
    --workpath $workDir `
    --specpath (Join-Path $repoRoot "dist") `
    (Join-Path $repoRoot "packaging\pyinstaller\launcher_entry.py")

Write-Host "Built:"
Write-Host "  $(Join-Path $distDir 'RevitMcpServer.exe')"
Write-Host "  $(Join-Path $distDir 'RevitMcpLauncher.exe')"
