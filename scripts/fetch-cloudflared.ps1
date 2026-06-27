param(
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "dist\RevitMcp\app\cloudflared.exe"
}

$outputDir = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"

Write-Host "Downloading cloudflared:"
Write-Host "  $url"
Write-Host "To:"
Write-Host "  $OutputPath"

Invoke-WebRequest -Uri $url -OutFile $OutputPath

if (!(Test-Path $OutputPath)) {
    throw "cloudflared download failed: $OutputPath"
}

Write-Host "Downloaded cloudflared.exe"

