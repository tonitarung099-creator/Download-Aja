param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionId
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$bridge = Join-Path $root "browser-bridge\DownloadAja.BrowserBridge.exe"

if (-not (Test-Path $bridge)) {
    throw "Browser bridge tidak ditemukan: $bridge"
}

$dataDir = Join-Path $root "data"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

$manifestPath = Join-Path $dataDir "com.downloadaja.bridge.json"
$manifest = @{
    name = "com.downloadaja.bridge"
    description = "Download Aja Chrome Native Messaging Bridge"
    path = $bridge
    type = "stdio"
    allowed_origins = @("chrome-extension://$ExtensionId/")
} | ConvertTo-Json -Depth 4

Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

$regPath = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.downloadaja.bridge"
New-Item -Path $regPath -Force | Out-Null
Set-Item -Path $regPath -Value $manifestPath

Write-Host "Integrasi Chrome terdaftar untuk extension: $ExtensionId"
Write-Host "Tidak diperlukan hak Administrator."
