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
$targetExe = Join-Path $targetDir "BluetoothUnlock.Service.exe"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -Force $resolved $targetExe
Get-ChildItem -Path $sourceDir -Filter "*.dll" | ForEach-Object {
    Copy-Item -Force $_.FullName (Join-Path $targetDir $_.Name)
}

$existing = Get-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
    sc.exe delete BluetoothUnlock | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create BluetoothUnlock binPath= "`"$targetExe`"" start= auto DisplayName= "Bluetooth Unlock Service" | Out-Null
sc.exe description BluetoothUnlock "Credential release service for the BluetoothUnlock Credential Provider." | Out-Null
Start-Service -Name "BluetoothUnlock"

Write-Host "BluetoothUnlock service installed and started."
Write-Host "Installed files:"
Get-ChildItem $targetDir -Filter "BluetoothUnlock.Service.exe" | ForEach-Object { Write-Host "  $($_.FullName)" }
Get-ChildItem $targetDir -Filter "*.dll" | ForEach-Object { Write-Host "  $($_.FullName)" }
