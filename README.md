# BluetoothWinUnlock

BluetoothWinUnlock 是一个 Windows 蓝牙解锁 MVP。它通过 Windows Credential Provider 接入锁屏/登录界面，不是模拟键盘输入密码，也不是绕过 Windows 认证。

## 组件

- `BluetoothUnlock.Provider`：C++ Credential Provider DLL，负责在锁屏界面显示 `Bluetooth Unlock`。
- `BluetoothUnlock.Service`：LocalSystem Windows 服务，负责保存验证状态并向 Provider 释放凭据。
- `BluetoothUnlock.ConfigUi`：桌面配置界面，用于安装、保存凭据、查看状态和测试授权。
- `BluetoothUnlock.Config`：命令行配置工具，适合排查或脚本化。
- `BluetoothUnlock.Shared`：共享配置和 Named Pipe 协议代码。

## 工作流程

```text
锁屏界面
  -> Bluetooth Unlock tile
  -> Credential Provider 通过 \\.\pipe\BluetoothUnlock 请求凭据
  -> Windows 服务检查是否已经授权
  -> 服务释放 DPAPI 保护的 Windows 凭据
  -> Provider 提交 KerbWorkstationUnlockLogon
  -> Windows 完成解锁
```

当前版本的验证模式是 `ManualTtl`：必须先给一个短时间授权窗口，例如 60 秒。新版 `UnlockServer.Wpf` 的蓝牙监控会在设备靠近时自动调用这个授权。

## 下载

到 GitHub Actions 下载最新构建产物：

```text
BluetoothWinUnlock-Windows-x64-Release
```

解压后目录里应包含：

```text
BluetoothUnlock.ConfigUi.exe
BluetoothUnlock.Config.exe
BluetoothUnlock.Provider.dll
BluetoothUnlock.Service.exe
BluetoothUnlock.Shared.dll
scripts\
```

## 使用界面安装

必须以管理员身份运行：

1. 右键 `BluetoothUnlock.ConfigUi.exe`。
2. 选择“以管理员身份运行”。
3. 点击 `Install service`。
4. 点击 `Install provider`。
5. 在 `Windows credential` 区域填写：

```text
Domain: .
Username: 你的 Windows 用户名
Password: 你的 Windows 登录密码，不是 Windows Hello PIN
```

6. 点击 `Save credential`。
7. 点击 `Refresh status`。

状态区正常应看到类似：

```text
OK
hasCredential:1
mode:ManualTtl
verifiedUntilUtc:...
verifiedNow:0
END
```

如果看到：

```text
Service pipe unavailable
```

通常说明服务没有安装、没有启动，或者 UI 不是管理员权限运行。

## 测试解锁

在 `BluetoothUnlock.ConfigUi.exe` 里：

1. 设置 `Grant seconds`，例如 `60`。
2. 点击 `Grant test unlock`。
3. 锁屏。
4. 在锁屏界面选择 `Bluetooth Unlock`。
5. 点击 `Unlock`。

如果锁屏界面没有显示 `Bluetooth Unlock`，重启一次 Windows 后再试。

## 命令行备用

管理员 PowerShell：

```powershell
cd D:\BluetoothWinUnlock-Windows-x64-Release

.\scripts\install-service.ps1 -ServiceExe .\BluetoothUnlock.Service.exe
.\scripts\install-provider.ps1 -ProviderDll .\BluetoothUnlock.Provider.dll

.\BluetoothUnlock.Config.exe set-credential --domain . --username W1lsp0 --password "你的Windows登录密码"
.\BluetoothUnlock.Config.exe status
.\BluetoothUnlock.Config.exe grant --seconds 60
```

锁屏：

```powershell
rundll32.exe user32.dll,LockWorkStation
```

## 配合蓝牙监控

安装并配置好 BluetoothWinUnlock 后，运行新版 `UnlockServer.Wpf`：

1. 选择蓝牙设备。
2. 开启 `自动解锁`。
3. 点击开始监控。
4. 锁屏后设备靠近时，WPF 会自动调用本地服务授权。
5. 在锁屏界面点击 `Bluetooth Unlock` 完成解锁。

## 卸载

管理员 PowerShell：

```powershell
cd D:\BluetoothWinUnlock-Windows-x64-Release

.\scripts\uninstall.ps1
Stop-Service BluetoothUnlock -ErrorAction SilentlyContinue
Get-Process BluetoothUnlock.Service -ErrorAction SilentlyContinue | Stop-Process -Force
sc.exe delete BluetoothUnlock
```

配置文件默认保留在：

```text
C:\ProgramData\BluetoothUnlock\config.xml
```

需要彻底清理时可以手动删除。

## 常见问题

### Run this script from an elevated PowerShell

说明不是管理员权限。请右键 `BluetoothUnlock.ConfigUi.exe`，选择“以管理员身份运行”。

### Service pipe unavailable

说明 UI/CLI 连不上 `\\.\pipe\BluetoothUnlock`。通常是服务没安装或没启动。先点 `Install service`，再点 `Refresh status`。

### 锁屏界面没有 Bluetooth Unlock

先确认 `Install provider` 没报错。如果仍然没有，重启 Windows。

### 密码填什么

填 Windows 登录密码，不是 Windows Hello PIN。

## 安全说明

- 不要在日常机器上启用 `AlwaysAllowTest`。
- Windows 密码使用 DPAPI LocalMachine 保护后存储在 `%ProgramData%\BluetoothUnlock\config.xml`。
- 当前蓝牙联动是短时间授权窗口，后续应升级为 challenge-response。
