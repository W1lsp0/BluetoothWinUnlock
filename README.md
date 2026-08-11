# BluetoothWinUnlock

BluetoothWinUnlock is the Windows unlock MVP for route A:

- `BluetoothUnlock.Provider`: native C++ Credential Provider DLL.
- `BluetoothUnlock.Service`: LocalSystem Windows service that owns verification and credential release.
- `BluetoothUnlock.Config`: administrator CLI for storing the Windows credential and granting short test verification windows.
- `BluetoothUnlock.Shared`: .NET Framework shared config and pipe protocol code.

The Provider does not bypass Windows authentication. It asks the service for a verified credential, packages it as `KERB_INTERACTIVE_UNLOCK_LOGON`, and returns it to LogonUI/Winlogon.

## MVP Flow

```text
LogonUI
  -> BluetoothUnlock.Provider GetSerialization()
  -> \\.\pipe\BluetoothUnlock GETCRED
  -> BluetoothUnlock.Service checks verifier state
  -> Service returns DPAPI-unprotected domain/user/password
  -> Provider submits KerbWorkstationUnlockLogon to Negotiate
```

The current verifier is intentionally small:

- `ManualTtl`: only unlocks after an administrator grants a short verification window.
- `AlwaysAllowTest`: test-only mode for proving the Credential Provider chain.

The existing WPF Bluetooth monitor can later call the same pipe command used by `BluetoothUnlock.Config grant --seconds 30` when the bound device passes challenge-response.

## Build

Use Visual Studio 2022 on Windows, or push to GitHub and download the `BluetoothWinUnlock-Windows-x64-Release` artifact from the Windows Build workflow.

```powershell
msbuild BluetoothUnlock.sln /p:Configuration=Release /p:Platform=x64
```

Build the Provider as x64 for normal Windows 10/11 desktops. A Credential Provider is loaded by LogonUI, so architecture must match the OS.

## Install

Run an elevated PowerShell:

```powershell
.\scripts\install-service.ps1 -ServiceExe .\BluetoothUnlock.Service\bin\Release\BluetoothUnlock.Service.exe
.\scripts\install-provider.ps1 -ProviderDll .\x64\Release\BluetoothUnlock.Provider.dll
```

Configure a test credential:

```powershell
.\BluetoothUnlock.Config\bin\Release\BluetoothUnlock.Config.exe set-credential --domain . --username alice --password "WindowsPassword"
.\BluetoothUnlock.Config\bin\Release\BluetoothUnlock.Config.exe grant --seconds 30
```

Lock Windows, choose the `Bluetooth Unlock` tile, and submit within the grant window.

## Safety Notes

- Do not leave `AlwaysAllowTest` enabled outside a disposable test machine.
- The password is stored with DPAPI machine scope under `%ProgramData%\BluetoothUnlock\config.xml`.
- The named pipe is restricted to LocalSystem and local Administrators.
- Replace `ManualTtl` with Bluetooth challenge-response before using this beyond MVP testing.
