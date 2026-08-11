using System;
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
        private readonly TextBox _domainTextBox = new TextBox();
        private readonly TextBox _usernameTextBox = new TextBox();
        private readonly TextBox _passwordTextBox = new TextBox();
        private readonly NumericUpDown _secondsInput = new NumericUpDown();
        private readonly TextBox _outputTextBox = new TextBox();
        private readonly Label _adminLabel = new Label();

        public MainForm()
        {
            Text = "BluetoothWinUnlock";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(620, 540);
            Size = new Size(680, 620);
            Font = new Font("Segoe UI", 9F);

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
                Padding = new Padding(16),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var title = new Label
            {
                Text = "BluetoothWinUnlock",
                Dock = DockStyle.Top,
                Font = new Font(Font.FontFamily, 18F, FontStyle.Bold),
                Height = 38,
            };
            root.Controls.Add(title);

            _adminLabel.AutoSize = true;
            _adminLabel.ForeColor = IsAdministrator() ? Color.SeaGreen : Color.Firebrick;
            _adminLabel.Text = IsAdministrator()
                ? "Administrator: installation commands are available."
                : "Not running as Administrator. Install/uninstall may fail.";
            root.Controls.Add(_adminLabel);

            var credentialGroup = new GroupBox
            {
                Text = "Windows credential",
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(12),
            };
            root.Controls.Add(credentialGroup);

            var credentialGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
            };
            credentialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            credentialGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            credentialGroup.Controls.Add(credentialGrid);

            AddLabeledControl(credentialGrid, "Domain", _domainTextBox, 0);
            AddLabeledControl(credentialGrid, "Username", _usernameTextBox, 1);
            _passwordTextBox.UseSystemPasswordChar = true;
            AddLabeledControl(credentialGrid, "Password", _passwordTextBox, 2);

            var saveButton = new Button { Text = "Save credential", Dock = DockStyle.Left, Width = 140 };
            saveButton.Click += (sender, args) => SaveCredential();
            credentialGrid.Controls.Add(new Label(), 0, 3);
            credentialGrid.Controls.Add(saveButton, 1, 3);

            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 92,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 8),
            };
            root.Controls.Add(actionPanel);

            AddButton(actionPanel, "Install service", () => RunScript("install-service.ps1", "-ServiceExe .\\BluetoothUnlock.Service.exe"));
            AddButton(actionPanel, "Install provider", () => RunScript("install-provider.ps1", "-ProviderDll .\\BluetoothUnlock.Provider.dll"));
            AddButton(actionPanel, "Uninstall", () => RunScript("uninstall.ps1", ""));
            AddButton(actionPanel, "Refresh status", RefreshStatus);

            _secondsInput.Minimum = 1;
            _secondsInput.Maximum = 300;
            _secondsInput.Value = 60;
            _secondsInput.Width = 70;
            actionPanel.Controls.Add(new Label { Text = "Grant seconds", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
            actionPanel.Controls.Add(_secondsInput);
            AddButton(actionPanel, "Grant test unlock", Grant);

            _outputTextBox.Dock = DockStyle.Fill;
            _outputTextBox.Multiline = true;
            _outputTextBox.ReadOnly = true;
            _outputTextBox.ScrollBars = ScrollBars.Vertical;
            _outputTextBox.Font = new Font("Consolas", 9F);
            root.Controls.Add(_outputTextBox);

            var footer = new Label
            {
                Text = "Use Windows password, not Windows Hello PIN. Lock screen tile appears as Bluetooth Unlock.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Dock = DockStyle.Bottom,
            };
            root.Controls.Add(footer);
        }

        private static void AddLabeledControl(TableLayoutPanel grid, string label, Control control, int row)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            control.Dock = DockStyle.Fill;
            grid.Controls.Add(control, 1, row);
        }

        private static void AddButton(FlowLayoutPanel panel, string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                Width = 130,
                Height = 30,
                Margin = new Padding(4),
            };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button);
        }

        private void LoadConfig()
        {
            try
            {
                var config = ConfigStore.Load();
                _domainTextBox.Text = string.IsNullOrWhiteSpace(config.Domain) ? "." : config.Domain;
                _usernameTextBox.Text = config.Username ?? "";
            }
            catch (Exception ex)
            {
                AppendOutput("Load config failed: " + ex.Message);
            }
        }

        private void SaveCredential()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_usernameTextBox.Text))
                {
                    MessageBox.Show(this, "Username is required.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ConfigStore.SetCredential(_domainTextBox.Text, _usernameTextBox.Text, _passwordTextBox.Text);
                _passwordTextBox.Clear();
                AppendOutput("Credential saved to " + ConfigStore.ConfigPath);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendOutput("Save credential failed: " + ex.Message);
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
                AppendOutput("Grant failed: " + ex.Message);
            }
        }

        private void RefreshStatus()
        {
            try
            {
                AppendOutput(SendPipeCommand("STATUS"));
            }
            catch (Exception ex)
            {
                try
                {
                    var config = ConfigStore.Load();
                    AppendOutput(
                        "Service pipe unavailable.\r\n" +
                        "Config path: " + ConfigStore.ConfigPath + "\r\n" +
                        "Has credential: " + config.HasCredential + "\r\n" +
                        "Mode: " + config.VerifierMode + "\r\n" +
                        "Verified until UTC: " + config.VerifiedUntilUtc.ToString("O"));
                }
                catch
                {
                    AppendOutput("Status failed: " + ex.Message);
                }
            }
        }

        private void RunScript(string scriptName, string arguments)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var scriptPath = Path.Combine(baseDir, "scripts", scriptName);
                if (!File.Exists(scriptPath))
                {
                    AppendOutput("Script not found: " + scriptPath);
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
                AppendOutput(scriptName + " failed: " + ex.Message);
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

