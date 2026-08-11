$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    throw "Run this script from an elevated PowerShell."
}

$targetDir = Join-Path $env:ProgramFiles "BluetoothUnlock"
$providerKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{8AA16CC0-1E39-473D-B8C7-8C3F7A4D6D62}"
$clsidKey = "HKCR:\CLSID\{8AA16CC0-1E39-473D-B8C7-8C3F7A4D6D62}\InprocServer32"
$targetDll = $null

if (Test-Path $clsidKey) {
    $targetDll = (Get-ItemProperty -Path $clsidKey)."(default)"
}

if ($targetDll -and (Test-Path $targetDll)) {
    & "$env:SystemRoot\System32\regsvr32.exe" /u /s $targetDll
}

if (Test-Path $providerKey) {
    Remove-Item -Recurse -Force $providerKey
}

$existing = Get-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
    $existing.WaitForStatus("Stopped", "00:00:15")
    Get-Process -Name "BluetoothUnlock.Service" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    sc.exe delete BluetoothUnlock | Out-Null
}

Write-Host "BluetoothUnlock uninstalled. ProgramData config was left in place intentionally."
