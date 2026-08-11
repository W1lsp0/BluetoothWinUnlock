$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    throw "Run this script from an elevated PowerShell."
}

$targetDir = Join-Path $env:ProgramFiles "BluetoothUnlock"
$targetDll = Join-Path $targetDir "BluetoothUnlock.Provider.dll"

if (Test-Path $targetDll) {
    & "$env:SystemRoot\System32\regsvr32.exe" /u /s $targetDll
}

$existing = Get-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service -Name "BluetoothUnlock" -ErrorAction SilentlyContinue
    sc.exe delete BluetoothUnlock | Out-Null
}

Write-Host "BluetoothUnlock uninstalled. ProgramData config was left in place intentionally."
