param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceExe
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    throw "Run this script from an elevated PowerShell."
}

$resolved = Resolve-Path $ServiceExe
$sourceDir = Split-Path -Parent $resolved
$targetDir = Join-Path $env:ProgramFiles "BluetoothUnlock"
$versionDir = Join-Path $targetDir ("service-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$targetExe = Join-Path $versionDir "BluetoothUnlock.Service.exe"

$existing = Get-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
    $existing.WaitForStatus("Stopped", "00:00:15")
    Get-Process -Name "BluetoothUnlock.Service" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    sc.exe delete BluetoothUnlock | Out-Null
    Start-Sleep -Seconds 3
}

New-Item -ItemType Directory -Force -Path $versionDir | Out-Null
Copy-Item -Force $resolved $targetExe
Get-ChildItem -Path $sourceDir -Filter "*.dll" | ForEach-Object {
    Copy-Item -Force $_.FullName (Join-Path $versionDir $_.Name)
}

sc.exe create BluetoothUnlock binPath= "`"$targetExe`"" start= auto DisplayName= "Bluetooth Unlock Service" | Out-Null
sc.exe description BluetoothUnlock "Credential release service for the BluetoothUnlock Credential Provider." | Out-Null
Start-Service -Name "BluetoothUnlock"

Write-Host "BluetoothUnlock service installed and started."
Write-Host "Installed files:"
Get-ChildItem $versionDir | ForEach-Object { Write-Host "  $($_.FullName)" }
