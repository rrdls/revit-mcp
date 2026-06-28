param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "dist\RevitMcp\app\ngrok.exe"
}

$outputDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$archivePath = Join-Path $outputDir "ngrok.zip"
$extractDir = Join-Path $outputDir "ngrok-extract"
$url = "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-windows-amd64.zip"

Write-Host "Downloading ngrok:"
Write-Host "  $url"
Write-Host "To:"
Write-Host "  $OutputPath"

Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue
Remove-Item -Force $archivePath -ErrorAction SilentlyContinue

Invoke-WebRequest -Uri $url -OutFile $archivePath
Expand-Archive -Path $archivePath -DestinationPath $extractDir -Force

$downloadedExe = Join-Path $extractDir "ngrok.exe"
if (!(Test-Path $downloadedExe)) {
    throw "ngrok download did not contain ngrok.exe"
}

Copy-Item -Path $downloadedExe -Destination $OutputPath -Force
Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue
Remove-Item -Force $archivePath -ErrorAction SilentlyContinue

if (!(Test-Path $OutputPath)) {
    throw "ngrok download failed: $OutputPath"
}

Write-Host "Downloaded ngrok.exe"
