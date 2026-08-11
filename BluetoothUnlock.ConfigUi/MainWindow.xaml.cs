using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.ConfigUi
{
    public partial class MainWindow : Window
    {
        private readonly Brush _successBrush;
        private readonly Brush _warningBrush;
        private readonly Brush _dangerBrush;
        private readonly Brush _secondaryBrush;

        public MainWindow()
        {
            InitializeComponent();
            _successBrush = (Brush)FindResource("SuccessBrush");
            _warningBrush = (Brush)FindResource("WarningBrush");
            _dangerBrush = (Brush)FindResource("DangerBrush");
            _secondaryBrush = (Brush)FindResource("TextSecondary");

            UpdateAdminState();
            LoadConfig();
            NavigationList.SelectedIndex = 0;
            RefreshStatus();
        }

        private void NavigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = NavigationList.SelectedIndex;
            StatusPage.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            CredentialPage.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            BluetoothPage.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
            MaintenancePage.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
            LogsPage.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

            switch (index)
            {
                case 0:
                    SetPageHeader("状态", "查看服务、凭据、蓝牙和授权窗口状态。");
                    break;
                case 1:
                    SetPageHeader("凭据", "保存 Windows 登录凭据和自动提交设置。");
                    break;
                case 2:
                    SetPageHeader("蓝牙设备", "管理手机、手表、耳机等可信设备。");
                    break;
                case 3:
                    SetPageHeader("安装维护", "安装服务、注册 Provider、测试授权和卸载。");
                    break;
                case 4:
                    SetPageHeader("日志诊断", "查看状态输出并复制诊断信息。");
                    break;
            }
        }

        private void SetPageHeader(string title, string subtitle)
        {
            PageTitleText.Text = title;
            PageSubtitleText.Text = subtitle;
        }

        private void LoadConfig()
        {
            try
            {
                var config = ConfigStore.Load();
                DomainTextBox.Text = string.IsNullOrWhiteSpace(config.Domain) ? "." : config.Domain;
                UsernameTextBox.Text = config.Username ?? "";
                AutoSubmitCheckBox.IsChecked = config.AutoSubmitOnVerified;
                BluetoothEnabledCheckBox.IsChecked = config.BluetoothUnlockEnabled;
                ProbeIntervalTextBox.Text = Clamp(config.BluetoothProbeIntervalSeconds, 3, 300).ToString();
                BluetoothGrantTextBox.Text = Clamp(config.BluetoothGrantSeconds, 5, 300).ToString();
                LoadTrustedDevices(config);
            }
            catch (Exception ex)
            {
                AppendOutput("加载配置失败: " + ex.Message);
            }
        }

        private void SaveCredential_Click(object sender, RoutedEventArgs e)
        {
            SaveCredential();
        }

        private void SaveCredential()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    MessageBox.Show(this, "请填写 Windows 用户名。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = ConfigStore.Load();
                if (!string.IsNullOrEmpty(PasswordBox.Password))
                {
                    ConfigStore.SetCredential(DomainTextBox.Text, UsernameTextBox.Text, PasswordBox.Password);
                    config = ConfigStore.Load();
                }
                else if (!config.HasCredential)
                {
                    MessageBox.Show(this, "首次保存凭据必须填写 Windows 登录密码。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                config.Domain = string.IsNullOrWhiteSpace(DomainTextBox.Text) ? "." : DomainTextBox.Text;
                config.Username = UsernameTextBox.Text;
                config.AutoSubmitOnVerified = AutoSubmitCheckBox.IsChecked == true;
                ConfigStore.Save(config);
                PasswordBox.Clear();
                AppendOutput("凭据已保存: " + ConfigStore.ConfigPath);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("保存凭据失败: " + ex.Message);
            }
        }

        private void ScanBluetooth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                AppendOutput("正在扫描蓝牙设备...");
                var devices = BluetoothDeviceScanner.FindDevices(true);
                ScannedDevicesListBox.Items.Clear();
                foreach (var device in devices)
                {
                    ScannedDevicesListBox.Items.Add(new BluetoothDeviceViewModel(device));
                }

                if (ScannedDevicesListBox.Items.Count > 0)
                {
                    ScannedDevicesListBox.SelectedIndex = 0;
                }

                AppendOutput("扫描完成，发现 " + devices.Count + " 个设备。");
            }
            catch (Exception ex)
            {
                AppendOutput("扫描蓝牙失败: " + ex.Message);
            }
            finally
            {
                Cursor = null;
            }
        }

        private void AddTrustedDevice_Click(object sender, RoutedEventArgs e)
        {
            var selected = ScannedDevicesListBox.SelectedItem as BluetoothDeviceViewModel;
            if (selected == null)
            {
                MessageBox.Show(this, "请先选择一个扫描到的设备。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var trusted = selected.ToTrustedDevice();
            if (ContainsTrustedDevice(trusted))
            {
                AppendOutput("设备已在可信列表中: " + trusted);
                return;
            }

            TrustedDevicesListBox.Items.Add(trusted);
            AppendOutput("已添加可信设备: " + trusted);
        }

        private void RemoveTrustedDevice_Click(object sender, RoutedEventArgs e)
        {
            if (TrustedDevicesListBox.SelectedIndex < 0)
            {
                MessageBox.Show(this, "请先选择要移除的可信设备。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var item = TrustedDevicesListBox.SelectedItem;
            TrustedDevicesListBox.Items.Remove(item);
            AppendOutput("已移除可信设备: " + item);
        }

        private void SaveBluetooth_Click(object sender, RoutedEventArgs e)
        {
            SaveBluetoothSettings();
        }

        private void SaveBluetoothSettings()
        {
            try
            {
                var devices = GetTrustedDevices();
                if (BluetoothEnabledCheckBox.IsChecked == true && devices.Count == 0)
                {
                    MessageBox.Show(this, "请至少添加一个可信蓝牙设备。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var probeSeconds = ParseBoundedInt(ProbeIntervalTextBox.Text, 10, 3, 300);
                var grantSeconds = ParseBoundedInt(BluetoothGrantTextBox.Text, 30, 5, 300);
                ProbeIntervalTextBox.Text = probeSeconds.ToString();
                BluetoothGrantTextBox.Text = grantSeconds.ToString();

                ConfigStore.SetBluetoothDevices(
                    BluetoothEnabledCheckBox.IsChecked == true,
                    devices,
                    probeSeconds,
                    grantSeconds);

                AppendOutput("蓝牙设置已保存。可信设备数量: " + devices.Count);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("保存蓝牙设置失败: " + ex.Message);
            }
        }

        private void InstallService_Click(object sender, RoutedEventArgs e)
        {
            RunScript("install-service.ps1", "-ServiceExe .\\BluetoothUnlock.Service.exe");
        }

        private void InstallProvider_Click(object sender, RoutedEventArgs e)
        {
            RunScript("install-provider.ps1", "-ProviderDll .\\BluetoothUnlock.Provider.dll");
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            RunScript("uninstall.ps1", "");
        }

        private void Grant_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var seconds = ParseBoundedInt(GrantSecondsTextBox.Text, 60, 1, 300);
                GrantSecondsTextBox.Text = seconds.ToString();
                AppendOutput(SendPipeCommand("GRANT " + seconds));
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("测试授权失败: " + ex.Message);
            }
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            try
            {
                var response = SendPipeCommand("STATUS");
                AppendOutput(response);
                UpdateStatus(ParseResponse(response), true);
            }
            catch (Exception ex)
            {
                try
                {
                    var config = ConfigStore.Load();
                    var values = new Dictionary<string, string>
                    {
                        ["hasCredential"] = config.HasCredential ? "1" : "0",
                        ["mode"] = config.VerifierMode.ToString(),
                        ["autoSubmit"] = config.AutoSubmitOnVerified ? "1" : "0",
                        ["bluetoothEnabled"] = config.BluetoothUnlockEnabled ? "1" : "0",
                        ["bluetoothTrustedDeviceCount"] = config.BluetoothTrustedDevices == null ? "0" : config.BluetoothTrustedDevices.Count.ToString(),
                        ["bluetoothLastStatus"] = config.BluetoothLastStatus,
                        ["bluetoothLastMatchedDeviceName"] = config.BluetoothLastMatchedDeviceName,
                        ["bluetoothLastMatchedDeviceAddress"] = BluetoothAddress.FormatWithSeparators(config.BluetoothLastMatchedDeviceAddress),
                        ["verifiedNow"] = "0",
                    };
                    UpdateStatus(values, false);
                    AppendOutput(
                        "服务管道不可用。\r\n" +
                        "配置文件: " + ConfigStore.ConfigPath + "\r\n" +
                        "凭据已保存: " + config.HasCredential + "\r\n" +
                        "自动提交: " + config.AutoSubmitOnVerified + "\r\n" +
                        "蓝牙自动授权: " + config.BluetoothUnlockEnabled + "\r\n" +
                        "可信设备数量: " + (config.BluetoothTrustedDevices == null ? 0 : config.BluetoothTrustedDevices.Count) + "\r\n" +
                        "错误: " + ex.Message);
                }
                catch
                {
                    SetStatus(ServiceStatusText, "异常", _dangerBrush);
                    AppendOutput("刷新状态失败: " + ex.Message);
                }
            }
        }

        private void UpdateStatus(Dictionary<string, string> values, bool serviceAvailable)
        {
            SetStatus(ServiceStatusText, serviceAvailable ? "运行中" : "未连接", serviceAvailable ? _successBrush : _dangerBrush);

            var hasCredential = values.TryGetValue("hasCredential", out var credentialValue) && credentialValue == "1";
            SetStatus(CredentialStatusText, hasCredential ? "已保存" : "未保存", hasCredential ? _successBrush : _warningBrush);

            var autoSubmit = values.TryGetValue("autoSubmit", out var autoSubmitValue) && autoSubmitValue == "1";
            SetStatus(AutoSubmitStatusText, autoSubmit ? "已开启" : "手动", autoSubmit ? _successBrush : _secondaryBrush);

            var bluetoothEnabled = values.TryGetValue("bluetoothEnabled", out var bluetoothEnabledValue) && bluetoothEnabledValue == "1";
            values.TryGetValue("bluetoothLastStatus", out var bluetoothStatus);
            values.TryGetValue("bluetoothTrustedDeviceCount", out var trustedCount);
            SetBluetoothStatus(bluetoothEnabled, bluetoothStatus, trustedCount);

            var verified = values.TryGetValue("verifiedNow", out var verifiedValue) && verifiedValue == "1";
            SetStatus(VerifiedStatusText, verified ? "可解锁" : "未授权", verified ? _successBrush : _warningBrush);

            values.TryGetValue("bluetoothLastMatchedDeviceName", out var matchedName);
            values.TryGetValue("bluetoothLastMatchedDeviceAddress", out var matchedAddress);
            values.TryGetValue("verifiedUntilUtc", out var verifiedUntilUtc);
            SummaryText.Text =
                "配置文件: " + ConfigStore.ConfigPath + "\n" +
                "可信设备数量: " + (string.IsNullOrWhiteSpace(trustedCount) ? "0" : trustedCount) + "\n" +
                "最近命中: " + FormatDeviceName(matchedName, matchedAddress) + "\n" +
                "授权截止 UTC: " + (verifiedUntilUtc ?? "") + "\n" +
                "锁屏 Provider: Bluetooth Unlock";
        }

        private void SetBluetoothStatus(bool enabled, string status, string trustedCount)
        {
            if (!enabled)
            {
                SetStatus(BluetoothStatusText, "未启用", _secondaryBrush);
                return;
            }

            switch ((status ?? "").Trim().ToLowerInvariant())
            {
                case "nearby":
                    SetStatus(BluetoothStatusText, "已靠近", _successBrush);
                    break;
                case "not-nearby":
                    SetStatus(BluetoothStatusText, "未发现/" + (string.IsNullOrWhiteSpace(trustedCount) ? "0" : trustedCount), _warningBrush);
                    break;
                case "no-target":
                    SetStatus(BluetoothStatusText, "未选择", _warningBrush);
                    break;
                case "":
                    SetStatus(BluetoothStatusText, "等待扫描", _secondaryBrush);
                    break;
                default:
                    SetStatus(BluetoothStatusText, status, _warningBrush);
                    break;
            }
        }

        private void SetStatus(TextBlock target, string text, Brush brush)
        {
            target.Text = text;
            target.Foreground = brush;
        }

        private void RunScript(string scriptName, string arguments)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var scriptPath = Path.Combine(baseDir, "scripts", scriptName);
                if (!File.Exists(scriptPath))
                {
                    AppendOutput("找不到脚本: " + scriptPath);
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + arguments,
                    WorkingDirectory = baseDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var process = Process.Start(startInfo))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    AppendOutput(output + error + "ExitCode: " + process.ExitCode);
                }

                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput(scriptName + " 执行失败: " + ex.Message);
            }
        }

        private static string SendPipeCommand(string command)
        {
            using (var pipe = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.InOut))
            {
                pipe.Connect(3000);
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true))
                using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                {
                    writer.WriteLine(command);
                    var builder = new StringBuilder();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        builder.AppendLine(line);
                        if (line == "END")
                        {
                            break;
                        }
                    }

                    return builder.ToString();
                }
            }
        }

        private static Dictionary<string, string> ParseResponse(string response)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StringReader(response ?? ""))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var separator = line.IndexOf(':');
                    if (separator > 0)
                    {
                        values[line.Substring(0, separator)] = line.Substring(separator + 1);
                    }
                }
            }

            return values;
        }

        private void LoadTrustedDevices(UnlockConfig config)
        {
            TrustedDevicesListBox.Items.Clear();
            if (config.BluetoothTrustedDevices == null)
            {
                return;
            }

            foreach (var device in config.BluetoothTrustedDevices)
            {
                TrustedDevicesListBox.Items.Add(new BluetoothTrustedDevice
                {
                    Address = device.Address,
                    Name = device.Name,
                });
            }
        }

        private List<BluetoothTrustedDevice> GetTrustedDevices()
        {
            var devices = new List<BluetoothTrustedDevice>();
            foreach (var item in TrustedDevicesListBox.Items)
            {
                if (item is BluetoothTrustedDevice device)
                {
                    devices.Add(device);
                }
            }

            return devices;
        }

        private bool ContainsTrustedDevice(BluetoothTrustedDevice device)
        {
            foreach (var existing in GetTrustedDevices())
            {
                if (!string.IsNullOrWhiteSpace(device.Address) &&
                    string.Equals(BluetoothAddress.Normalize(existing.Address), BluetoothAddress.Normalize(device.Address), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(device.Address) &&
                    string.IsNullOrWhiteSpace(existing.Address) &&
                    string.Equals(existing.Name, device.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(OutputTextBox.Text ?? "");
            AppendOutput("诊断信息已复制。");
        }

        private void AppendOutput(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            OutputTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.TrimEnd() + Environment.NewLine + Environment.NewLine);
            OutputTextBox.ScrollToEnd();
        }

        private void UpdateAdminState()
        {
            var isAdmin = IsAdministrator();
            AdminText.Text = isAdmin ? "管理员模式已启用\n可以安装服务和 Provider" : "当前不是管理员\n安装/卸载会失败";
            AdminText.Foreground = isAdmin ? _successBrush : _dangerBrush;
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static int ParseBoundedInt(string text, int fallback, int min, int max)
        {
            if (!int.TryParse(text, out var value))
            {
                value = fallback;
            }

            return Clamp(value, min, max);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static string FormatDeviceName(string name, string address)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(address))
            {
                return "无";
            }

            return (name ?? "").Trim() + " " + BluetoothAddress.FormatWithSeparators(address);
        }

        private sealed class BluetoothDeviceViewModel
        {
            private readonly BluetoothDeviceInfo _device;

            public BluetoothDeviceViewModel(BluetoothDeviceInfo device)
            {
                _device = device;
                DisplayText =
                    (string.IsNullOrWhiteSpace(device.Name) ? "未命名设备" : device.Name) +
                    "  " +
                    BluetoothAddress.FormatWithSeparators(device.Address) +
                    (device.Connected ? "  已连接" : "") +
                    (BluetoothDeviceScanner.IsNearby(device) ? "  附近" : "");
            }

            public string DisplayText { get; }

            public BluetoothTrustedDevice ToTrustedDevice()
            {
                return new BluetoothTrustedDevice
                {
                    Address = _device.Address,
                    Name = _device.Name,
                };
            }
        }
    }
}
