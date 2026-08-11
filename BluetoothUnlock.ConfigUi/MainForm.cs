using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.ConfigUi
{
    public sealed class MainForm : Form
    {
        private static readonly Color WindowBack = Color.FromArgb(245, 247, 250);
        private static readonly Color CardBack = Color.White;
        private static readonly Color TextPrimary = Color.FromArgb(28, 35, 45);
        private static readonly Color TextSecondary = Color.FromArgb(94, 103, 116);
        private static readonly Color Primary = Color.FromArgb(37, 99, 235);
        private static readonly Color Success = Color.FromArgb(22, 163, 74);
        private static readonly Color Warning = Color.FromArgb(202, 138, 4);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);
        private static readonly Color ConsoleBack = Color.FromArgb(18, 24, 38);
        private static readonly Color ConsoleText = Color.FromArgb(220, 230, 245);

        private readonly TextBox _domainTextBox = new TextBox();
        private readonly TextBox _usernameTextBox = new TextBox();
        private readonly TextBox _passwordTextBox = new TextBox();
        private readonly CheckBox _autoSubmitCheckBox = new CheckBox();
        private readonly NumericUpDown _secondsInput = new NumericUpDown();
        private readonly TextBox _outputTextBox = new TextBox();
        private readonly Label _adminLabel = new Label();
        private readonly Label _serviceStatusLabel = new Label();
        private readonly Label _credentialStatusLabel = new Label();
        private readonly Label _autoSubmitStatusLabel = new Label();
        private readonly Label _verifiedStatusLabel = new Label();

        public MainForm()
        {
            Text = "BluetoothWinUnlock";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 780);
            Size = new Size(980, 840);
            BackColor = WindowBack;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildLayout();
            LoadConfig();
            RefreshStatus();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(24),
                BackColor = WindowBack,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildStatusCards(), 0, 1);
            root.Controls.Add(BuildMainControls(), 0, 2);
            root.Controls.Add(BuildLogPanel(), 0, 3);
            root.Controls.Add(BuildFooter(), 0, 4);
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = WindowBack,
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

            var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = WindowBack };
            var title = new Label
            {
                Text = "BluetoothWinUnlock",
                Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 2),
                AutoSize = true,
            };
            var subtitle = new Label
            {
                Text = "蓝牙触发的 Windows Credential Provider 解锁工具",
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextSecondary,
                Location = new Point(2, 48),
                AutoSize = true,
            };
            titlePanel.Controls.Add(title);
            titlePanel.Controls.Add(subtitle);

            var adminCard = CreateCard();
            adminCard.Padding = new Padding(14, 12, 14, 12);
            _adminLabel.Dock = DockStyle.Fill;
            _adminLabel.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _adminLabel.TextAlign = ContentAlignment.MiddleLeft;
            _adminLabel.ForeColor = IsAdministrator() ? Success : Danger;
            _adminLabel.Text = IsAdministrator()
                ? "管理员模式已启用\n可以安装服务和 Provider"
                : "当前不是管理员\n安装/卸载会失败";
            adminCard.Controls.Add(_adminLabel);

            header.Controls.Add(titlePanel, 0, 0);
            header.Controls.Add(adminCard, 1, 0);
            return header;
        }

        private Control BuildStatusCards()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = WindowBack,
                Padding = new Padding(0, 8, 0, 8),
            };
            for (var i = 0; i < 4; i++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            }

            grid.Controls.Add(CreateStatusCard("服务", _serviceStatusLabel), 0, 0);
            grid.Controls.Add(CreateStatusCard("凭据", _credentialStatusLabel), 1, 0);
            grid.Controls.Add(CreateStatusCard("自动提交", _autoSubmitStatusLabel), 2, 0);
            grid.Controls.Add(CreateStatusCard("授权窗口", _verifiedStatusLabel), 3, 0);
            return grid;
        }

        private Control BuildMainControls()
        {
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = WindowBack,
                Padding = new Padding(0, 4, 0, 12),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

            grid.Controls.Add(BuildCredentialCard(), 0, 0);
            grid.Controls.Add(BuildActionCard(), 1, 0);
            return grid;
        }

        private Control BuildCredentialCard()
        {
            var card = CreateCard();
            card.Padding = new Padding(18);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = CardBack,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(grid);

            var title = new Label
            {
                Text = "Windows 凭据",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
            };
            grid.Controls.Add(title, 0, 0);
            grid.SetColumnSpan(title, 2);

            StyleTextBox(_domainTextBox);
            StyleTextBox(_usernameTextBox);
            StyleTextBox(_passwordTextBox);
            _passwordTextBox.UseSystemPasswordChar = true;

            AddLabeledInput(grid, "域", _domainTextBox, 1);
            AddLabeledInput(grid, "用户名", _usernameTextBox, 2);
            AddLabeledInput(grid, "密码", _passwordTextBox, 3);

            _autoSubmitCheckBox.Text = "授权后自动提交 Bluetooth Unlock";
            _autoSubmitCheckBox.AutoSize = true;
            _autoSubmitCheckBox.ForeColor = TextPrimary;
            _autoSubmitCheckBox.Margin = new Padding(0, 8, 0, 0);
            grid.Controls.Add(new Label(), 0, 4);
            grid.Controls.Add(_autoSubmitCheckBox, 1, 4);

            var saveButton = CreateButton("保存凭据", Primary, Color.White);
            saveButton.Width = 136;
            saveButton.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            saveButton.Click += (sender, args) => SaveCredential();
            grid.Controls.Add(new Label(), 0, 5);
            grid.Controls.Add(saveButton, 1, 5);

            return card;
        }

        private Control BuildActionCard()
        {
            var card = CreateCard();
            card.Padding = new Padding(18);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                BackColor = CardBack,
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(panel);

            var title = new Label
            {
                Text = "操作",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
            };
            panel.Controls.Add(title, 0, 0);
            panel.SetColumnSpan(title, 2);

            AddActionButton(panel, "安装服务", () => RunScript("install-service.ps1", "-ServiceExe .\\BluetoothUnlock.Service.exe"), 0, 1, Primary);
            AddActionButton(panel, "安装 Provider", () => RunScript("install-provider.ps1", "-ProviderDll .\\BluetoothUnlock.Provider.dll"), 1, 1, Primary);
            AddActionButton(panel, "刷新状态", RefreshStatus, 0, 2, Color.FromArgb(71, 85, 105));
            AddActionButton(panel, "卸载", () => RunScript("uninstall.ps1", ""), 1, 2, Danger);
            AddActionButton(panel, "保存凭据", SaveCredential, 0, 3, Primary);

            var grantPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = CardBack,
                Padding = new Padding(0, 4, 0, 0),
            };
            _secondsInput.Minimum = 1;
            _secondsInput.Maximum = 300;
            _secondsInput.Value = 60;
            _secondsInput.Width = 72;
            _secondsInput.Height = 30;
            grantPanel.Controls.Add(new Label
            {
                Text = "授权秒数",
                AutoSize = true,
                ForeColor = TextSecondary,
                Padding = new Padding(0, 7, 8, 0),
            });
            grantPanel.Controls.Add(_secondsInput);
            panel.Controls.Add(grantPanel, 0, 4);

            var grantButton = CreateButton("测试授权", Success, Color.White);
            grantButton.Dock = DockStyle.Fill;
            grantButton.Margin = new Padding(6, 4, 0, 6);
            grantButton.Click += (sender, args) => Grant();
            panel.Controls.Add(grantButton, 1, 4);

            var hint = new Label
            {
                Text = "先安装服务和 Provider，再保存凭据。启用自动提交后，授权窗口内锁屏会自动提交解锁。",
                Dock = DockStyle.Fill,
                ForeColor = TextSecondary,
                Padding = new Padding(0, 10, 0, 0),
            };
            panel.Controls.Add(hint, 0, 5);
            panel.SetColumnSpan(hint, 2);

            return card;
        }

        private Control BuildLogPanel()
        {
            var card = CreateCard();
            card.Padding = new Padding(18);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CardBack,
            };
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(grid);

            var title = new Label
            {
                Text = "状态日志",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
            };
            grid.Controls.Add(title, 0, 0);

            _outputTextBox.Dock = DockStyle.Fill;
            _outputTextBox.Multiline = true;
            _outputTextBox.ReadOnly = true;
            _outputTextBox.BorderStyle = BorderStyle.None;
            _outputTextBox.ScrollBars = ScrollBars.Vertical;
            _outputTextBox.Font = new Font("Consolas", 9F);
            _outputTextBox.BackColor = ConsoleBack;
            _outputTextBox.ForeColor = ConsoleText;
            _outputTextBox.Margin = new Padding(0, 4, 0, 0);
            grid.Controls.Add(_outputTextBox, 0, 1);

            return card;
        }

        private Control BuildFooter()
        {
            return new Label
            {
                Text = "请使用 Windows 登录密码，不是 Windows Hello PIN。锁屏 Provider 名称为 Bluetooth Unlock。",
                AutoSize = false,
                Dock = DockStyle.Fill,
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
            };
        }

        private Panel CreateCard()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBack,
                Margin = new Padding(6),
            };
        }

        private Control CreateStatusCard(string title, Label valueLabel)
        {
            var card = CreateCard();
            card.Padding = new Padding(14, 10, 14, 10);

            var titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9F),
            };
            valueLabel.Text = "--";
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.ForeColor = TextPrimary;
            valueLabel.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;

            card.Controls.Add(valueLabel);
            card.Controls.Add(titleLabel);
            return card;
        }

        private static void StyleTextBox(TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 4, 0, 4);
            textBox.Font = new Font("Segoe UI", 9.5F);
        }

        private void AddLabeledInput(TableLayoutPanel grid, string label, Control control, int row)
        {
            grid.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
            }, 0, row);
            grid.Controls.Add(control, 1, row);
        }

        private Button CreateButton(string text, Color backColor, Color foreColor)
        {
            var button = new Button
            {
                Text = text,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Height = 34,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 6, 4),
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void AddActionButton(TableLayoutPanel panel, string text, Action action, int column, int row, Color color)
        {
            var button = CreateButton(text, color, Color.White);
            button.Dock = DockStyle.Fill;
            button.Click += (sender, args) => action();
            panel.Controls.Add(button, column, row);
        }

        private void LoadConfig()
        {
            try
            {
                var config = ConfigStore.Load();
                _domainTextBox.Text = string.IsNullOrWhiteSpace(config.Domain) ? "." : config.Domain;
                _usernameTextBox.Text = config.Username ?? "";
                _autoSubmitCheckBox.Checked = config.AutoSubmitOnVerified;
            }
            catch (Exception ex)
            {
                AppendOutput("加载配置失败: " + ex.Message);
            }
        }

        private void SaveCredential()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_usernameTextBox.Text))
                {
                    MessageBox.Show(this, "请填写 Windows 用户名。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var config = ConfigStore.Load();
                if (!string.IsNullOrEmpty(_passwordTextBox.Text))
                {
                    ConfigStore.SetCredential(_domainTextBox.Text, _usernameTextBox.Text, _passwordTextBox.Text);
                    config = ConfigStore.Load();
                }
                else if (!config.HasCredential)
                {
                    MessageBox.Show(this, "首次保存凭据必须填写 Windows 登录密码。", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                config.Domain = string.IsNullOrWhiteSpace(_domainTextBox.Text) ? "." : _domainTextBox.Text;
                config.Username = _usernameTextBox.Text;
                config.AutoSubmitOnVerified = _autoSubmitCheckBox.Checked;
                ConfigStore.Save(config);
                _passwordTextBox.Clear();
                AppendOutput("凭据已保存: " + ConfigStore.ConfigPath);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("保存凭据失败: " + ex.Message);
            }
        }

        private void Grant()
        {
            try
            {
                AppendOutput(SendPipeCommand("GRANT " + (int)_secondsInput.Value));
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("测试授权失败: " + ex.Message);
            }
        }

        private void RefreshStatus()
        {
            try
            {
                var response = SendPipeCommand("STATUS");
                AppendOutput(response);
                UpdateStatusBadges(ParseResponse(response), true);
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
                        ["verifiedNow"] = "0",
                    };
                    UpdateStatusBadges(values, false);
                    AppendOutput(
                        "服务管道不可用。\r\n" +
                        "配置文件: " + ConfigStore.ConfigPath + "\r\n" +
                        "凭据已保存: " + config.HasCredential + "\r\n" +
                        "模式: " + config.VerifierMode + "\r\n" +
                        "自动提交: " + config.AutoSubmitOnVerified + "\r\n" +
                        "授权截止 UTC: " + config.VerifiedUntilUtc.ToString("O"));
                }
                catch
                {
                    SetStatusBadge(_serviceStatusLabel, "异常", Danger);
                    AppendOutput("刷新状态失败: " + ex.Message);
                }
            }
        }

        private void UpdateStatusBadges(Dictionary<string, string> values, bool serviceAvailable)
        {
            SetStatusBadge(_serviceStatusLabel, serviceAvailable ? "运行中" : "未连接", serviceAvailable ? Success : Danger);

            var hasCredential = values.TryGetValue("hasCredential", out var credentialValue) && credentialValue == "1";
            SetStatusBadge(_credentialStatusLabel, hasCredential ? "已保存" : "未保存", hasCredential ? Success : Warning);

            var autoSubmit = values.TryGetValue("autoSubmit", out var autoSubmitValue) && autoSubmitValue == "1";
            SetStatusBadge(_autoSubmitStatusLabel, autoSubmit ? "已开启" : "手动", autoSubmit ? Success : TextSecondary);

            var verified = values.TryGetValue("verifiedNow", out var verifiedValue) && verifiedValue == "1";
            SetStatusBadge(_verifiedStatusLabel, verified ? "可解锁" : "未授权", verified ? Success : Warning);
        }

        private static void SetStatusBadge(Label label, string text, Color color)
        {
            label.Text = text;
            label.ForeColor = color;
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

        private void AppendOutput(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _outputTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.TrimEnd() + Environment.NewLine + Environment.NewLine);
        }

        private static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
