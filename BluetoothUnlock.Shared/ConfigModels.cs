using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace BluetoothUnlock.Shared
{
    public enum VerifierMode
    {
        ManualTtl,
        AlwaysAllowTest
    }

    public sealed class UnlockConfig
    {
        public string Domain { get; set; } = ".";
        public string Username { get; set; } = "";
        public string ProtectedPassword { get; set; } = "";
        public VerifierMode VerifierMode { get; set; } = VerifierMode.ManualTtl;
        public DateTime VerifiedUntilUtc { get; set; } = DateTime.MinValue;
        public bool AutoSubmitOnVerified { get; set; } = false;
        public bool BluetoothUnlockEnabled { get; set; } = false;
        public string BluetoothDeviceAddress { get; set; } = "";
        public string BluetoothDeviceName { get; set; } = "";
        public List<BluetoothTrustedDevice> BluetoothTrustedDevices { get; set; } = new List<BluetoothTrustedDevice>();
        public int BluetoothProbeIntervalSeconds { get; set; } = 10;
        public int BluetoothGrantSeconds { get; set; } = 30;
        public DateTime BluetoothLastSeenUtc { get; set; } = DateTime.MinValue;
        public string BluetoothLastStatus { get; set; } = "";
        public string BluetoothLastMatchedDeviceAddress { get; set; } = "";
        public string BluetoothLastMatchedDeviceName { get; set; } = "";

        [XmlIgnore]
        public bool HasCredential =>
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(ProtectedPassword);
    }

    public sealed class BluetoothTrustedDevice
    {
        public string Address { get; set; } = "";
        public string Name { get; set; } = "";

        [XmlIgnore]
        public bool HasIdentity =>
            !string.IsNullOrWhiteSpace(Address) ||
            !string.IsNullOrWhiteSpace(Name);

        public override string ToString()
        {
            var name = string.IsNullOrWhiteSpace(Name) ? "未命名设备" : Name;
            var address = BluetoothAddress.FormatWithSeparators(Address);
            return string.IsNullOrWhiteSpace(address) ? name : name + "  " + address;
        }
    }

    public sealed class PlainCredential
    {
        public PlainCredential()
        {
        }

        public PlainCredential(string domain, string username, string password)
        {
            Domain = domain ?? ".";
            Username = username ?? "";
            Password = password ?? "";
        }

        public string Domain { get; set; } = ".";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public static class ConfigStore
    {
        public static readonly string ConfigDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BluetoothUnlock");

        public static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.xml");

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BluetoothUnlock.RouteA.Credential.v1");
        private static readonly object SyncRoot = new object();

        public static UnlockConfig Load()
        {
            lock (SyncRoot)
            {
                if (!File.Exists(ConfigPath))
                {
                    return new UnlockConfig();
                }

                using (var stream = File.OpenRead(ConfigPath))
                {
                    var serializer = new XmlSerializer(typeof(UnlockConfig));
                    return Normalize((UnlockConfig)serializer.Deserialize(stream));
                }
            }
        }

        public static void Save(UnlockConfig config)
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(ConfigDirectory);
                var tempPath = ConfigPath + ".tmp";

                using (var stream = File.Create(tempPath))
                {
                    var serializer = new XmlSerializer(typeof(UnlockConfig));
                    serializer.Serialize(stream, Normalize(config ?? new UnlockConfig()));
                }

                if (File.Exists(ConfigPath))
                {
                    File.Replace(tempPath, ConfigPath, null);
                }
                else
                {
                    File.Move(tempPath, ConfigPath);
                }
            }
        }

        public static UnlockConfig Update(Action<UnlockConfig> update)
        {
            lock (SyncRoot)
            {
                var config = Load();
                update(config);
                Save(config);
                return config;
            }
        }

        public static void SetCredential(string domain, string username, string password)
        {
            var config = Load();
            config.Domain = string.IsNullOrWhiteSpace(domain) ? "." : domain;
            config.Username = username ?? "";
            config.ProtectedPassword = Protect(password ?? "");
            Save(config);
        }

        public static void SetAutoSubmit(bool enabled)
        {
            var config = Load();
            config.AutoSubmitOnVerified = enabled;
            Save(config);
        }

        public static void SetBluetooth(
            bool enabled,
            string address,
            string name,
            int probeIntervalSeconds,
            int grantSeconds)
        {
            var config = Load();
            config.BluetoothUnlockEnabled = enabled;
            config.BluetoothDeviceAddress = BluetoothAddress.Normalize(address);
            config.BluetoothDeviceName = name ?? "";
            config.BluetoothTrustedDevices = new List<BluetoothTrustedDevice>
            {
                new BluetoothTrustedDevice
                {
                    Address = BluetoothAddress.Normalize(address),
                    Name = name ?? "",
                },
            };
            config.BluetoothProbeIntervalSeconds = Clamp(probeIntervalSeconds, 3, 300);
            config.BluetoothGrantSeconds = Clamp(grantSeconds, 5, 300);
            Save(config);
        }

        public static void SetBluetoothDevices(
            bool enabled,
            IEnumerable<BluetoothTrustedDevice> devices,
            int probeIntervalSeconds,
            int grantSeconds)
        {
            var config = Load();
            config.BluetoothUnlockEnabled = enabled;
            config.BluetoothTrustedDevices = NormalizeDevices(devices);
            var first = config.BluetoothTrustedDevices.Count > 0 ? config.BluetoothTrustedDevices[0] : null;
            config.BluetoothDeviceAddress = first == null ? "" : first.Address;
            config.BluetoothDeviceName = first == null ? "" : first.Name;
            config.BluetoothProbeIntervalSeconds = Clamp(probeIntervalSeconds, 3, 300);
            config.BluetoothGrantSeconds = Clamp(grantSeconds, 5, 300);
            Save(config);
        }

        public static PlainCredential GetCredential(UnlockConfig config)
        {
            if (config == null || !config.HasCredential)
            {
                return null;
            }

            return new PlainCredential(
                config.Domain,
                config.Username,
                Unprotect(config.ProtectedPassword));
        }

        private static string Protect(string value)
        {
            var data = Encoding.UTF8.GetBytes(value ?? "");
            var protectedData = ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
            Array.Clear(data, 0, data.Length);
            return Convert.ToBase64String(protectedData);
        }

        private static string Unprotect(string protectedValue)
        {
            var protectedData = Convert.FromBase64String(protectedValue);
            var data = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.LocalMachine);
            try
            {
                return Encoding.UTF8.GetString(data);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        private static UnlockConfig Normalize(UnlockConfig config)
        {
            if (config == null)
            {
                config = new UnlockConfig();
            }

            config.Domain = string.IsNullOrWhiteSpace(config.Domain) ? "." : config.Domain;
            config.Username = config.Username ?? "";
            config.ProtectedPassword = config.ProtectedPassword ?? "";
            config.BluetoothDeviceAddress = BluetoothAddress.Normalize(config.BluetoothDeviceAddress);
            config.BluetoothDeviceName = config.BluetoothDeviceName ?? "";
            config.BluetoothTrustedDevices = NormalizeDevices(config.BluetoothTrustedDevices);
            if (config.BluetoothTrustedDevices.Count == 0 &&
                (!string.IsNullOrWhiteSpace(config.BluetoothDeviceAddress) ||
                 !string.IsNullOrWhiteSpace(config.BluetoothDeviceName)))
            {
                config.BluetoothTrustedDevices.Add(new BluetoothTrustedDevice
                {
                    Address = config.BluetoothDeviceAddress,
                    Name = config.BluetoothDeviceName,
                });
            }

            if (config.BluetoothTrustedDevices.Count > 0)
            {
                config.BluetoothDeviceAddress = config.BluetoothTrustedDevices[0].Address;
                config.BluetoothDeviceName = config.BluetoothTrustedDevices[0].Name;
            }

            config.BluetoothProbeIntervalSeconds = Clamp(config.BluetoothProbeIntervalSeconds, 3, 300);
            config.BluetoothGrantSeconds = Clamp(config.BluetoothGrantSeconds, 5, 300);
            config.BluetoothLastStatus = config.BluetoothLastStatus ?? "";
            config.BluetoothLastMatchedDeviceAddress = BluetoothAddress.Normalize(config.BluetoothLastMatchedDeviceAddress);
            config.BluetoothLastMatchedDeviceName = config.BluetoothLastMatchedDeviceName ?? "";
            return config;
        }

        private static List<BluetoothTrustedDevice> NormalizeDevices(IEnumerable<BluetoothTrustedDevice> devices)
        {
            var normalized = new List<BluetoothTrustedDevice>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (devices == null)
            {
                return normalized;
            }

            foreach (var device in devices)
            {
                if (device == null)
                {
                    continue;
                }

                var address = BluetoothAddress.Normalize(device.Address);
                var name = device.Name ?? "";
                if (string.IsNullOrWhiteSpace(address) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(address)
                    ? "name:" + name.Trim().ToUpperInvariant()
                    : "addr:" + address;
                if (!seen.Add(key))
                {
                    continue;
                }

                normalized.Add(new BluetoothTrustedDevice
                {
                    Address = address,
                    Name = name,
                });
            }

            return normalized;
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
    }
}
