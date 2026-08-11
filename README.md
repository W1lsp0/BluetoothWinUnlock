# BluetoothWinUnlock

BluetoothWinUnlock 是一个 Windows 蓝牙解锁 MVP。它通过 Windows Credential Provider 接入锁屏/登录界面，不是模拟键盘输入密码，也不是绕过 Windows 认证。

## 组件

- `BluetoothUnlock.Provider`：C++ Credential Provider DLL，负责在锁屏界面显示 `Bluetooth Unlock`。
- `BluetoothUnlock.Service`：LocalSystem Windows 服务，负责蓝牙监控、保存验证状态并向 Provider 释放凭据。
- `BluetoothUnlock.ConfigUi`：WPF 桌面配置界面，用于安装、保存凭据、管理可信蓝牙设备、查看状态和测试授权。
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

默认验证模式是 `ManualTtl`：服务只在短时间授权窗口内释放凭据。开启内置蓝牙自动授权后，服务会定时扫描可信设备列表；手机、手表、耳机等任意一个可信设备当前连接或刚刚被扫描到时，服务自动续上授权窗口。

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
3. 打开“安装维护”页，点击“安装服务”。
4. 点击“安装 Provider”。
5. 打开“凭据”页，填写：

```text
Domain: .
Username: 你的 Windows 用户名
Password: 你的 Windows 登录密码，不是 Windows Hello PIN
```

6. 勾选“上划进入登录界面后自动提交 Bluetooth Unlock”。
7. 点击“保存凭据”。
8. 在 Windows 设置中先完成蓝牙设备配对。
9. 打开“蓝牙设备”页，点击“扫描蓝牙设备”。
10. 选择设备，点击“添加到可信列表”。
11. 可以继续选择手机、手表、耳机等多个设备并逐个添加到“可信列表”。
12. 勾选“设备靠近时自动授权”。
13. 设置扫描间隔和授权秒数，默认可以先用 10 秒 / 30 秒。
14. 点击“保存蓝牙设置”。
15. 回到“状态”页，点击“刷新状态”。

状态区正常应看到类似：

```text
OK
hasCredential:1
mode:ManualTtl
autoSubmit:1
bluetoothEnabled:1
bluetoothTrustedDeviceCount:3
bluetoothLastStatus:not-nearby
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

在 `BluetoothUnlock.ConfigUi.exe` 的“安装维护”页：

1. 设置“授权秒数”，例如 `60`。
2. 点击“测试授权”。
3. 锁屏。
4. 如果未启用自动提交，在锁屏界面选择 `Bluetooth Unlock` 并点击 `Unlock`。
5. 如果已启用自动提交，且 `verifiedNow:1`，Windows 会尝试自动提交 `Bluetooth Unlock`。

如果锁屏界面没有显示 `Bluetooth Unlock`，重启一次 Windows 后再试。

## 蓝牙自动解锁

1. 打开 Windows 设置。
2. 进入“蓝牙和设备”。
3. 添加设备并完成配对。
4. 管理员运行 `BluetoothUnlock.ConfigUi.exe`。
5. 打开“蓝牙设备”页，点击“扫描蓝牙设备”。
6. 选择你的手机，点击“添加”。
7. 选择手表或耳机，继续点击“添加”。
8. 勾选“设备靠近时自动授权”。
9. 点击“保存蓝牙设置”。
10. 确认已经勾选“上划进入登录界面后自动提交 Bluetooth Unlock”，并点击“保存凭据”。
11. 等待一个扫描周期后点击“刷新状态”。

状态含义：

```text
蓝牙: 已靠近     -> 至少一个可信设备命中，服务正在续授权窗口
蓝牙: 未发现/3   -> 3 个可信设备都没扫描到
蓝牙: 等待扫描   -> 服务刚启动或还没完成第一次扫描
授权窗口: 可解锁 -> Provider 可以释放凭据
```

然后锁屏测试：

```powershell
rundll32.exe user32.dll,LockWorkStation
```

如果设备已靠近且自动提交已开启，上划或按键进入登录界面后，Windows 会尝试自动选择 `Bluetooth Unlock` 并解锁；停留在时间锁屏界面时不会自动解锁。

## 命令行备用

管理员 PowerShell：

```powershell
cd D:\BluetoothWinUnlock-Windows-x64-Release

.\scripts\install-service.ps1 -ServiceExe .\BluetoothUnlock.Service.exe
.\scripts\install-provider.ps1 -ProviderDll .\BluetoothUnlock.Provider.dll

.\BluetoothUnlock.Config.exe set-credential --domain . --username W1lsp0 --password "你的Windows登录密码"
.\BluetoothUnlock.Config.exe set-auto-submit --enabled true
.\BluetoothUnlock.Config.exe list-bluetooth
.\BluetoothUnlock.Config.exe set-bluetooth --enabled true --address AABBCCDDEEFF --name "我的手机" --probe-seconds 10 --grant-seconds 30
.\BluetoothUnlock.Config.exe add-bluetooth --address 112233445566 --name "我的手表"
.\BluetoothUnlock.Config.exe add-bluetooth --address 778899AABBCC --name "我的耳机"
.\BluetoothUnlock.Config.exe status
.\BluetoothUnlock.Config.exe grant --seconds 60
```

锁屏：

```powershell
rundll32.exe user32.dll,LockWorkStation
```

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

### 保存了多个可信设备但仍然不授权

如果“日志诊断”里的 `STATUS` 输出没有这些字段：

```text
protocolVersion:2
bluetoothTrustedDeviceCount:...
```

说明当前正在运行的 Windows 服务还是旧版。请在新版 `BluetoothUnlock.ConfigUi.exe` 的“安装维护”页点击“安装服务”，然后再刷新状态。

### 锁屏界面没有 Bluetooth Unlock

先确认“安装 Provider”没报错。如果仍然没有，重启 Windows。

### 如何在上划后不点击 Bluetooth Unlock 自动解锁

在 `BluetoothUnlock.ConfigUi.exe` 中勾选：

```text
上划进入登录界面后自动提交 Bluetooth Unlock
```

然后点击“保存凭据”。之后只要服务状态是 `verifiedNow:1`，上划或按键进入登录界面时 Credential Provider 会作为默认凭据自动提交；停留在显示时间的锁屏界面时不会提交。不同 Windows 登录界面状态下，自动选择可能需要重新锁屏或等待 LogonUI 重新枚举凭据。

### 蓝牙扫描不到手机

先确认 Windows 已经完成配对。很多手机平时不会一直处于经典蓝牙可发现状态，只有打开蓝牙设置页时才更容易被扫描到；手表、耳机、鼠标、键盘这类会保持连接的设备通常更稳定。命令行可以用：

```powershell
.\BluetoothUnlock.Config.exe list-bluetooth
```

如果列表里 `nearby:0`，服务不会自动授权，这是为了避免只凭历史配对缓存误解锁。你可以添加多个可信设备，任意一个 `nearby:1` 都会触发授权。

### 密码填什么

填 Windows 登录密码，不是 Windows Hello PIN。

## 安全说明

- 不要在日常机器上启用 `AlwaysAllowTest`。
- Windows 密码使用 DPAPI LocalMachine 保护后存储在 `%ProgramData%\BluetoothUnlock\config.xml`。
- 当前蓝牙联动是短时间授权窗口，并且只接受“当前连接”或“刚刚扫描到”的可信设备，避免只凭历史配对缓存解锁。
- 更强安全性后续应升级为 challenge-response。
