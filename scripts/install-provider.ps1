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

$regsvr = Start-Process -FilePath "$env:SystemRoot\System32\regsvr32.exe" -ArgumentList @("/s", "`"$targetDll`"") -Wait -PassThru
if ($regsvr.ExitCode -ne 0) {
    throw "regsvr32 failed with exit code $($regsvr.ExitCode)."
}

$providerKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{8AA16CC0-1E39-473D-B8C7-8C3F7A4D6D62}"
if (-not (Test-Path $providerKey)) {
    throw "Credential Provider registry key was not created: $providerKey"
}

Write-Host "BluetoothUnlock Credential Provider registered."
Write-Host "Rollback: regsvr32 /u /s `"$targetDll`""
