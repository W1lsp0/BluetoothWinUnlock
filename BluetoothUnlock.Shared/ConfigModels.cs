using System;
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

        [XmlIgnore]
        public bool HasCredential =>
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(ProtectedPassword);
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

        public static UnlockConfig Load()
        {
            if (!File.Exists(ConfigPath))
            {
                return new UnlockConfig();
            }

            using (var stream = File.OpenRead(ConfigPath))
            {
                var serializer = new XmlSerializer(typeof(UnlockConfig));
                return (UnlockConfig)serializer.Deserialize(stream);
            }
        }

        public static void Save(UnlockConfig config)
        {
            Directory.CreateDirectory(ConfigDirectory);
            var tempPath = ConfigPath + ".tmp";

            using (var stream = File.Create(tempPath))
            {
                var serializer = new XmlSerializer(typeof(UnlockConfig));
                serializer.Serialize(stream, config ?? new UnlockConfig());
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
    }
}
