using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using BluetoothUnlock.Shared;

namespace BluetoothUnlock.Config
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || HasArg(args, "--help") || HasArg(args, "-h"))
                {
                    PrintUsage();
                    return 0;
                }

                var command = args[0].ToLowerInvariant();
                var options = ParseOptions(args);

                switch (command)
                {
                    case "set-credential":
                        return SetCredential(options);
                    case "set-mode":
                        return SetMode(options);
                    case "set-auto-submit":
                        return SetAutoSubmit(options);
                    case "set-bluetooth":
                        return SetBluetooth(options);
                    case "add-bluetooth":
                        return AddBluetooth(options);
                    case "remove-bluetooth":
                        return RemoveBluetooth(options);
                    case "list-bluetooth":
                        return ListBluetooth(options);
                    case "grant":
                    case "bluetooth-verified":
                        return Grant(options);
                    case "status":
                        return Status();
                    case "clear":
                        ConfigStore.Save(new UnlockConfig());
                        Console.WriteLine("Configuration cleared.");
                        return 0;
                    default:
                        Console.Error.WriteLine("Unknown command: " + args[0]);
                        PrintUsage();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int SetCredential(Dictionary<string, string> options)
        {
            var username = Require(options, "username");
            var password = Require(options, "password");
            options.TryGetValue("domain", out var domain);

            ConfigStore.SetCredential(domain, username, password);
            Console.WriteLine("Credential saved to " + ConfigStore.ConfigPath);
            return 0;
        }

        private static int SetMode(Dictionary<string, string> options)
        {
            var modeText = Require(options, "mode");
            if (!Enum.TryParse<VerifierMode>(modeText, true, out var mode))
            {
                throw new ArgumentException("Invalid mode. Use ManualTtl or AlwaysAllowTest.");
            }

            var config = ConfigStore.Load();
            config.VerifierMode = mode;
            ConfigStore.Save(config);
            Console.WriteLine("Mode set to " + mode + ".");
            return 0;
        }

        private static int SetAutoSubmit(Dictionary<string, string> options)
        {
            var enabledText = Require(options, "enabled");
            if (!bool.TryParse(enabledText, out var enabled))
            {
                throw new ArgumentException("Invalid --enabled value. Use true or false.");
            }

            ConfigStore.SetAutoSubmit(enabled);
            Console.WriteLine("Auto submit set to " + enabled + ".");
            return 0;
        }

        private static int Grant(Dictionary<string, string> options)
        {
            var seconds = 30;
            if (options.TryGetValue("seconds", out var value) && !int.TryParse(value, out seconds))
            {
                throw new ArgumentException("--seconds must be an integer.");
            }

            var response = SendPipeCommand("GRANT " + seconds);
            Console.Write(response);
            return response.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }

        private static int Status()
        {
            try
            {
                Console.Write(SendPipeCommand("STATUS"));
            }
            catch (IOException)
            {
                var config = ConfigStore.Load();
                Console.WriteLine("Service pipe unavailable.");
                Console.WriteLine("Config path: " + ConfigStore.ConfigPath);
                Console.WriteLine("Protocol version: offline");
                Console.WriteLine("Has credential: " + config.HasCredential);
                Console.WriteLine("Mode: " + config.VerifierMode);
                Console.WriteLine("Auto submit: " + config.AutoSubmitOnVerified);
                Console.WriteLine("Bluetooth enabled: " + config.BluetoothUnlockEnabled);
                Console.WriteLine("Bluetooth trusted devices: " + (config.BluetoothTrustedDevices == null ? 0 : config.BluetoothTrustedDevices.Count));
                Console.WriteLine("Bluetooth first target: " + config.BluetoothDeviceName + " " + BluetoothAddress.FormatWithSeparators(config.BluetoothDeviceAddress));
                Console.WriteLine("Bluetooth status: " + config.BluetoothLastStatus);
                Console.WriteLine("Bluetooth matched: " + config.BluetoothLastMatchedDeviceName + " " + BluetoothAddress.FormatWithSeparators(config.BluetoothLastMatchedDeviceAddress));
                Console.WriteLine("Verified until UTC: " + config.VerifiedUntilUtc.ToString("O"));
            }

            return 0;
        }

        private static int AddBluetooth(Dictionary<string, string> options)
        {
            options.TryGetValue("address", out var address);
            options.TryGetValue("name", out var name);
            if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Missing --address or --name.");
            }

            var config = ConfigStore.Load();
            var devices = new List<BluetoothTrustedDevice>(config.BluetoothTrustedDevices ?? new List<BluetoothTrustedDevice>())
            {
                new BluetoothTrustedDevice
                {
                    Address = BluetoothAddress.Normalize(address),
                    Name = name ?? "",
                },
            };

            ConfigStore.SetBluetoothDevices(
                true,
                devices,
                config.BluetoothProbeIntervalSeconds,
                config.BluetoothGrantSeconds);
            Console.WriteLine("Bluetooth device added.");
            return 0;
        }

        private static int RemoveBluetooth(Dictionary<string, string> options)
        {
            options.TryGetValue("address", out var address);
            options.TryGetValue("name", out var name);
            var normalizedAddress = BluetoothAddress.Normalize(address);
            var normalizedName = (name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedAddress) && string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new ArgumentException("Missing --address or --name.");
            }

            var config = ConfigStore.Load();
            var devices = new List<BluetoothTrustedDevice>();
            foreach (var device in config.BluetoothTrustedDevices ?? new List<BluetoothTrustedDevice>())
            {
                var addressMatches = !string.IsNullOrWhiteSpace(normalizedAddress) &&
                    string.Equals(BluetoothAddress.Normalize(device.Address), normalizedAddress, StringComparison.OrdinalIgnoreCase);
                var nameMatches = string.IsNullOrWhiteSpace(normalizedAddress) &&
                    !string.IsNullOrWhiteSpace(normalizedName) &&
                    string.Equals(device.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase);
                if (!addressMatches && !nameMatches)
                {
                    devices.Add(device);
                }
            }

            ConfigStore.SetBluetoothDevices(
                config.BluetoothUnlockEnabled,
                devices,
                config.BluetoothProbeIntervalSeconds,
                config.BluetoothGrantSeconds);
            Console.WriteLine("Bluetooth device removed.");
            return 0;
        }

        private static int SetBluetooth(Dictionary<string, string> options)
        {
            var enabled = false;
            if (options.TryGetValue("enabled", out var enabledText) && !bool.TryParse(enabledText, out enabled))
            {
                throw new ArgumentException("Invalid --enabled value. Use true or false.");
            }

            options.TryGetValue("address", out var address);
            options.TryGetValue("name", out var name);

            var probeSeconds = 10;
            if (options.TryGetValue("probe-seconds", out var probeText) && !int.TryParse(probeText, out probeSeconds))
            {
                throw new ArgumentException("--probe-seconds must be an integer.");
            }

            var grantSeconds = 30;
            if (options.TryGetValue("grant-seconds", out var grantText) && !int.TryParse(grantText, out grantSeconds))
            {
                throw new ArgumentException("--grant-seconds must be an integer.");
            }

            ConfigStore.SetBluetooth(enabled, address, name, probeSeconds, grantSeconds);
            Console.WriteLine("Bluetooth unlock set to " + enabled + ".");
            Console.WriteLine("Target: " + (name ?? "") + " " + BluetoothAddress.FormatWithSeparators(address));
            Console.WriteLine("Probe seconds: " + probeSeconds);
            Console.WriteLine("Grant seconds: " + grantSeconds);
            return 0;
        }

        private static int ListBluetooth(Dictionary<string, string> options)
        {
            var inquiry = !options.TryGetValue("inquiry", out var value) || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            var devices = BluetoothDeviceScanner.FindDevices(inquiry);

            if (devices.Count == 0)
            {
                Console.WriteLine("No Bluetooth devices found.");
                return 0;
            }

            foreach (var device in devices)
            {
                Console.WriteLine(
                    BluetoothAddress.FormatWithSeparators(device.Address) +
                    "\t" + device.Name +
                    "\tconnected:" + (device.Connected ? "1" : "0") +
                    "\tnearby:" + (BluetoothDeviceScanner.IsNearby(device) ? "1" : "0") +
                    "\tremembered:" + (device.Remembered ? "1" : "0") +
                    "\tauthenticated:" + (device.Authenticated ? "1" : "0") +
                    "\tlastSeenUtc:" + device.LastSeenUtc.ToString("O"));
            }

            return 0;
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

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < args.Length; i++)
            {
                var key = args[i];
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                key = key.Substring(2);
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[key] = "true";
                }
                else
                {
                    options[key] = args[++i];
                }
            }

            return options;
        }

        private static string Require(Dictionary<string, string> options, string key)
        {
            if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Missing --" + key + ".");
            }

            return value;
        }

        private static bool HasArg(string[] args, string value)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("BluetoothUnlock.Config");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  set-credential --domain . --username alice --password secret");
            Console.WriteLine("  set-mode --mode ManualTtl");
            Console.WriteLine("  set-mode --mode AlwaysAllowTest");
            Console.WriteLine("  set-auto-submit --enabled true");
            Console.WriteLine("  list-bluetooth");
            Console.WriteLine("  set-bluetooth --enabled true --address AABBCCDDEEFF --name phone --probe-seconds 10 --grant-seconds 30");
            Console.WriteLine("  add-bluetooth --address AABBCCDDEEFF --name phone");
            Console.WriteLine("  remove-bluetooth --address AABBCCDDEEFF");
            Console.WriteLine("  grant --seconds 30");
            Console.WriteLine("  bluetooth-verified --seconds 30");
            Console.WriteLine("  status");
            Console.WriteLine("  clear");
        }
    }
}
