param(
    [Parameter(Mandatory = $true)]
    [string]$ProviderDll
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    throw "Run this script from an elevated PowerShell."
}

$resolved = Resolve-Path $ProviderDll
$targetDir = Join-Path $env:ProgramFiles "BluetoothUnlock"
$targetDll = Join-Path $targetDir "BluetoothUnlock.Provider.dll"

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item -Force $resolved $targetDll

& "$env:SystemRoot\System32\regsvr32.exe" /s $targetDll

Write-Host "BluetoothUnlock Credential Provider registered."
Write-Host "Rollback: regsvr32 /u /s `"$targetDll`""
